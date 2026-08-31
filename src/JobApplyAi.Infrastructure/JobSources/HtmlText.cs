using System.Net;
using System.Text.RegularExpressions;

namespace JobApplyAi.Infrastructure.JobSources;

public static partial class HtmlText
{
    [GeneratedRegex("<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"[ \t]{2,}")]
    private static partial Regex WhitespaceRegex();

    /// <summary>
    /// Good-enough HTML→text for embedding/LLM input. Not a sanitizer — output is never rendered.
    /// Greenhouse entity-escapes the HTML itself, so entities inside text survive the first decode
    /// (&amp;amp;amp; → &amp;amp;) — hence decode → strip tags → decode again.
    /// </summary>
    public static string ToPlainText(string html)
    {
        var decoded = WebUtility.HtmlDecode(html);
        var stripped = TagRegex().Replace(decoded, " ");
        var textDecoded = WebUtility.HtmlDecode(stripped);
        return WhitespaceRegex().Replace(textDecoded, " ").Trim();
    }
}
