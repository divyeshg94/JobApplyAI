namespace JobApplyAi.Api.Security;

/// <summary>
/// v1 stand-in for real auth: every /api/* request must carry the shared secret in X-Api-Key.
/// Blazor pages and /health are exempt (same-origin UI and App Service probes respectively).
/// Fails closed: an unconfigured key rejects all /api traffic rather than allowing it.
/// When real auth lands, this middleware is replaced wholesale — no schema impact.
/// </summary>
public class ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<ApiKeyMiddleware> logger)
{
    public const string HeaderName = "X-Api-Key";
    public const string ConfigKey = "ApiKey";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        var configuredKey = configuration[ConfigKey];
        if (string.IsNullOrEmpty(configuredKey))
        {
            logger.LogError("ApiKey is not configured; rejecting API request.");
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var provided)
            || !CryptographicEquals(provided.ToString(), configuredKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(context);
    }

    private static bool CryptographicEquals(string a, string b)
        => System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(a),
            System.Text.Encoding.UTF8.GetBytes(b));
}
