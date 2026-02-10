using System.Text;
using Fabric;
using Fabric.API.Middleware;
using Fabric.Imprimatur;
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

// Imprimatur services (Reach instance registration/validation)
builder.Services.AddImprimaturServices();

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
    sb.AppendLine("  a { color: #0078d4; cursor: pointer; }");
    sb.AppendLine("  .links { margin: 16px 0; }");
    sb.AppendLine("  .links a { display: block; padding: 4px 0; font-family: monospace; }");
    sb.AppendLine("  json-viewer { --background-color: #1e1e1e; --color: #d4d4d4; --font-family: 'Cascadia Code', 'Consolas', 'Courier New', monospace; --font-size: 0.875rem; --string-color: #ce9178; --number-color: #b5cea8; --boolean-color: #569cd6; --null-color: #569cd6; --property-color: #9cdcfe; max-height: 600px; overflow: auto; display: block; padding: 12px; border-radius: 0 0 6px 6px; }");
    sb.AppendLine("  #results-panel { margin-top: 24px; border: 1px solid #333; border-radius: 6px; }");
    sb.AppendLine("  #results-header { display: flex; align-items: center; gap: 12px; padding: 8px 12px; background: #252526; border-radius: 6px 6px 0 0; font-family: 'Cascadia Code', 'Consolas', monospace; font-size: 0.8rem; color: #ccc; }");
    sb.AppendLine("  #results-url { flex: 1; color: #569cd6; }");
    sb.AppendLine("  #results-close { background: none; border: none; color: #888; cursor: pointer; font-size: 1rem; padding: 0 4px; }");
    sb.AppendLine("  #results-close:hover { color: #fff; }");
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
    sb.AppendLine("<tr><td>POST</td><td><code>/api/reach/register</code></td><td>Register a Reach instance (requires X-Installer-Key)</td></tr>");
    sb.AppendLine("<tr><td>POST</td><td><code>/api/reach/validate</code></td><td>Validate a Reach instance</td></tr>");
    sb.AppendLine("</table>");

    if (isDev && databases.Length > 0)
    {
        sb.AppendLine("<h2>Try It</h2>");
        sb.AppendLine("<div class=\"links\">");
        foreach (var db in databases)
        {
            sb.AppendLine($"  <a href=\"#\" onclick=\"testEndpoint('/data/{db}/Matters'); return false;\">/data/{db}/Matters</a>");
        }
        sb.AppendLine("</div>");

        sb.AppendLine("<div id=\"results-panel\" style=\"display: none;\">");
        sb.AppendLine("  <div id=\"results-header\">");
        sb.AppendLine("    <span id=\"results-url\"></span>");
        sb.AppendLine("    <span id=\"results-status\"></span>");
        sb.AppendLine("    <span id=\"results-time\"></span>");
        sb.AppendLine("    <button id=\"results-close\" onclick=\"closeResults()\">&#10005;</button>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <json-viewer id=\"results\"></json-viewer>");
        sb.AppendLine("</div>");
    }

    sb.AppendLine("<script src=\"https://unpkg.com/@alenaksu/json-viewer@2.1.0/dist/json-viewer.bundle.js\"></script>");
    sb.AppendLine("<script>");
    sb.AppendLine("async function testEndpoint(url) {");
    sb.AppendLine("  var panel = document.getElementById('results-panel');");
    sb.AppendLine("  var viewer = document.getElementById('results');");
    sb.AppendLine("  var urlDisplay = document.getElementById('results-url');");
    sb.AppendLine("  var statusDisplay = document.getElementById('results-status');");
    sb.AppendLine("  var timeDisplay = document.getElementById('results-time');");
    sb.AppendLine("  panel.style.display = 'block';");
    sb.AppendLine("  urlDisplay.textContent = url;");
    sb.AppendLine("  statusDisplay.textContent = '...';");
    sb.AppendLine("  statusDisplay.style.color = '#ccc';");
    sb.AppendLine("  timeDisplay.textContent = '';");
    sb.AppendLine("  var start = performance.now();");
    sb.AppendLine("  try {");
    sb.AppendLine("    var response = await fetch(url);");
    sb.AppendLine("    var elapsed = Math.round(performance.now() - start);");
    sb.AppendLine("    var data = await response.json();");
    sb.AppendLine("    statusDisplay.textContent = response.status + ' ' + response.statusText;");
    sb.AppendLine("    statusDisplay.style.color = response.ok ? '#4caf50' : '#f44336';");
    sb.AppendLine("    timeDisplay.textContent = elapsed + 'ms';");
    sb.AppendLine("    viewer.data = data;");
    sb.AppendLine("    if (JSON.stringify(data).length < 50000) { viewer.expandAll(); } else { viewer.expand('*'); }");
    sb.AppendLine("  } catch (err) {");
    sb.AppendLine("    statusDisplay.textContent = 'Error';");
    sb.AppendLine("    statusDisplay.style.color = '#f44336';");
    sb.AppendLine("    timeDisplay.textContent = '';");
    sb.AppendLine("    viewer.data = { error: err.message };");
    sb.AppendLine("  }");
    sb.AppendLine("}");
    sb.AppendLine("function closeResults() { document.getElementById('results-panel').style.display = 'none'; }");
    sb.AppendLine("</script>");

    sb.AppendLine("</body></html>");
    return sb.ToString();
}
