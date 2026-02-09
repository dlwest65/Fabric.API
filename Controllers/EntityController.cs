using Microsoft.AspNetCore.Mvc;

namespace Fabric.API.Controllers;

[ApiController]
[Route("entity")]
public class EntityController : ControllerBase
{
    [HttpGet("{database}/{entityName}")]
    public IActionResult Get(string database, string entityName)
    {
        return StatusCode(501, new { error = "Entity endpoints are not yet implemented." });
    }

    [HttpGet("{database}/{entityName}/{id}")]
    public IActionResult GetById(string database, string entityName, string id)
    {
        return StatusCode(501, new { error = "Entity endpoints are not yet implemented." });
    }
}
