using System.Security.Claims;
using HeThongDatTiecCuoi_API.DTOs.ScheduleChange;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Manager)]
[Route("api/manager/schedule-changes")]
public sealed class ManagerScheduleChangeController : ControllerBase
{
    private readonly IScheduleChangeService _service;
    public ManagerScheduleChangeController(IScheduleChangeService service) => _service = service;

    [HttpGet]
    public Task<IActionResult> List([FromQuery] string? status) => ExecuteAsync(() =>
        _service.GetManagerRequestsAsync(status, HttpContext.RequestAborted));

    [HttpGet("{id:int}")]
    public Task<IActionResult> Detail(int id) => ExecuteAsync(() =>
        _service.GetInternalRequestAsync(id, HttpContext.RequestAborted));

    [HttpPost("{id:int}/quote")]
    public Task<IActionResult> Quote(int id, [FromBody] QuoteScheduleChangeRequest request) => ExecuteAsync(() =>
        _service.QuoteAndConditionallyApproveAsync(UserId(), id, request, HttpContext.RequestAborted));

    [HttpPost("{id:int}/reject")]
    public Task<IActionResult> Reject(int id, [FromBody] RejectScheduleChangeRequest request) => ExecuteAsync(() =>
        _service.RejectAsync(UserId(), id, request, HttpContext.RequestAborted));

    [HttpPost("{id:int}/reconcile")]
    public Task<IActionResult> Reconcile(int id, [FromBody] ReconciliationScheduleChangeRequest request) => ExecuteAsync(() =>
        _service.ReconcileAsync(UserId(), id, request, HttpContext.RequestAborted));

    [HttpGet("{id:int}/financials")]
    public Task<IActionResult> Financials(int id) => ExecuteAsync(() =>
        _service.GetFinancialDetailAsync(id, HttpContext.RequestAborted));

    [HttpPost("{id:int}/refunds/{refundId:int}/confirm")]
    public Task<IActionResult> ConfirmRefund(int id, int refundId, [FromBody] ManualRefundConfirmationRequest request) => ExecuteAsync(() =>
        _service.ConfirmManualRefundAsync(UserId(), id, refundId, request, HttpContext.RequestAborted));

    private int UserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<IActionResult> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try { return Ok(new { success = true, data = await operation() }); }
        catch (ScheduleChangeException ex) { return StatusCode(ex.StatusCode, new { success = false, message = ex.Message }); }
        catch (DbUpdateException) { return Conflict(new { success = false, message = "Dữ liệu vừa thay đổi hoặc phát sinh xung đột lịch." }); }
    }
}

