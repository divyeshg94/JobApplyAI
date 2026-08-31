using JobApplyAi.Domain.Abstractions;
using JobApplyAi.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace JobApplyAi.Infrastructure.Documents;

/// <summary>
/// Pure layout — takes already-tailored content (see IApplicationDocumentGenerator) and lays it
/// out as PDF. No AI calls here; keeping content generation and rendering separate means either
/// can change independently (e.g. swapping the PDF library later touches only this file).
/// </summary>
public static class QuestPdfDocumentRenderer
{
    public static byte[] RenderResume(CandidateProfile profile, TailoredDocuments tailored)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text(profile.FullName ?? "").FontSize(20).Bold();
                    var contactParts = new[] { profile.Email, profile.Phone, profile.LocationText, profile.LinkedInUrl, profile.PortfolioUrl }
                        .Where(s => !string.IsNullOrWhiteSpace(s));
                    col.Item().Text(string.Join("  |  ", contactParts)).FontSize(9).FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingTop(15).Column(col =>
                {
                    col.Spacing(12);

                    if (!string.IsNullOrWhiteSpace(tailored.ResumeSummary))
                    {
                        col.Item().Column(section =>
                        {
                            section.Item().Text("Summary").FontSize(13).Bold();
                            section.Item().Text(tailored.ResumeSummary);
                        });
                    }

                    col.Item().Column(section =>
                    {
                        section.Item().Text("Experience").FontSize(13).Bold();
                        foreach (var work in tailored.WorkExperiences)
                        {
                            section.Item().PaddingTop(6).Column(item =>
                            {
                                item.Item().Text($"{work.Title} — {work.Company}").Bold();
                                foreach (var bullet in work.Bullets)
                                {
                                    item.Item().Text($"• {bullet}");
                                }
                            });
                        }
                    });

                    if (profile.Educations.Count > 0)
                    {
                        col.Item().Column(section =>
                        {
                            section.Item().Text("Education").FontSize(13).Bold();
                            foreach (var education in profile.Educations)
                            {
                                var line = string.Join(", ", new[] { education.Degree, education.FieldOfStudy }
                                    .Where(s => !string.IsNullOrWhiteSpace(s)));
                                section.Item().Text($"{education.Institution}{(line.Length > 0 ? " — " + line : "")}");
                            }
                        });
                    }

                    if (tailored.SkillsOrdered.Count > 0)
                    {
                        col.Item().Column(section =>
                        {
                            section.Item().Text("Skills").FontSize(13).Bold();
                            section.Item().Text(string.Join(", ", tailored.SkillsOrdered));
                        });
                    }
                });
            });
        }).GeneratePdf();
    }

    public static byte[] RenderCoverLetter(CandidateProfile profile, JobPosting jobPosting, string coverLetterText)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(50);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Text(profile.FullName ?? "").FontSize(16).Bold();
                    var contactParts = new[] { profile.Email, profile.Phone }.Where(s => !string.IsNullOrWhiteSpace(s));
                    col.Item().Text(string.Join("  |  ", contactParts)).FontSize(9).FontColor(Colors.Grey.Darken2);
                    col.Item().PaddingTop(10).Text(DateOnly.FromDateTime(DateTime.UtcNow).ToString("MMMM d, yyyy"));
                    col.Item().PaddingTop(4).Text($"Re: {jobPosting.Title} at {jobPosting.CompanyName}").Bold();
                });

                page.Content().PaddingTop(15).Column(col =>
                {
                    col.Spacing(10);
                    foreach (var paragraph in coverLetterText.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
                    {
                        col.Item().Text(paragraph.Trim());
                    }
                });
            });
        }).GeneratePdf();
    }
}
