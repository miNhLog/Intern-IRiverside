using System.Security.Claims;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/hall-manager/dashboard")]
[Authorize(Roles = RoleNames.HallManager)]
public sealed class HallManagerDashboardController : ControllerBase
{
    private readonly IHallManagerDashboardService _service;

    public HallManagerDashboardController(IHallManagerDashboardService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        ToActionResult(await _service.GetAsync(CurrentUserId(), cancellationToken));

    [HttpGet("schedule")]
    public async Task<IActionResult> GetSchedule([FromQuery] DateTime date, CancellationToken cancellationToken)
    {
        if (date == default) return BadRequest(new { message = "Ngày xem lịch không hợp lệ." });
        return ToActionResult(await _service.GetScheduleAsync(CurrentUserId(), date, cancellationToken));
    }

    private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
        ? userId : throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ.");

    private IActionResult ToActionResult<T>(ServiceResult<T> result) => result.Succeeded
        ? StatusCode(result.StatusCode, result.Value)
        : StatusCode(result.StatusCode, new { message = result.Error });
}
