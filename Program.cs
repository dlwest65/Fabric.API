using System.Text;
using Fabric;
using Fabric.API.Middleware;
using Fabric.Telemetry;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Weave;

var builder = WebApplication.CreateBuilder(args);

// Azure App Configuration
var settings = builder.Configuration;
var appConfigConnectionString = settings["AppConfig:ConnectionString"];
var clientId = settings["ClientId"] ?? "nxp";

builder.Configuration.AddAzureAppConfiguration(options =>
{
    options.Connect(appConfigConnectionString)
           .Select(KeyFilter.Any)
           .Select(KeyFilter.Any, clientId);
});

// Telemetry
var aiConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
if (!string.IsNullOrEmpty(aiConnectionString))
    builder.Services.AddSingleton<ITelemetrySink>(new AppInsightsTelemetrySink(aiConnectionString, "Fabric.API"));

// Weave services (ReachClient, WeaveService, ServiceBusListener)
builder.Services.AddWeaveServices();

builder.Services.AddControllers();

var app = builder.Build();

app.UseMiddleware<ApiKeyAuthMiddleware>();

// Splash page — accessible without auth in all environments
app.MapGet("/", (IConfiguration config, IWebHostEnvironment env) =>
{
    var isDev = env.IsDevelopment();
    var currentClientId = config["ClientId"] ?? "nxp";

    // Discover databases from Azure App Config
    var databases = config.GetSection("Databases").Get<List<DatabaseConfig>>();
    var dbNames = databases?
        .Where(d => !string.IsNullOrEmpty(d.Name))
        .Select(d => d.Name)
        .ToArray() ?? Array.Empty<string>();

    if (dbNames.Length == 0)
    {
        dbNames = config.GetSection("ApiKeys:test-key-nxp:AllowedDatabases")
            .Get<string[]>() ?? Array.Empty<string>();
    }

    var html = BuildSplashHtml(env.EnvironmentName, isDev, currentClientId, dbNames);
    return Results.Content(html, "text/html");
});

app.MapControllers();

app.Run();

// --- Splash page HTML ---
static string BuildSplashHtml(string environmentName, bool isDev, string clientId, string[] databases)
{
    var sb = new StringBuilder();
    sb.AppendLine("<!DOCTYPE html>");
    sb.AppendLine("<html><head>");
    sb.AppendLine("<title>Fabric API</title>");
    sb.AppendLine("<style>");
    sb.AppendLine("  body { font-family: 'Segoe UI', sans-serif; max-width: 800px; margin: 40px auto; padding: 0 20px; color: #333; }");
    sb.AppendLine("  h1 { border-bottom: 2px solid #0078d4; padding-bottom: 8px; }");
    sb.AppendLine("  .env { color: #0078d4; }");
    sb.AppendLine("  .dev-info { background: #fff3cd; border: 1px solid #ffc107; padding: 12px; border-radius: 4px; margin: 16px 0; }");
    sb.AppendLine("  table { border-collapse: collapse; width: 100%; margin: 16px 0; }");
    sb.AppendLine("  th, td { text-align: left; padding: 8px 12px; border-bottom: 1px solid #ddd; }");
    sb.AppendLine("  th { background: #f5f5f5; }");
    sb.AppendLine("  code { background: #f0f0f0; padding: 2px 6px; border-radius: 3px; }");
    sb.AppendLine("  a { color: #0078d4; }");
    sb.AppendLine("  .links { margin: 16px 0; }");
    sb.AppendLine("  .links a { display: block; padding: 4px 0; font-family: monospace; }");
    sb.AppendLine("</style>");
    sb.AppendLine("</head><body>");

    sb.AppendLine($"<h1>Fabric API &mdash; <span class=\"env\">{environmentName}</span></h1>");

    if (isDev)
    {
        sb.AppendLine("<div class=\"dev-info\">");
        sb.AppendLine($"  <strong>Dev Mode</strong> &mdash; Auth bypassed. Client: <code>{clientId}</code>, Databases: <code>{string.Join(", ", databases)}</code>");
        sb.AppendLine("</div>");
    }

    sb.AppendLine("<h2>Routes</h2>");
    sb.AppendLine("<table>");
    sb.AppendLine("<tr><th>Method</th><th>Route</th><th>Description</th></tr>");
    sb.AppendLine("<tr><td>GET</td><td><code>/</code></td><td>This page</td></tr>");
    sb.AppendLine("<tr><td>GET</td><td><code>/data/{database}/{tableName}</code></td><td>List all rows</td></tr>");
    sb.AppendLine("<tr><td>GET</td><td><code>/data/{database}/{tableName}/{id}</code></td><td>Get row by ID</td></tr>");
    sb.AppendLine("<tr><td>GET</td><td><code>/entity/{database}/{entityName}</code></td><td>Not yet implemented (501)</td></tr>");
    sb.AppendLine("<tr><td>GET</td><td><code>/entity/{database}/{entityName}/{id}</code></td><td>Not yet implemented (501)</td></tr>");
    sb.AppendLine("</table>");

    if (isDev && databases.Length > 0)
    {
        sb.AppendLine("<h2>Try It</h2>");
        sb.AppendLine("<div class=\"links\">");
        foreach (var db in databases)
        {
            sb.AppendLine($"  <a href=\"/data/{db}/Matters\">/data/{db}/Matters</a>");
        }
        sb.AppendLine("</div>");
    }

    sb.AppendLine("</body></html>");
    return sb.ToString();
}
