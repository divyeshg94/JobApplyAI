namespace JobApplyAi.Api.Options;

public class ParsingOptions
{
    public const string SectionName = "Parsing";

    /// <summary>Profiles stuck in Parsing longer than this are marked Failed (worker died mid-parse).</summary>
    public int TimeoutMinutes { get; set; } = 10;

    public int MaxUploadBytes { get; set; } = 10 * 1024 * 1024;
}
