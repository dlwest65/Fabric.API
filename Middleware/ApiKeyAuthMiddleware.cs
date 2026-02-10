using Fabric.Contracts;

namespace Fabric.API.Middleware;

public class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;
    private bool _devBypassLogged;

    public ApiKeyAuthMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<ApiKeyAuthMiddleware> logger)
    {
        _next = next;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Splash page is accessible without auth in all environments
        if (context.Request.Path == "/")
        {
            await _next(context);
            return;
        }

        // Reach registration/validation endpoints handle their own auth
        if (context.Request.Path.StartsWithSegments("/api/reach"))
        {
            await _next(context);
            return;
        }

        // If API key header is present, always use standard auth (even in dev)
        if (context.Request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeader))
        {
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

            context.Items["TenantContext"] = new TenantContext
            {
                ClientId = clientId!,
                AllowedDatabases = allowedDatabases
            };

            await _next(context);
            return;
        }

        // Dev mode bypass: no API key header, Development environment
        if (_environment.IsDevelopment())
        {
            var clientId = _configuration["ClientId"] ?? "nxp";

            // Discover databases from Azure App Config (indexed array pattern)
            var databases = _configuration.GetSection("Databases").Get<List<DatabaseConfig>>();
            var allowedDatabases = databases?
                .Where(d => !string.IsNullOrEmpty(d.Name))
                .Select(d => d.Name)
                .ToArray();

            // Fallback to stubbed test key config
            if (allowedDatabases == null || allowedDatabases.Length == 0)
            {
                allowedDatabases = _configuration
                    .GetSection("ApiKeys:test-key-nxp:AllowedDatabases")
                    .Get<string[]>() ?? Array.Empty<string>();
            }

            if (!_devBypassLogged)
            {
                _logger.LogWarning(
                    "[DEV MODE] Auth bypassed. Client: {ClientId}, Databases: {Databases}",
                    clientId,
                    string.Join(", ", allowedDatabases));
                _devBypassLogged = true;
            }

            context.Items["TenantContext"] = new TenantContext
            {
                ClientId = clientId,
                AllowedDatabases = allowedDatabases
            };

            await _next(context);
            return;
        }

        // Production: no API key header = 401
        context.Response.StatusCode = 401;
        await context.Response.WriteAsJsonAsync(new { error = "Missing X-Api-Key header." });
    }
}
