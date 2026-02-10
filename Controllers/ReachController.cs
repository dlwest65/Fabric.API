using Fabric.Imprimatur.Services;
using Microsoft.AspNetCore.Mvc;

namespace Fabric.API.Controllers;

[ApiController]
[Route("api/reach")]
public class ReachController : ControllerBase
{
    private readonly IImprimaturService _imprimaturService;
    private readonly IConfiguration _configuration;

    public ReachController(IImprimaturService imprimaturService, IConfiguration configuration)
    {
        _imprimaturService = imprimaturService;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var installerKey = Request.Headers["X-Installer-Key"].ToString();
        var expectedKey = _configuration["Imprimatur:InstallerKey"];

        if (string.IsNullOrEmpty(installerKey) ||
            string.IsNullOrEmpty(expectedKey) ||
            installerKey != expectedKey)
        {
            return Unauthorized(new { error = "Invalid or missing installer key." });
        }

        if (string.IsNullOrWhiteSpace(request.TenantId) ||
            string.IsNullOrWhiteSpace(request.Secret) ||
            string.IsNullOrWhiteSpace(request.RegisteredBy))
        {
            return BadRequest(new { error = "TenantId, Secret, and RegisteredBy are required." });
        }

        var instanceId = await _imprimaturService.RegisterReachInstanceAsync(
            request.TenantId,
            request.Secret,
            request.RegisteredBy,
            request.MachineName,
            request.Notes);

        return StatusCode(201, new
        {
            instanceId,
            tenantId = request.TenantId,
            registeredAt = DateTime.UtcNow
        });
    }

    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] ValidateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId) ||
            string.IsNullOrWhiteSpace(request.Secret))
        {
            return BadRequest(new { error = "TenantId and Secret are required." });
        }

        var instance = await _imprimaturService.ValidateReachInstanceAsync(
            request.TenantId,
            request.Secret);

        if (instance == null)
            return Unauthorized(new { error = "Invalid credentials." });

        return Ok(new
        {
            instanceId = instance.Id,
            tenantId = instance.TenantId,
            isActive = instance.IsActive
        });
    }
}

public class RegisterRequest
{
    public string TenantId { get; set; } = "";
    public string Secret { get; set; } = "";
    public string RegisteredBy { get; set; } = "";
    public string? MachineName { get; set; }
    public string? Notes { get; set; }
}

public class ValidateRequest
{
    public string TenantId { get; set; } = "";
    public string Secret { get; set; } = "";
}
