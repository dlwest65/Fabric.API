using Fabric.Imprimatur.Services;
using Microsoft.AspNetCore.Mvc;

namespace Fabric.API.Controllers;

[ApiController]
[Route("api/imprimatur")]
public class ImprimaturController : ControllerBase
{
    private readonly IApiKeyService _apiKeyService;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public ImprimaturController(IApiKeyService apiKeyService, IConfiguration configuration, IWebHostEnvironment environment)
    {
        _apiKeyService = apiKeyService;
        _configuration = configuration;
        _environment = environment;
    }

    private IActionResult? ValidateInstallerKey()
    {
        // In development, skip installer key validation
        // TODO: Replace with MS365/OAuth identity (@nextpro.law users only)
        if (_environment.IsDevelopment())
            return null;

        var installerKey = Request.Headers["X-Installer-Key"].ToString();
        var expectedKey = _configuration["Imprimatur:InstallerKey"];

        if (string.IsNullOrEmpty(installerKey) ||
            string.IsNullOrEmpty(expectedKey) ||
            installerKey != expectedKey)
        {
            return Unauthorized(new { error = "Invalid or missing installer key." });
        }

        return null;
    }

    [HttpGet("keys")]
    public async Task<IActionResult> GetKeys([FromQuery] string tenantId)
    {
        var authError = ValidateInstallerKey();
        if (authError != null) return authError;

        if (string.IsNullOrWhiteSpace(tenantId))
            return BadRequest(new { error = "tenantId query parameter is required." });

        var keys = await _apiKeyService.GetKeysForTenantAsync(tenantId);
        return Ok(keys);
    }

    [HttpPost("keys")]
    public async Task<IActionResult> CreateKey([FromBody] CreateKeyRequest request)
    {
        var authError = ValidateInstallerKey();
        if (authError != null) return authError;

        if (string.IsNullOrWhiteSpace(request.TenantId) ||
            string.IsNullOrWhiteSpace(request.Label) ||
            string.IsNullOrWhiteSpace(request.CreatedBy))
        {
            return BadRequest(new { error = "TenantId, Label, and CreatedBy are required." });
        }

        var result = await _apiKeyService.CreateKeyAsync(
            request.TenantId,
            request.Label,
            request.CreatedBy,
            request.Notes);

        return StatusCode(201, new
        {
            keyId = result.KeyId,
            apiKey = result.PlaintextKey,
            tenantId = result.TenantId,
            warning = "This is the only time you will see this key. Store it securely."
        });
    }

    [HttpPut("keys/{id}/pause")]
    public async Task<IActionResult> PauseKey(Guid id, [FromQuery] string pausedBy)
    {
        var authError = ValidateInstallerKey();
        if (authError != null) return authError;

        if (string.IsNullOrWhiteSpace(pausedBy))
            return BadRequest(new { error = "pausedBy query parameter is required." });

        var success = await _apiKeyService.PauseKeyAsync(id, pausedBy);

        if (!success)
            return NotFound(new { error = "Key not found or not in Active state." });

        return Ok(new { paused = true, keyId = id });
    }

    [HttpPut("keys/{id}/resume")]
    public async Task<IActionResult> ResumeKey(Guid id)
    {
        var authError = ValidateInstallerKey();
        if (authError != null) return authError;

        var success = await _apiKeyService.ResumeKeyAsync(id);

        if (!success)
            return NotFound(new { error = "Key not found or not in Paused state." });

        return Ok(new { resumed = true, keyId = id });
    }

    [HttpDelete("keys/{id}")]
    public async Task<IActionResult> RevokeKey(Guid id, [FromQuery] string revokedBy)
    {
        var authError = ValidateInstallerKey();
        if (authError != null) return authError;

        if (string.IsNullOrWhiteSpace(revokedBy))
            return BadRequest(new { error = "revokedBy query parameter is required." });

        var success = await _apiKeyService.RevokeKeyAsync(id, revokedBy);

        if (!success)
            return NotFound(new { error = "Key not found or already revoked." });

        return Ok(new { revoked = true, keyId = id });
    }

    [HttpPut("keys/pause")]
    public async Task<IActionResult> PauseKeys([FromBody] BulkKeyOperationRequest request)
    {
        var authError = ValidateInstallerKey();
        if (authError != null) return authError;

        if (request.KeyIds == null || request.KeyIds.Count == 0)
            return BadRequest(new { error = "KeyIds array is required and cannot be empty." });

        if (string.IsNullOrWhiteSpace(request.Actor))
            return BadRequest(new { error = "Actor is required." });

        var affected = await _apiKeyService.PauseKeysAsync(request.KeyIds, request.Actor);

        return Ok(new { paused = affected, total = request.KeyIds.Count });
    }

    [HttpPut("keys/resume")]
    public async Task<IActionResult> ResumeKeys([FromBody] BulkKeyOperationRequest request)
    {
        var authError = ValidateInstallerKey();
        if (authError != null) return authError;

        if (request.KeyIds == null || request.KeyIds.Count == 0)
            return BadRequest(new { error = "KeyIds array is required and cannot be empty." });

        var affected = await _apiKeyService.ResumeKeysAsync(request.KeyIds);

        return Ok(new { resumed = affected, total = request.KeyIds.Count });
    }

    [HttpDelete("keys/revoke")]
    public async Task<IActionResult> RevokeKeys([FromBody] BulkKeyOperationRequest request)
    {
        var authError = ValidateInstallerKey();
        if (authError != null) return authError;

        if (request.KeyIds == null || request.KeyIds.Count == 0)
            return BadRequest(new { error = "KeyIds array is required and cannot be empty." });

        if (string.IsNullOrWhiteSpace(request.Actor))
            return BadRequest(new { error = "Actor is required." });

        var affected = await _apiKeyService.RevokeKeysAsync(request.KeyIds, request.Actor);

        return Ok(new { revoked = affected, total = request.KeyIds.Count });
    }
}

public class CreateKeyRequest
{
    public string TenantId { get; set; } = "";
    public string Label { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public string? Notes { get; set; }
}

public class BulkKeyOperationRequest
{
    public List<Guid> KeyIds { get; set; } = new();
    public string Actor { get; set; } = "";
}
