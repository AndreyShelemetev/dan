namespace PamyatRyadom.Api.Services.Media;

/// <summary>
/// Decides what a file actually is, from its leading bytes.
///
/// The declared Content-Type is attacker-controlled and means nothing on its own: a request may
/// claim <c>image/jpeg</c> and carry an HTML page or a polyglot that a browser will happily
/// execute. Reading the magic bytes is the cheap half of SEC-011; the expensive half (an
/// antivirus pass) is still outstanding — see the note in MediaService.
/// </summary>
public static class ImageSignature
{
    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";
    public const string WebP = "image/webp";
    public const string Heic = "image/heic";

    /// <summary>Formats a client is allowed to send. HEIC is here because it is what an iPhone
    /// produces by default, and telling a grieving relative their photo is "unsupported" is not
    /// an acceptable outcome.</summary>
    public static readonly IReadOnlyCollection<string> AllowedUploadTypes = new[] { Jpeg, Png, WebP, Heic };

    /// <summary>The real type of <paramref name="header"/>, or null when it is not an image this
    /// service accepts.</summary>
    public static string? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return Jpeg;
        }

        if (header.Length >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
        {
            return Png;
        }

        // RIFF....WEBP
        if (header.Length >= 12 &&
            header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
        {
            return WebP;
        }

        // ISO-BMFF: ....ftyp<brand>. HEIC brands vary by device (heic, heix, hevc, mif1).
        if (header.Length >= 12 &&
            header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70)
        {
            var brand = System.Text.Encoding.ASCII.GetString(header.Slice(8, 4));
            if (brand is "heic" or "heix" or "hevc" or "hevx" or "mif1" or "msf1")
            {
                return Heic;
            }
        }

        return null;
    }
}
