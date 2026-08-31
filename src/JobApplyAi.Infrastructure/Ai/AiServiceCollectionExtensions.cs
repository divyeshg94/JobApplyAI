using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.Identity;
using Azure.Storage.Blobs;
using JobApplyAi.Domain.Abstractions;
using JobApplyAi.Infrastructure.Email;
using JobApplyAi.Infrastructure.Storage;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;

namespace JobApplyAi.Infrastructure.Ai;

#pragma warning disable OPENAI001 // BearerTokenPolicy is experimental — this is Microsoft's documented v1-API auth pattern.

public static class AiServiceCollectionExtensions
{
    /// <summary>
    /// Foundry chat + embedding clients via the v1 Azure OpenAI API (`/openai/v1/`, no dated
    /// api-version). Azure.AI.OpenAI's AzureOpenAIClient only speaks the old dated api-versions,
    /// which newer Foundry resources reject outright ("API version not supported") — the plain
    /// OpenAI client pointed at the v1 path is Microsoft's current guidance instead.
    /// Auth: Foundry:ApiKey when set, else DefaultAzureCredential (az login locally, Managed
    /// Identity on App Service) via the ai.azure.com token scope.
    /// </summary>
    public static IServiceCollection AddFoundryAi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(_ =>
        {
            var endpoint = configuration["Foundry:Endpoint"]
                ?? throw new InvalidOperationException("Foundry:Endpoint is not configured.");

            // The v1 OpenAI-compatible surface lives at the resource root (<host>/openai/v1/),
            // not under a Foundry project path — but the portal hands out project-scoped URLs
            // (.../api/projects/<name>) on some pages, which route chat completions fine but 404
            // on embeddings. Always rebuild from the authority so either shape works.
            var resourceRoot = new Uri(endpoint).GetLeftPart(UriPartial.Authority);
            var v1Endpoint = new Uri(resourceRoot + "/openai/v1/");
            var apiKey = configuration["Foundry:ApiKey"];

            if (!string.IsNullOrEmpty(apiKey))
            {
                return new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = v1Endpoint });
            }

            var tokenPolicy = new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default");
            return new OpenAIClient(authenticationPolicy: tokenPolicy, new OpenAIClientOptions { Endpoint = v1Endpoint });
        });

        services.AddSingleton<IChatClient>(sp =>
        {
            var deployment = configuration["Foundry:ChatDeployment"]
                ?? throw new InvalidOperationException("Foundry:ChatDeployment is not configured.");
            return sp.GetRequiredService<OpenAIClient>().GetChatClient(deployment).AsIChatClient();
        });

        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var deployment = configuration["Foundry:EmbeddingDeployment"]
                ?? throw new InvalidOperationException("Foundry:EmbeddingDeployment is not configured.");
            return sp.GetRequiredService<OpenAIClient>()
                .GetEmbeddingClient(deployment)
                .AsIEmbeddingGenerator();
        });

        services.AddSingleton<IResumeParser, FoundryResumeParser>();
        services.AddSingleton<IMatchScorer, FoundryMatchScorer>();
        services.AddSingleton<IJobPostingClassifier, FoundryJobPostingClassifier>();
        services.AddSingleton<IApplicationDocumentGenerator, FoundryApplicationDocumentGenerator>();
        return services;
    }

    /// <summary>Blob via Blob:ConnectionString when set, else Blob:AccountUrl + DefaultAzureCredential.</summary>
    public static IServiceCollection AddBlobStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(_ =>
        {
            var connectionString = configuration["Blob:ConnectionString"];
            if (!string.IsNullOrEmpty(connectionString))
            {
                return new BlobServiceClient(connectionString);
            }

            var accountUrl = configuration["Blob:AccountUrl"]
                ?? throw new InvalidOperationException("Configure Blob:ConnectionString or Blob:AccountUrl.");
            return new BlobServiceClient(new Uri(accountUrl), new DefaultAzureCredential());
        });

        services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();
        return services;
    }

    public static IServiceCollection AddEmailNotifications(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.AddSingleton<IEmailNotifier, MailKitEmailNotifier>();
        return services;
    }
}
