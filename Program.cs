using Fabric.API.Middleware;
using Fabric.Telemetry;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Weave;

var builder = WebApplication.CreateBuilder(args);

// Azure App Configuration
var settings = builder.Configuration;
var appConfigConnectionString = settings["AppConfigConnectionString"];
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
app.MapControllers();

app.Run();
