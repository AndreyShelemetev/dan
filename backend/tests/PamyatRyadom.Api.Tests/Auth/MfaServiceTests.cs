using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using OtpNet;
using PamyatRyadom.Api.Services.Auth;

namespace PamyatRyadom.Api.Tests.Auth;

/// <summary>The TOTP primitives on their own. Pure logic with no database and no HTTP in it, so a
/// round trip through the API would only make the failure harder to read.</summary>
public sealed class MfaServiceTests
{
    private static MfaService CreateService() =>
        new(new EphemeralDataProtectionProvider(), NullLogger<MfaService>.Instance);

    [Fact]
    public void Generated_secrets_are_160_bit_base32_and_never_repeat()
    {
        var service = CreateService();

        var secrets = Enumerable.Range(0, 20).Select(_ => service.GenerateSecret()).ToArray();

        Assert.Equal(secrets.Length, secrets.Distinct().Count());
        Assert.All(secrets, secret =>
        {
            Assert.Matches("^[A-Z2-7]+$", secret);
            Assert.Equal(20, Base32Encoding.ToBytes(secret).Length);
        });
    }

    [Fact]
    public void The_provisioning_uri_carries_the_parameters_an_authenticator_needs()
    {
        var service = CreateService();
        var secret = service.GenerateSecret();

        var uri = service.GetProvisioningUri(secret, "user@example.com");

        Assert.StartsWith($"otpauth://totp/{Uri.EscapeDataString(MfaService.DefaultIssuer)}:user%40example.com?", uri);
        Assert.Contains($"secret={secret}", uri);
        Assert.Contains($"issuer={Uri.EscapeDataString(MfaService.DefaultIssuer)}", uri);
        Assert.Contains("algorithm=SHA1", uri);
        Assert.Contains("digits=6", uri);
        Assert.Contains("period=30", uri);
    }

    [Fact]
    public void A_custom_issuer_replaces_the_default_in_both_label_and_query()
    {
        var service = CreateService();
        var secret = service.GenerateSecret();

        var uri = service.GetProvisioningUri(secret, "user@example.com", "Pamyat Test");

        Assert.Contains("Pamyat%20Test:user%40example.com", uri);
        Assert.Contains("issuer=Pamyat%20Test", uri);
        Assert.DoesNotContain(Uri.EscapeDataString(MfaService.DefaultIssuer), uri);
    }

    [Fact]
    public void The_current_code_and_its_neighbours_verify()
    {
        var service = CreateService();
        var secret = service.GenerateSecret();
        var totp = new Totp(Base32Encoding.ToBytes(secret));

        Assert.True(service.VerifyCode(secret, totp.ComputeTotp()));

        // One step of clock drift each way is tolerated on purpose — phones are not atomic clocks.
        Assert.True(service.VerifyCode(secret, totp.ComputeTotp(DateTime.UtcNow.AddSeconds(-30))));
        Assert.True(service.VerifyCode(secret, totp.ComputeTotp(DateTime.UtcNow.AddSeconds(30))));
    }

    [Fact]
    public void A_code_from_outside_the_window_is_refused()
    {
        var service = CreateService();
        var secret = service.GenerateSecret();
        var totp = new Totp(Base32Encoding.ToBytes(secret));

        Assert.False(service.VerifyCode(secret, totp.ComputeTotp(DateTime.UtcNow.AddSeconds(-90))));
        Assert.False(service.VerifyCode(secret, totp.ComputeTotp(DateTime.UtcNow.AddSeconds(90))));
    }

    [Fact]
    public void A_code_generated_from_a_different_secret_is_refused()
    {
        var service = CreateService();
        var mine = service.GenerateSecret();
        var theirs = service.GenerateSecret();

        var theirCode = new Totp(Base32Encoding.ToBytes(theirs)).ComputeTotp();

        Assert.False(service.VerifyCode(mine, theirCode));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abcdef")]
    [InlineData("12345")]
    public void A_missing_or_malformed_code_is_refused(string? code)
    {
        var service = CreateService();

        Assert.False(service.VerifyCode(service.GenerateSecret(), code));
    }

    [Fact]
    public void An_unusable_stored_secret_never_verifies_anything()
    {
        var service = CreateService();

        Assert.False(service.VerifyCode(string.Empty, "123456"));
        Assert.False(service.VerifyCode("not-base32-!!", "123456"));
    }

    [Fact]
    public void Recovery_codes_come_with_one_hash_each_and_no_plaintext()
    {
        var service = CreateService();

        var set = service.GenerateRecoveryCodes();

        Assert.Equal(10, set.Codes.Count);
        Assert.Equal(set.Codes.Count, set.Hashes.Count);
        Assert.Equal(set.Codes.Count, set.Codes.Distinct().Count());
        Assert.Equal(set.Hashes.Count, set.Hashes.Distinct().Count());

        Assert.All(set.Codes, code => Assert.Matches("^[A-HJ-NP-Z2-9]{5}-[A-HJ-NP-Z2-9]{5}$", code));
        Assert.All(set.Hashes, hash => Assert.StartsWith("pbkdf2-sha256:", hash));

        foreach (var code in set.Codes)
        {
            Assert.All(set.Hashes, hash => Assert.DoesNotContain(code, hash));
        }
    }

    [Fact]
    public void The_recovery_code_count_is_configurable()
    {
        var service = CreateService();

        var set = service.GenerateRecoveryCodes(3);

        Assert.Equal(3, set.Codes.Count);
        Assert.Equal(3, set.Hashes.Count);
    }

    [Fact]
    public void A_protected_secret_round_trips_and_is_not_stored_in_the_clear()
    {
        var service = CreateService();
        var secret = service.GenerateSecret();

        var protectedSecret = service.ProtectSecret(secret);

        Assert.NotEqual(secret, protectedSecret);
        Assert.DoesNotContain(secret, protectedSecret);
        Assert.Equal(secret, service.UnprotectSecret(protectedSecret));
    }

    [Fact]
    public void An_undecryptable_payload_yields_null_rather_than_throwing()
    {
        var service = CreateService();

        Assert.Null(service.UnprotectSecret("not-a-protected-payload"));

        // A payload from a different key ring — what a lost/rotated key ring looks like in practice.
        var stranger = CreateService();
        Assert.Null(service.UnprotectSecret(stranger.ProtectSecret("JBSWY3DPEHPK3PXP")));
    }
}
