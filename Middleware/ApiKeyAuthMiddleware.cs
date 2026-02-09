using Fabric.Contracts;

namespace Fabric.API.Middleware;

public class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public ApiKeyAuthMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeader))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Missing X-Api-Key header." });
            return;
        }

        var apiKey = apiKeyHeader.ToString();
        var keySection = _configuration.GetSection($"ApiKeys:{apiKey}");

        if (!keySection.Exists())
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid API key." });
            return;
        }

        var clientId = keySection["ClientId"];
        var allowedDatabases = keySection.GetSection("AllowedDatabases").Get<string[]>() ?? Array.Empty<string>();

        var tenant = new TenantContext
        {
            ClientId = clientId!,
            AllowedDatabases = allowedDatabases
        };

        context.Items["TenantContext"] = tenant;
        await _next(context);
    }
}
