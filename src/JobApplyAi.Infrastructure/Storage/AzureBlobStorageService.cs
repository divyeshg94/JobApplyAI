using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using JobApplyAi.Domain.Abstractions;

namespace JobApplyAi.Infrastructure.Storage;

public class AzureBlobStorageService(BlobServiceClient serviceClient) : IBlobStorageService
{
    public async Task<string> UploadAsync(
        string container, string blobPath, Stream content, string contentType, CancellationToken ct)
    {
        var containerClient = serviceClient.GetBlobContainerClient(container);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);

        var blobClient = containerClient.GetBlobClient(blobPath);
        await blobClient.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
        }, ct);

        return blobClient.Uri.ToString();
    }

    public async Task<Uri> GetDownloadUrlAsync(
        string container, string blobPath, TimeSpan validity, CancellationToken ct)
    {
        var blobClient = serviceClient.GetBlobContainerClient(container).GetBlobClient(blobPath);

        if (blobClient.CanGenerateSasUri)
        {
            // Connection-string / account-key auth.
            return blobClient.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.Add(validity));
        }

        // Token auth (DefaultAzureCredential) — needs a user delegation key; the identity must
        // hold a Storage Blob Data role on the account.
        var delegationKey = await serviceClient.GetUserDelegationKeyAsync(
            DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.Add(validity), ct);
        var sasBuilder = new BlobSasBuilder(BlobSasPermissions.Read, DateTimeOffset.UtcNow.Add(validity))
        {
            BlobContainerName = container,
            BlobName = blobPath,
        };
        var sas = sasBuilder.ToSasQueryParameters(delegationKey.Value, serviceClient.AccountName);
        return new UriBuilder(blobClient.Uri) { Query = sas.ToString() }.Uri;
    }
}
