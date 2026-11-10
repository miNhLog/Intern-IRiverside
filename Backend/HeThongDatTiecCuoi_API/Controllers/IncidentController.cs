using System.Security.Claims;
using HeThongDatTiecCuoi_API.DTOs.Incident;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/incidents")]
[Authorize(Roles = RoleNames.Coordinator + "," + RoleNames.HallManager + "," + RoleNames.Manager)]
public sealed class IncidentController : ControllerBase
{
    private readonly IIncidentService _incidents;
    public IncidentController(IIncidentService incidents) => _incidents = incidents;

    [HttpGet]
    public async Task<IActionResult> GetList(string? search, string? status, string? severity, int? hallId,
        DateTime? fromDate, DateTime? toDate,
        int page = 1, int pageSize = 10, CancellationToken cancellationToken = default) =>
        ToActionResult(await _incidents.GetListAsync(CurrentUserId(), CurrentRole(), search, status, severity,
            hallId, fromDate, toDate, page, pageSize, cancellationToken));

    [HttpGet("booking-options")]
    public async Task<IActionResult> GetBookingOptions(CancellationToken cancellationToken) =>
        ToActionResult(await _incidents.GetBookingOptionsAsync(CurrentUserId(), CurrentRole(), cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetail(int id, CancellationToken cancellationToken) =>
        ToActionResult(await _incidents.GetDetailAsync(CurrentUserId(), CurrentRole(), id, cancellationToken));

    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> GetHistory(int id, CancellationToken cancellationToken) =>
        ToActionResult(await _incidents.GetHistoryAsync(CurrentUserId(), CurrentRole(), id, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(CreateIncidentRequest request, CancellationToken cancellationToken)
    {
        var result = await _incidents.CreateAsync(CurrentUserId(), CurrentRole(), request, cancellationToken);
        return result.Succeeded
            ? CreatedAtAction(nameof(GetDetail), new { id = result.Value!.IncidentId }, result.Value)
            : StatusCode(result.StatusCode, new { message = result.Error });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateIncidentRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await _incidents.UpdateAsync(CurrentUserId(), CurrentRole(), id, request, cancellationToken));

    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = RoleNames.HallManager + "," + RoleNames.Manager)]
    public async Task<IActionResult> UpdateStatus(int id, UpdateIncidentStatusRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await _incidents.UpdateStatusAsync(CurrentUserId(), CurrentRole(), id, request, cancellationToken));

    [HttpPatch("{id:int}/resolution")]
    [Authorize(Roles = RoleNames.HallManager + "," + RoleNames.Manager)]
    public async Task<IActionResult> UpdateResolution(int id, UpdateIncidentResolutionRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await _incidents.UpdateResolutionAsync(CurrentUserId(), CurrentRole(), id, request, cancellationToken));

    private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
        ? userId : throw new UnauthorizedAccessException("Không xác định được tài khoản.");

    private string CurrentRole() => User.FindFirstValue(ClaimTypes.Role)
        ?? throw new UnauthorizedAccessException("Không xác định được vai trò.");

    private IActionResult ToActionResult<T>(ServiceResult<T> result) => result.Succeeded
        ? StatusCode(result.StatusCode, result.Value)
        : StatusCode(result.StatusCode, new { message = result.Error });
}
