namespace JobApplyAi.Domain.Abstractions;

/// <summary>
/// Containers are private; reads happen only via short-lived download URLs (SAS) minted here —
/// file bytes are never proxied through the API.
/// </summary>
public interface IBlobStorageService
{
    /// <summary>Uploads and returns the blob's URL (not readable without a SAS).</summary>
    Task<string> UploadAsync(string container, string blobPath, Stream content, string contentType, CancellationToken ct);

    Task<Uri> GetDownloadUrlAsync(string container, string blobPath, TimeSpan validity, CancellationToken ct);
}

public static class BlobContainers
{
    public const string Resumes = "resumes";
    public const string Generated = "generated";
}
