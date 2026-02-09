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
}
