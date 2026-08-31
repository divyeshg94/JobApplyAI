namespace JobApplyAi.Api.Options;

public class MatchingOptions
{
    public const string SectionName = "Matching";

    /// <summary>How many vector-prefiltered candidates get LLM-rescored per tick.</summary>
    public int TopN { get; set; } = 25;

    public int EmbeddingBatchSize { get; set; } = 16;

    /// <summary>Truncate before embedding/scoring — keeps requests well under model input limits.</summary>
    public int MaxDescriptionChars { get; set; } = 6000;

    /// <summary>PendingReview matches at or above this LlmScore get emailed, then flip to Notified.</summary>
    public double NotifyThreshold { get; set; } = 70;
}
