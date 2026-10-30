using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Email;

/// <summary>MailKit-based SMTP test sender.</summary>
public class MailKitSmtpTester : ISmtpTester
{
    public async Task<ConnectivityTestResult> TestSendAsync(SmtpTestRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(request.FromName, request.FromEmail));
            message.To.Add(MailboxAddress.Parse(request.ToEmail));
            message.Subject = "HK Parts — SMTP test message";
            message.Body = new TextPart("plain")
            {
                Text = "This is a test message confirming your SMTP account is configured correctly.",
            };

            var secureOptions = request.EncryptionMode switch
            {
                SmtpEncryptionMode.None => SecureSocketOptions.None,
                SmtpEncryptionMode.Ssl => SecureSocketOptions.SslOnConnect,
                SmtpEncryptionMode.Tls => SecureSocketOptions.StartTls,
                SmtpEncryptionMode.StartTls => SecureSocketOptions.StartTls,
                _ => SecureSocketOptions.Auto,
            };

            using var client = new SmtpClient { Timeout = 15000 };
            await client.ConnectAsync(request.Host, request.Port, secureOptions, cancellationToken);

            // Restrict to a specific SASL mechanism when the account pins one; Auto/OAuth2 keep MailKit's default.
            if (request.AuthMethod != SmtpAuthMethod.Auto)
            {
                var mechanism = request.AuthMethod switch
                {
                    SmtpAuthMethod.Login => "LOGIN",
                    SmtpAuthMethod.Plain => "PLAIN",
                    SmtpAuthMethod.CramMd5 => "CRAM-MD5",
                    _ => null,
                };

                if (mechanism is not null)
                {
                    client.AuthenticationMechanisms.Clear();
                    client.AuthenticationMechanisms.Add(mechanism);
                }
            }

            if (!string.IsNullOrEmpty(request.Username))
                await client.AuthenticateAsync(request.Username, request.Password ?? string.Empty, cancellationToken);

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            return new ConnectivityTestResult(true, $"Test email sent to {request.ToEmail} via {request.Host}:{request.Port}.", now);
        }
        catch (Exception ex)
        {
            return new ConnectivityTestResult(false, $"SMTP test failed: {ex.Message}", now);
        }
    }
}
