using System.Security.Claims;
using HeThongDatTiecCuoi_API.DTOs.ScheduleChange;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.HallManager)]
[Route("api/hall-manager/schedule-changes")]
public sealed class HallManagerScheduleChangeController : ControllerBase
{
    private readonly IScheduleChangeService _service;
    public HallManagerScheduleChangeController(IScheduleChangeService service) => _service = service;

    [HttpGet("create-options")]
    public Task<IActionResult> CreateOptions() => ExecuteAsync(() =>
        _service.GetHallManagerCreateOptionsAsync(UserId(), HttpContext.RequestAborted));

    [HttpGet("availability")]
    public Task<IActionResult> Availability([FromQuery] int bookingId, [FromQuery] DateTime date, [FromQuery] string shift) =>
        ExecuteAsync(() => _service.GetHallManagerAvailabilityAsync(UserId(), bookingId, date, shift, HttpContext.RequestAborted));

    [HttpGet]
    public Task<IActionResult> List([FromQuery] string? status) => ExecuteAsync(() =>
        _service.GetHallManagerRequestsAsync(UserId(), status, HttpContext.RequestAborted));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        try
        {
            var scoped = await _service.GetHallManagerRequestsAsync(UserId(), null, HttpContext.RequestAborted);
            var item = scoped.SingleOrDefault(x => x.ScheduleChangeId == id);
            return item is null ? NotFound(new { success = false, message = "Không tìm thấy yêu cầu trong phạm vi phụ trách." })
                : Ok(new { success = true, data = item });
        }
        catch (ScheduleChangeException ex) { return StatusCode(ex.StatusCode, new { success = false, message = ex.Message }); }
    }

    [HttpPost]
    public Task<IActionResult> Create([FromBody] HallManagerCreateScheduleChangeRequest request) => ExecuteAsync(() =>
        _service.CreateHallManagerRequestAsync(UserId(), request, HttpContext.RequestAborted), StatusCodes.Status201Created);

    [HttpPost("{id:int}/review")]
    public Task<IActionResult> Review(int id, [FromBody] ReviewScheduleChangeRequest request) => ExecuteAsync(() =>
        _service.ReviewAsync(UserId(), id, request, HttpContext.RequestAborted));

    private int UserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<IActionResult> ExecuteAsync<T>(Func<Task<T>> operation, int successStatus = StatusCodes.Status200OK)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try { return StatusCode(successStatus, new { success = true, data = await operation() }); }
        catch (ScheduleChangeException ex) { return StatusCode(ex.StatusCode, new { success = false, message = ex.Message }); }
        catch (DbUpdateException) { return Conflict(new { success = false, message = "Dữ liệu vừa thay đổi hoặc phát sinh xung đột lịch." }); }
    }
}

