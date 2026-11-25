using System.Buffers.Binary;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Infrastructure.Storefront;

/// <summary>
/// <see cref="IEmailDomainChecker"/> with no extra package: a minimal DNS MX query over UDP to the machine's own
/// resolvers, then the RFC 5321 fallback (a domain with an A/AAAA record can also receive mail). Answers are cached
/// for an hour. Any timeout, socket error or malformed reply counts as "can receive", so a DNS hiccup never stops a
/// checkout; only a definite "this domain does not exist / has no mail host" (NXDOMAIN, or no MX and no address)
/// is a "no".
/// </summary>
public class EmailDomainChecker : IEmailDomainChecker
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CacheFor = TimeSpan.FromHours(1);

    private readonly IMemoryCache _cache;
    private readonly ILogger<EmailDomainChecker> _logger;

    public EmailDomainChecker(IMemoryCache cache, ILogger<EmailDomainChecker> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> CanReceiveMailAsync(string email, CancellationToken cancellationToken = default)
    {
        var at = (email ?? string.Empty).Trim().LastIndexOf('@');
        if (at < 0)
            return false;
        var domain = email!.Trim()[(at + 1)..].TrimEnd('.').ToLowerInvariant();
        if (domain.Length == 0 || domain.Length > 253)
            return false;

        if (_cache.TryGetValue("mx:" + domain, out bool cached))
            return cached;

        bool result;
        try
        {
            result = await LookupAsync(domain, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "MX lookup failed; allowing the email domain.");
            return true; // not cached: try again next time
        }
        _cache.Set("mx:" + domain, result, CacheFor);
        return result;
    }

    private static async Task<bool> LookupAsync(string domain, CancellationToken ct)
    {
        var mx = await QueryMxAsync(domain, ct);
        if (mx == MxAnswer.HasMx)
            return true;
        if (mx == MxAnswer.NoSuchDomain)
            return false;
        if (mx == MxAnswer.Unknown)
            return true;

        // No MX: a domain with an address record can still take mail (RFC 5321 §5.1).
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(domain, ct);
            return addresses.Length > 0;
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.HostNotFound)
        {
            return false;
        }
    }

    private enum MxAnswer { HasMx, NoMx, NoSuchDomain, Unknown }

    /// <summary>One MX question to the first configured IPv4 resolver that answers.</summary>
    private static async Task<MxAnswer> QueryMxAsync(string domain, CancellationToken ct)
    {
        var servers = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up)
            .SelectMany(n => n.GetIPProperties().DnsAddresses)
            .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
            .Distinct()
            .Take(2)
            .ToList();
        if (servers.Count == 0)
            return MxAnswer.Unknown;

        var id = (ushort)Random.Shared.Next(ushort.MaxValue);
        var query = BuildQuery(id, domain);
        foreach (var server in servers)
        {
            try
            {
                using var udp = new UdpClient(AddressFamily.InterNetwork);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(Timeout);
                await udp.SendAsync(query, new IPEndPoint(server, 53), timeout.Token);
                var reply = await udp.ReceiveAsync(timeout.Token);
                var answer = Parse(reply.Buffer, id);
                if (answer != MxAnswer.Unknown)
                    return answer;
            }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException && !ct.IsCancellationRequested)
            {
                // try the next resolver
            }
        }
        return MxAnswer.Unknown;
    }

    private static byte[] BuildQuery(ushort id, string domain)
    {
        var buffer = new List<byte>(32 + domain.Length);
        var header = new byte[12];
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(0), id);
        header[2] = 0x01; // recursion desired
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4), 1); // one question
        buffer.AddRange(header);
        foreach (var label in domain.Split('.'))
        {
            var bytes = System.Text.Encoding.ASCII.GetBytes(new IdnMapping().GetAscii(label));
            if (bytes.Length is 0 or > 63)
                throw new FormatException("Bad domain label.");
            buffer.Add((byte)bytes.Length);
            buffer.AddRange(bytes);
        }
        buffer.Add(0);
        buffer.AddRange(new byte[] { 0x00, 0x0F, 0x00, 0x01 }); // QTYPE = MX, QCLASS = IN
        return buffer.ToArray();
    }

    /// <summary>Reads only the header: the reply code and how many answers came back.</summary>
    private static MxAnswer Parse(byte[] reply, ushort id)
    {
        if (reply.Length < 12 || BinaryPrimitives.ReadUInt16BigEndian(reply.AsSpan(0)) != id)
            return MxAnswer.Unknown;
        if ((reply[2] & 0x02) != 0)
            return MxAnswer.Unknown; // truncated: don't guess
        var rcode = reply[3] & 0x0F;
        if (rcode == 3)
            return MxAnswer.NoSuchDomain;
        if (rcode != 0)
            return MxAnswer.Unknown;
        var answers = BinaryPrimitives.ReadUInt16BigEndian(reply.AsSpan(6));
        return answers > 0 ? MxAnswer.HasMx : MxAnswer.NoMx;
    }
}
