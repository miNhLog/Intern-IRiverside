using System.Security.Claims;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/coordinator/assigned-parties")]
[Authorize(Roles = RoleNames.Coordinator)]
public sealed class CoordinatorAssignmentsController : ControllerBase
{
    private readonly ICoordinationAssignmentService _service;
    public CoordinatorAssignmentsController(ICoordinationAssignmentService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var result = await _service.GetCoordinatorPartiesAsync(userId, cancellationToken);
        return StatusCode(result.StatusCode, result.Succeeded ? result.Value : new { message = result.Error });
    }
}
