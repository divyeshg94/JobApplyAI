using System.Threading.Channels;
using System.Threading.RateLimiting;
using JobApplyAi.Api.BackgroundServices;
using JobApplyAi.Api.Components;
using JobApplyAi.Api.Endpoints;
using JobApplyAi.Api.Options;
using JobApplyAi.Api.Security;
using JobApplyAi.Api.Services;
using JobApplyAi.Infrastructure.Ai;
using JobApplyAi.Infrastructure.Data;
using JobApplyAi.Infrastructure.JobSources;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Factory pattern for Blazor Server (components outlive a request scope); endpoints get a
// scoped AppDbContext forwarded from the same factory.
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseAzureSql(builder.Configuration.GetConnectionString("AzureSql"), sql =>
        sql.MigrationsHistoryTable(AppDbContext.MigrationsHistoryTable, AppDbContext.Schema)));
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

builder.Services.AddJobSourceClients(builder.Configuration);
builder.Services.Configure<PollingOptions>(builder.Configuration.GetSection(PollingOptions.SectionName));
builder.Services.AddHostedService<JobPollingBackgroundService>();

builder.Services.AddFoundryAi(builder.Configuration);
builder.Services.AddBlobStorage(builder.Configuration);
builder.Services.AddEmailNotifications(builder.Configuration);
builder.Services.Configure<ParsingOptions>(builder.Configuration.GetSection(ParsingOptions.SectionName));
builder.Services.Configure<MatchingOptions>(builder.Configuration.GetSection(MatchingOptions.SectionName));
builder.Services.AddScoped<MatchingPipelineService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<ApplicationGenerationService>();
builder.Services.AddSingleton(Channel.CreateUnbounded<ResumeParseJob>(
    new UnboundedChannelOptions { SingleReader = true }));
builder.Services.AddHostedService<ResumeParsingWorker>();

// CORS: only the extension origin — the Blazor UI is same-origin and needs no entry.
const string extensionCorsPolicy = "Extension";
builder.Services.AddCors(options => options.AddPolicy(extensionCorsPolicy, policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .WithHeaders("Content-Type", ApiKeyMiddleware.HeaderName)
    .WithMethods("GET", "POST", "PUT", "DELETE")));

// Defense-in-depth for a leaked API key — not user-facing throttling.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        context.Request.Path.StartsWithSegments("/api")
            ? RateLimitPartition.GetFixedWindowLimiter("api", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
            })
            : RateLimitPartition.GetNoLimiter("ui"));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseCors(extensionCorsPolicy);
app.UseRateLimiter();
app.UseMiddleware<ApiKeyMiddleware>();

app.UseAntiforgery();

// Exempt from the API-key middleware — App Service health probes hit this anonymously.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapSubscriptionEndpoints();
app.MapProfileEndpoints();
app.MapApplicationEndpoints();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
