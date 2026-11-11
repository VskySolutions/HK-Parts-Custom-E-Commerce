using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using HKPartsEcommerce.Infrastructure.Credentials;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Credentials;

/// <summary>
/// A secret encrypted under another deployment's key ring (DB copied between machines, key folder wiped by a
/// redeploy) must read as "not set" instead of throwing "The key ... was not found in the key ring".
/// </summary>
public class CredentialProtectorTests
{
    private static CredentialProtector NewProtector()
        // Each ephemeral provider holds its own random key ring — two of them model two machines.
        => new(new EphemeralDataProtectionProvider(), NullLogger<CredentialProtector>.Instance);

    [Fact]
    public void Value_from_this_key_ring_round_trips()
    {
        var protector = NewProtector();
        Assert.Equal("sk_test_123", protector.TryUnprotect(protector.Protect("sk_test_123")));
    }

    [Fact]
    public void Value_from_another_key_ring_reads_as_null()
    {
        var otherMachine = NewProtector();
        var thisMachine = NewProtector();

        Assert.Null(thisMachine.TryUnprotect(otherMachine.Protect("sk_test_123")));
    }

    [Fact]
    public void Garbage_reads_as_null()
        => Assert.Null(NewProtector().TryUnprotect("not-a-protected-payload"));
}
