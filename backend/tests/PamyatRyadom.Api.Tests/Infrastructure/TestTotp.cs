using OtpNet;

namespace PamyatRyadom.Api.Tests.Infrastructure;

/// <summary>
/// Generates the TOTP codes the tests need from the Base32 secret the API just handed out, using the
/// same parameters <c>MfaService</c> verifies with (SHA-1, 30-second step, 6 digits). Computing them
/// beats hardcoding: a hardcoded code would only ever be right for one second in 1970.
/// </summary>
public static class TestTotp
{
    private static readonly VerificationWindow Window = new(previous: 1, future: 1);

    public static string Now(string base32Secret) =>
        Build(base32Secret).ComputeTotp();

    /// <summary>A six-digit code the server will reject. Candidates are screened against the same
    /// ±1-step verification window the API uses, so this can never be accidentally valid.</summary>
    public static string Wrong(string base32Secret)
    {
        var totp = Build(base32Secret);

        for (var candidate = 0; candidate < 1000; candidate++)
        {
            var code = candidate.ToString("D6");
            if (!totp.VerifyTotp(code, out _, Window))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Could not find a code outside the verification window.");
    }

    private static Totp Build(string base32Secret) => new(Base32Encoding.ToBytes(base32Secret));
}
