using System.Text;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace JobApplyAi.Infrastructure.Ai;

/// <summary>
/// Local text extraction pre-pass (PDF via PdfPig, DOCX via OpenXml) so the LLM receives plain
/// text — deterministic and model-agnostic vs. sending raw file bytes to the model.
/// </summary>
public static class ResumeTextExtractor
{
    public static string Extract(byte[] content, string fileName, string contentType)
    {
        var isPdf = contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
        var isDocx = contentType.Contains("wordprocessingml", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase);

        if (isPdf)
        {
            return ExtractPdf(content);
        }

        if (isDocx)
        {
            return ExtractDocx(content);
        }

        throw new NotSupportedException($"Unsupported resume format: {contentType} ({fileName}). Upload PDF or DOCX.");
    }

    private static string ExtractPdf(byte[] content)
    {
        using var document = PdfDocument.Open(content);
        var builder = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            builder.AppendLine(page.Text);
        }

        return builder.ToString();
    }

    private static string ExtractDocx(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var document = WordprocessingDocument.Open(stream, isEditable: false);
        return document.MainDocumentPart?.Document?.Body?.InnerText ?? string.Empty;
    }
}
