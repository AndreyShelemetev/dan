using System.Net;
using System.Text;

namespace PamyatRyadom.Api.Services.Notifications;

/// <summary>
/// Turns a template's plain paragraphs into the two bodies every notification email carries: HTML
/// and plain text. The signature and the optional call-to-action link are rendered here, once, so
/// every notification looks and reads the same way regardless of which event sent it.
/// </summary>
public static class NotificationBodyRenderer
{
    private const string Signature = "— «Память рядом»";

    public static string RenderHtml(IReadOnlyList<string> paragraphs, string? linkUrl, string? linkLabel)
    {
        var body = new StringBuilder();
        body.Append("""<div style="font-family:Arial,Helvetica,sans-serif;max-width:480px;margin:0 auto;color:#111827">""");

        foreach (var paragraph in paragraphs)
        {
            body.Append($"""<p style="margin:0 0 12px;font-size:15px">{WebUtility.HtmlEncode(paragraph)}</p>""");
        }

        if (linkUrl is { Length: > 0 })
        {
            var encodedUrl = WebUtility.HtmlEncode(linkUrl);
            var label = WebUtility.HtmlEncode(linkLabel ?? linkUrl);
            body.Append($"""
                <p style="margin:0 0 20px">
                  <a href="{encodedUrl}" style="display:inline-block;padding:10px 20px;background:#111827;color:#ffffff;text-decoration:none;border-radius:6px;font-size:14px">{label}</a>
                </p>
                """);
        }

        body.Append($"""<p style="color:#6b7280;font-size:13px;margin:16px 0 0">{WebUtility.HtmlEncode(Signature)}</p>""");
        body.Append("</div>");
        return body.ToString();
    }

    public static string RenderText(IReadOnlyList<string> paragraphs, string? linkUrl, string? linkLabel)
    {
        var lines = new List<string>(paragraphs);

        if (linkUrl is { Length: > 0 })
        {
            lines.Add($"{linkLabel ?? linkUrl}: {linkUrl}");
        }

        lines.Add(Signature);
        return string.Join("\n\n", lines);
    }
}
