using System.Text.Json;
using Fabric.API.Extensions;
using Fabric.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Fabric.API.Controllers;

[ApiController]
[Route("data")]
public class DataController : ControllerBase
{
    private readonly IWeaveService _weaveService;

    public DataController(IWeaveService weaveService)
    {
        _weaveService = weaveService;
    }

    [HttpGet("{database}/{tableName}")]
    public async Task<IActionResult> GetRows(string database, string tableName)
    {
        var tenant = HttpContext.GetTenantContext();
        try
        {
            var rows = await _weaveService.GetRowsAsync(tenant, database, tableName);
            return Ok(rows);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("{database}/{tableName}/{id}")]
    public async Task<IActionResult> GetRowById(string database, string tableName, string id)
    {
        var tenant = HttpContext.GetTenantContext();
        try
        {
            var row = await _weaveService.GetRowByIdAsync(tenant, database, tableName, id);
            if (row == null)
                return NotFound();
            return Ok(row);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPut("{database}/{tableName}")]
    public async Task<IActionResult> UpdateRows(
        string database, string tableName, [FromBody] JsonElement[] rows)
    {
        var tenant = HttpContext.GetTenantContext();
        try
        {
            var rowDicts = rows.Select(ConvertJsonElement).ToList();
            var result = await _weaveService.UpdateRowsAsync(tenant, database, tableName, rowDicts);

            if (result.HasConflicts)
                return Conflict(new { results = result.Results });

            return Ok(new { results = result.Results });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static Dictionary<string, object?> ConvertJsonElement(JsonElement element)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in element.EnumerateObject())
        {
            dict[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number when prop.Value.TryGetInt64(out var l) => l,
                JsonValueKind.Number => prop.Value.GetDouble(),
                _ => prop.Value.ToString()
            };
        }
        return dict;
    }
}
