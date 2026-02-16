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

// Imprimatur admin page — accessible without API key auth (exempted in middleware)
app.MapGet("/imprimatur", () =>
{
    var html = BuildImprimaturAdminHtml();
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
        sb.AppendLine("  <a href=\"#\" onclick=\"testEndpoint('/data/ProLaw/Professionals'); return false;\">/data/ProLaw/Professionals</a>");
        sb.AppendLine("  <a href=\"#\" onclick=\"testEndpoint('/data/ProLaw/Accounts'); return false;\">/data/ProLaw/Accounts</a>");
        sb.AppendLine("  <a href=\"#\" onclick=\"testEndpoint('/data/ProLaw/Components'); return false;\">/data/ProLaw/Components</a>");
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

static string BuildImprimaturAdminHtml()
{
    var sb = new StringBuilder();
    sb.AppendLine("<!DOCTYPE html>");
    sb.AppendLine("<html><head>");
    sb.AppendLine("<title>Imprimatur - API Key Management</title>");
    sb.AppendLine("<style>");
    sb.AppendLine("  body { font-family: 'Segoe UI', sans-serif; max-width: 1200px; margin: 40px auto; padding: 0 20px; color: #333; }");
    sb.AppendLine("  h1 { border-bottom: 2px solid #0078d4; padding-bottom: 8px; }");
    sb.AppendLine("  h2 { margin-top: 24px; }");
    sb.AppendLine("  .section { margin: 24px 0; padding: 16px; border: 1px solid #ddd; border-radius: 4px; background: #f9f9f9; }");
    sb.AppendLine("  .form-group { margin: 12px 0; }");
    sb.AppendLine("  label { display: block; margin-bottom: 4px; font-weight: 600; }");
    sb.AppendLine("  input, textarea, button { font-family: 'Segoe UI', sans-serif; font-size: 14px; padding: 8px; border: 1px solid #ccc; border-radius: 3px; }");
    sb.AppendLine("  input, textarea { width: 100%; box-sizing: border-box; }");
    sb.AppendLine("  textarea { resize: vertical; min-height: 60px; }");
    sb.AppendLine("  button { background: #0078d4; color: white; border: none; cursor: pointer; padding: 8px 16px; margin-right: 8px; }");
    sb.AppendLine("  button:hover { background: #005a9e; }");
    sb.AppendLine("  button.warning { background: #ca5010; }");
    sb.AppendLine("  button.warning:hover { background: #9b3e0e; }");
    sb.AppendLine("  button.danger { background: #d13438; }");
    sb.AppendLine("  button.danger:hover { background: #a72824; }");
    sb.AppendLine("  button:disabled { background: #ccc; cursor: not-allowed; }");
    sb.AppendLine("  .toolbar { margin: 12px 0; padding: 8px; background: #f0f0f0; border-radius: 3px; }");
    sb.AppendLine("  table { border-collapse: collapse; width: 100%; margin: 16px 0; }");
    sb.AppendLine("  th, td { text-align: left; padding: 8px 12px; border-bottom: 1px solid #ddd; }");
    sb.AppendLine("  th { background: #f5f5f5; }");
    sb.AppendLine("  th input[type=checkbox] { cursor: pointer; }");
    sb.AppendLine("  .row-active { background: #f0f9f0; }");
    sb.AppendLine("  .row-paused { background: #fff4ce; }");
    sb.AppendLine("  .row-revoked { background: #fde7e9; }");
    sb.AppendLine("  .status-badge { display: inline-flex; align-items: center; font-weight: 600; }");
    sb.AppendLine("  .status-dot { width: 8px; height: 8px; border-radius: 50%; margin-right: 6px; }");
    sb.AppendLine("  .status-active .status-dot { background: #107c10; }");
    sb.AppendLine("  .status-paused .status-dot { background: #ca5010; }");
    sb.AppendLine("  .status-revoked .status-dot { background: #d13438; }");
    sb.AppendLine("  code { background: #f0f0f0; padding: 2px 6px; border-radius: 3px; font-family: 'Cascadia Code', 'Consolas', monospace; }");
    sb.AppendLine("  .error { color: #d13438; margin: 8px 0; }");
    sb.AppendLine("  .banner-warning { color: #ca5010; background: #fff4ce; padding: 12px; border-left: 4px solid #ca5010; margin: 12px 0; }");
    sb.AppendLine("  .modal { display: none; position: fixed; z-index: 1000; left: 0; top: 0; width: 100%; height: 100%; background: rgba(0,0,0,0.5); }");
    sb.AppendLine("  .modal-content { background: white; margin: 10% auto; padding: 24px; width: 600px; border-radius: 4px; box-shadow: 0 4px 16px rgba(0,0,0,0.3); }");
    sb.AppendLine("  .modal-key { background: #1e1e1e; color: #d4d4d4; padding: 12px; border-radius: 3px; font-family: 'Cascadia Code', 'Consolas', monospace; word-break: break-all; margin: 12px 0; font-size: 16px; }");
    sb.AppendLine("  .hidden { display: none; }");
    sb.AppendLine("</style>");
    sb.AppendLine("</head><body>");

    sb.AppendLine("<h1>Imprimatur &mdash; API Key Management</h1>");
    sb.AppendLine("<p style=\"color: #107c10; background: #f0f9ff; padding: 12px; border-left: 4px solid #0078d4; margin: 16px 0;\">");
    sb.AppendLine("  <strong>Development Mode</strong> &mdash; Auth disabled. In production, this page will require @nextpro.law authentication.");
    sb.AppendLine("</p>");

    // Section 1: Load Keys
    sb.AppendLine("<div class=\"section\">");
    sb.AppendLine("  <h2>1. Load Keys for Tenant</h2>");
    sb.AppendLine("  <div class=\"form-group\">");
    sb.AppendLine("    <label for=\"tenant-id\">Tenant ID</label>");
    sb.AppendLine("    <input type=\"text\" id=\"tenant-id\" placeholder=\"e.g., nxp\">");
    sb.AppendLine("  </div>");
    sb.AppendLine("  <button onclick=\"loadKeys()\">Load Keys</button>");
    sb.AppendLine("  <div class=\"error hidden\" id=\"load-error\"></div>");
    sb.AppendLine("</div>");

    // Section 2: Create Key (DatabaseName removed)
    sb.AppendLine("<div class=\"section\">");
    sb.AppendLine("  <h2>2. Create New API Key</h2>");
    sb.AppendLine("  <div class=\"form-group\">");
    sb.AppendLine("    <label for=\"create-tenant\">Tenant ID</label>");
    sb.AppendLine("    <input type=\"text\" id=\"create-tenant\" placeholder=\"e.g., nxp\">");
    sb.AppendLine("  </div>");
    sb.AppendLine("  <div class=\"form-group\">");
    sb.AppendLine("    <label for=\"create-label\">Label</label>");
    sb.AppendLine("    <input type=\"text\" id=\"create-label\" placeholder=\"e.g., Production API Key\">");
    sb.AppendLine("  </div>");
    sb.AppendLine("  <div class=\"form-group\">");
    sb.AppendLine("    <label for=\"create-by\">Created By</label>");
    sb.AppendLine("    <input type=\"text\" id=\"create-by\" placeholder=\"Your name\">");
    sb.AppendLine("  </div>");
    sb.AppendLine("  <div class=\"form-group\">");
    sb.AppendLine("    <label for=\"create-notes\">Notes (optional)</label>");
    sb.AppendLine("    <textarea id=\"create-notes\" placeholder=\"Additional notes\"></textarea>");
    sb.AppendLine("  </div>");
    sb.AppendLine("  <button onclick=\"createKey()\">Create Key</button>");
    sb.AppendLine("  <div class=\"error hidden\" id=\"create-error\"></div>");
    sb.AppendLine("</div>");

    // Section 3: Keys Table (with multi-select and bulk actions)
    sb.AppendLine("<div class=\"section\">");
    sb.AppendLine("  <h2>3. API Keys</h2>");
    sb.AppendLine("  <div id=\"keys-container\" class=\"hidden\">");
    sb.AppendLine("    <div class=\"toolbar\">");
    sb.AppendLine("      <span id=\"selection-count\">0 selected</span>");
    sb.AppendLine("      <button id=\"btn-pause\" onclick=\"bulkPause()\" disabled>Pause</button>");
    sb.AppendLine("      <button id=\"btn-resume\" onclick=\"bulkResume()\" disabled>Resume</button>");
    sb.AppendLine("      <button id=\"btn-revoke\" class=\"danger\" onclick=\"bulkRevoke()\" disabled>Revoke</button>");
    sb.AppendLine("    </div>");
    sb.AppendLine("    <table id=\"keys-table\">");
    sb.AppendLine("      <thead>");
    sb.AppendLine("        <tr>");
    sb.AppendLine("          <th><input type=\"checkbox\" id=\"select-all\" onchange=\"toggleSelectAll()\"></th>");
    sb.AppendLine("          <th>Label</th>");
    sb.AppendLine("          <th>Created By</th>");
    sb.AppendLine("          <th>Created At</th>");
    sb.AppendLine("          <th>Last Used</th>");
    sb.AppendLine("          <th>Status</th>");
    sb.AppendLine("        </tr>");
    sb.AppendLine("      </thead>");
    sb.AppendLine("      <tbody id=\"keys-tbody\"></tbody>");
    sb.AppendLine("    </table>");
    sb.AppendLine("  </div>");
    sb.AppendLine("  <div id=\"no-keys\" class=\"hidden\">No keys found for this tenant.</div>");
    sb.AppendLine("</div>");

    // Modal for showing plaintext key
    sb.AppendLine("<div id=\"key-modal\" class=\"modal\">");
    sb.AppendLine("  <div class=\"modal-content\">");
    sb.AppendLine("    <h2>API Key Created</h2>");
    sb.AppendLine("    <div class=\"banner-warning\">");
    sb.AppendLine("      <strong>Warning:</strong> This is the only time you will see this key. Copy it now and store it securely.");
    sb.AppendLine("    </div>");
    sb.AppendLine("    <div class=\"modal-key\" id=\"modal-key-display\"></div>");
    sb.AppendLine("    <button onclick=\"copyKey()\">Copy to Clipboard</button>");
    sb.AppendLine("    <button onclick=\"closeModal()\">I've Copied It</button>");
    sb.AppendLine("  </div>");
    sb.AppendLine("</div>");

    // JavaScript
    sb.AppendLine("<script>");
    sb.AppendLine("let currentPlaintextKey = '';");
    sb.AppendLine("let allKeys = [];");
    sb.AppendLine("");
    sb.AppendLine("async function loadKeys() {");
    sb.AppendLine("  const tenantId = document.getElementById('tenant-id').value.trim();");
    sb.AppendLine("  if (!tenantId) {");
    sb.AppendLine("    document.getElementById('load-error').textContent = 'Tenant ID is required';");
    sb.AppendLine("    document.getElementById('load-error').classList.remove('hidden');");
    sb.AppendLine("    return;");
    sb.AppendLine("  }");
    sb.AppendLine("");
    sb.AppendLine("  const url = `/api/imprimatur/keys?tenantId=${encodeURIComponent(tenantId)}`;");
    sb.AppendLine("");
    sb.AppendLine("  try {");
    sb.AppendLine("    const response = await fetch(url);");
    sb.AppendLine("");
    sb.AppendLine("    if (!response.ok) {");
    sb.AppendLine("      const error = await response.json();");
    sb.AppendLine("      throw new Error(error.error || 'Failed to load keys');");
    sb.AppendLine("    }");
    sb.AppendLine("");
    sb.AppendLine("    allKeys = await response.json();");
    sb.AppendLine("    renderKeys();");
    sb.AppendLine("    document.getElementById('load-error').classList.add('hidden');");
    sb.AppendLine("  } catch (err) {");
    sb.AppendLine("    document.getElementById('load-error').textContent = err.message;");
    sb.AppendLine("    document.getElementById('load-error').classList.remove('hidden');");
    sb.AppendLine("  }");
    sb.AppendLine("}");
    sb.AppendLine("");
    sb.AppendLine("function renderKeys() {");
    sb.AppendLine("  const tbody = document.getElementById('keys-tbody');");
    sb.AppendLine("  tbody.innerHTML = '';");
    sb.AppendLine("");
    sb.AppendLine("  if (allKeys.length === 0) {");
    sb.AppendLine("    document.getElementById('keys-container').classList.add('hidden');");
    sb.AppendLine("    document.getElementById('no-keys').classList.remove('hidden');");
    sb.AppendLine("    return;");
    sb.AppendLine("  }");
    sb.AppendLine("");
    sb.AppendLine("  document.getElementById('keys-container').classList.remove('hidden');");
    sb.AppendLine("  document.getElementById('no-keys').classList.add('hidden');");
    sb.AppendLine("  document.getElementById('select-all').checked = false;");
    sb.AppendLine("");
    sb.AppendLine("  allKeys.forEach(key => {");
    sb.AppendLine("    const row = document.createElement('tr');");
    sb.AppendLine("    const statusText = key.status === 0 ? 'Active' : (key.status === 1 ? 'Paused' : 'Revoked');");
    sb.AppendLine("    const statusClass = statusText.toLowerCase();");
    sb.AppendLine("    row.className = `row-${statusClass}`;");
    sb.AppendLine("    const lastUsed = key.lastUsedAt ? new Date(key.lastUsedAt).toLocaleString() : 'Never';");
    sb.AppendLine("");
    sb.AppendLine("    row.innerHTML = `");
    sb.AppendLine("      <td><input type=\"checkbox\" class=\"key-checkbox\" data-key-id=\"${key.id}\" data-key-status=\"${statusText}\" onchange=\"updateToolbar()\"></td>");
    sb.AppendLine("      <td>${escapeHtml(key.label)}</td>");
    sb.AppendLine("      <td>${escapeHtml(key.createdBy)}</td>");
    sb.AppendLine("      <td>${new Date(key.createdAt).toLocaleString()}</td>");
    sb.AppendLine("      <td>${lastUsed}</td>");
    sb.AppendLine("      <td><span class=\"status-badge status-${statusClass}\"><span class=\"status-dot\"></span>${statusText}</span></td>");
    sb.AppendLine("    `;");
    sb.AppendLine("    tbody.appendChild(row);");
    sb.AppendLine("  });");
    sb.AppendLine("  updateToolbar();");
    sb.AppendLine("}");
    sb.AppendLine("");
    sb.AppendLine("function toggleSelectAll() {");
    sb.AppendLine("  const checked = document.getElementById('select-all').checked;");
    sb.AppendLine("  document.querySelectorAll('.key-checkbox').forEach(cb => cb.checked = checked);");
    sb.AppendLine("  updateToolbar();");
    sb.AppendLine("}");
    sb.AppendLine("");
    sb.AppendLine("function updateToolbar() {");
    sb.AppendLine("  const checkboxes = Array.from(document.querySelectorAll('.key-checkbox:checked'));");
    sb.AppendLine("  const count = checkboxes.length;");
    sb.AppendLine("  document.getElementById('selection-count').textContent = `${count} selected`;");
    sb.AppendLine("");
    sb.AppendLine("  const statuses = checkboxes.map(cb => cb.dataset.keyStatus);");
    sb.AppendLine("  const hasActive = statuses.includes('Active');");
    sb.AppendLine("  const hasPaused = statuses.includes('Paused');");
    sb.AppendLine("  const hasRevoked = statuses.includes('Revoked');");
    sb.AppendLine("");
    sb.AppendLine("  document.getElementById('btn-pause').disabled = !hasActive || count === 0;");
    sb.AppendLine("  document.getElementById('btn-resume').disabled = !hasPaused || count === 0;");
    sb.AppendLine("  document.getElementById('btn-revoke').disabled = (!hasActive && !hasPaused) || count === 0;");
    sb.AppendLine("}");
    sb.AppendLine("");
    sb.AppendLine("async function createKey() {");
    sb.AppendLine("  const tenantId = document.getElementById('create-tenant').value.trim();");
    sb.AppendLine("  const label = document.getElementById('create-label').value.trim();");
    sb.AppendLine("  const createdBy = document.getElementById('create-by').value.trim();");
    sb.AppendLine("  const notes = document.getElementById('create-notes').value.trim();");
    sb.AppendLine("");
    sb.AppendLine("  if (!tenantId || !label || !createdBy) {");
    sb.AppendLine("    document.getElementById('create-error').textContent = 'Tenant ID, Label, and Created By are required';");
    sb.AppendLine("    document.getElementById('create-error').classList.remove('hidden');");
    sb.AppendLine("    return;");
    sb.AppendLine("  }");
    sb.AppendLine("");
    sb.AppendLine("  try {");
    sb.AppendLine("    const response = await fetch('/api/imprimatur/keys', {");
    sb.AppendLine("      method: 'POST',");
    sb.AppendLine("      headers: { 'Content-Type': 'application/json' },");
    sb.AppendLine("      body: JSON.stringify({ tenantId, label, createdBy, notes: notes || null })");
    sb.AppendLine("    });");
    sb.AppendLine("");
    sb.AppendLine("    if (!response.ok) {");
    sb.AppendLine("      const error = await response.json();");
    sb.AppendLine("      throw new Error(error.error || 'Failed to create key');");
    sb.AppendLine("    }");
    sb.AppendLine("");
    sb.AppendLine("    const result = await response.json();");
    sb.AppendLine("    currentPlaintextKey = result.apiKey;");
    sb.AppendLine("    document.getElementById('modal-key-display').textContent = currentPlaintextKey;");
    sb.AppendLine("    document.getElementById('key-modal').style.display = 'block';");
    sb.AppendLine("");
    sb.AppendLine("    // Clear form");
    sb.AppendLine("    document.getElementById('create-label').value = '';");
    sb.AppendLine("    document.getElementById('create-notes').value = '';");
    sb.AppendLine("    document.getElementById('create-error').classList.add('hidden');");
    sb.AppendLine("  } catch (err) {");
    sb.AppendLine("    document.getElementById('create-error').textContent = err.message;");
    sb.AppendLine("    document.getElementById('create-error').classList.remove('hidden');");
    sb.AppendLine("  }");
    sb.AppendLine("}");
    sb.AppendLine("");
    sb.AppendLine("async function bulkPause() {");
    sb.AppendLine("  await bulkOperation('pause', 'PUT', 'paused');");
    sb.AppendLine("}");
    sb.AppendLine("");
    sb.AppendLine("async function bulkResume() {");
    sb.AppendLine("  await bulkOperation('resume', 'PUT', 'resumed');");
    sb.AppendLine("}");
    sb.AppendLine("");
    sb.AppendLine("async function bulkRevoke() {");
    sb.AppendLine("  if (!confirm('Revoke is permanent and cannot be undone. Continue?')) return;");
    sb.AppendLine("  await bulkOperation('revoke', 'DELETE', 'revoked');");
    sb.AppendLine("}");
    sb.AppendLine("");
    sb.AppendLine("async function bulkOperation(action, method, pastTense) {");
    sb.AppendLine("  const checkboxes = Array.from(document.querySelectorAll('.key-checkbox:checked'));");
    sb.AppendLine("  const keyIds = checkboxes.map(cb => cb.dataset.keyId);");
    sb.AppendLine("  if (keyIds.length === 0) return;");
    sb.AppendLine("");
    sb.AppendLine("  const actor = document.getElementById('create-by').value.trim() || 'admin';");
    sb.AppendLine("");
    sb.AppendLine("  try {");
    sb.AppendLine("    const response = await fetch(`/api/imprimatur/keys/${action}`, {");
    sb.AppendLine("      method: method,");
    sb.AppendLine("      headers: { 'Content-Type': 'application/json' },");
    sb.AppendLine("      body: JSON.stringify({ keyIds, actor })");
    sb.AppendLine("    });");
    sb.AppendLine("");
    sb.AppendLine("    if (!response.ok) {");
    sb.AppendLine("      const error = await response.json();");
    sb.AppendLine("      throw new Error(error.error || `Failed to ${action} keys`);");
    sb.AppendLine("    }");
    sb.AppendLine("");
    sb.AppendLine("    const result = await response.json();");
    sb.AppendLine("    alert(`Successfully ${pastTense} ${result[pastTense]} of ${result.total} keys`);");
    sb.AppendLine("    await loadKeys();");
    sb.AppendLine("  } catch (err) {");
    sb.AppendLine("    alert('Error: ' + err.message);");
    sb.AppendLine("  }");
    sb.AppendLine("}");
    sb.AppendLine("");
    sb.AppendLine("function copyKey() {");
    sb.AppendLine("  navigator.clipboard.writeText(currentPlaintextKey).then(() => {");
    sb.AppendLine("    alert('API key copied to clipboard!');");
    sb.AppendLine("  }).catch(err => {");
    sb.AppendLine("    alert('Failed to copy: ' + err);");
    sb.AppendLine("  });");
    sb.AppendLine("}");
    sb.AppendLine("");
    sb.AppendLine("function closeModal() {");
    sb.AppendLine("  document.getElementById('key-modal').style.display = 'none';");
    sb.AppendLine("  currentPlaintextKey = '';");
    sb.AppendLine("  // Reload keys to show newly created key in table");
    sb.AppendLine("  const tenantId = document.getElementById('tenant-id').value.trim();");
    sb.AppendLine("  if (tenantId) loadKeys();");
    sb.AppendLine("}");
    sb.AppendLine("");
    sb.AppendLine("function escapeHtml(text) {");
    sb.AppendLine("  const div = document.createElement('div');");
    sb.AppendLine("  div.textContent = text;");
    sb.AppendLine("  return div.innerHTML;");
    sb.AppendLine("}");
    sb.AppendLine("</script>");

    sb.AppendLine("</body></html>");
    return sb.ToString();
}
