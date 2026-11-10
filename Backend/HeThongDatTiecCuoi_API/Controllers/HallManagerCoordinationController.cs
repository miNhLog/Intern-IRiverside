using System.Security.Claims;
using HeThongDatTiecCuoi_API.DTOs.EventCoordination;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/hall-manager/coordination-assignments")]
[Authorize(Roles = RoleNames.HallManager)]
public sealed class HallManagerCoordinationController : ControllerBase
{
    private readonly ICoordinationAssignmentService _service;
    public HallManagerCoordinationController(ICoordinationAssignmentService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] HallManagerCoordinationQuery query,
        CancellationToken cancellationToken) => ToActionResult(
        await _service.GetHallManagerListAsync(CurrentUserId(), query, cancellationToken));

    [HttpGet("{bookingId:int}")]
    public async Task<IActionResult> GetDetail(int bookingId, CancellationToken cancellationToken) => ToActionResult(
        await _service.GetHallManagerDetailAsync(CurrentUserId(), bookingId, cancellationToken));

    [HttpGet("{bookingId:int}/coordinators")]
    public async Task<IActionResult> GetCoordinators(int bookingId, CancellationToken cancellationToken) => ToActionResult(
        await _service.GetCoordinatorAvailabilityAsync(CurrentUserId(), bookingId, cancellationToken));

    [HttpPost("{bookingId:int}")]
    public async Task<IActionResult> Assign(int bookingId, [FromBody] CreateCoordinationAssignmentRequest request,
        CancellationToken cancellationToken) => ToActionResult(
        await _service.AssignAsync(CurrentUserId(), bookingId, request, cancellationToken));

    [HttpPut("{bookingId:int}")]
    public async Task<IActionResult> Change(int bookingId, [FromBody] ChangeCoordinationAssignmentRequest request,
        CancellationToken cancellationToken) => ToActionResult(
        await _service.ChangeAsync(CurrentUserId(), bookingId, request, cancellationToken));

    [HttpPatch("{bookingId:int}/cancel")]
    public async Task<IActionResult> Cancel(int bookingId, [FromBody] CancelCoordinationAssignmentRequest request,
        CancellationToken cancellationToken) => ToActionResult(
        await _service.CancelAsync(CurrentUserId(), bookingId, request, cancellationToken));

    private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
        ? userId : throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ.");

    private ObjectResult ToActionResult<T>(ServiceResult<T> result) =>
        StatusCode(result.StatusCode, result.Succeeded ? result.Value : new { message = result.Error });
}
