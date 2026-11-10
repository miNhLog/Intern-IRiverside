using System.Security.Claims;
using HeThongDatTiecCuoi_API.DTOs.Checklist;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController, Route("api/checklists")]
[Authorize(Roles = RoleNames.Coordinator + "," + RoleNames.HallManager)]
public sealed class ChecklistController(ChecklistService service) : ControllerBase
{
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private string Role => User.IsInRole(RoleNames.HallManager) ? RoleNames.HallManager : RoleNames.Coordinator;
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await service.GetAsync(UserId, Role, ct);
        return result.Succeeded ? Ok(result.Value) : StatusCode(result.StatusCode, new { message = result.Error });
    }
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var result = await service.IncompleteCountAsync(UserId, Role, ct);
        return result.Succeeded ? Ok(new { incompleteWorkCount = result.Value }) : StatusCode(result.StatusCode, new { message = result.Error });
    }
    [HttpPost("{bookingId:int}/{operation}")]
    public async Task<IActionResult> Execute(int bookingId, string operation, ChecklistCommand command, CancellationToken ct)
    {
        var result = await service.ExecuteAsync(UserId, Role, bookingId, operation, command, ct);
        return StatusCode(result.StatusCode, new { message = result.Succeeded ? result.Value : result.Error });
    }
}
