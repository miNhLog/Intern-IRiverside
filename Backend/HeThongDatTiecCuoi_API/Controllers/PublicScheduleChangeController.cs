using HeThongDatTiecCuoi_API.DTOs.ScheduleChange;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/public/schedule-changes")]
public sealed class PublicScheduleChangeController : ControllerBase
{
    private readonly IScheduleChangeService _service;
    public PublicScheduleChangeController(IScheduleChangeService service) => _service = service;

    [HttpPost("eligibility")]
    public Task<IActionResult> Eligibility([FromBody] ScheduleChangeLookupRequest request) =>
        ExecuteAsync(() => _service.GetEligibilityAsync(request, HttpContext.RequestAborted));

    [HttpPost("availability")]
    public Task<IActionResult> Availability([FromBody] ScheduleChangeAvailabilityRequest request) =>
        ExecuteAsync(() => _service.GetAvailabilityAsync(request, HttpContext.RequestAborted));

    [HttpPost("otp/send")]
    public Task<IActionResult> SendOtp([FromBody] SendScheduleChangeOtpRequest request) => ExecuteAsync(async () =>
    {
        await _service.SendOtpAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", HttpContext.RequestAborted);
        return new { message = "Nếu thông tin hợp lệ, OTP đã được gửi tới email của booking." };
    });

    [HttpPost("otp/verify")]
    public Task<IActionResult> VerifyOtp([FromBody] VerifyScheduleChangeOtpRequest request) =>
        ExecuteAsync(() => _service.VerifyOtpAsync(request,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", HttpContext.RequestAborted));

    [HttpPost]
    public Task<IActionResult> Create([FromBody] CreateScheduleChangeRequest request) =>
        ExecuteAsync(() => _service.CreateCustomerRequestAsync(request, HttpContext.RequestAborted), StatusCodes.Status201Created);

    [HttpPost("lookup")]
    public Task<IActionResult> Lookup([FromBody] ScheduleChangeLookupRequest request) =>
        ExecuteAsync(() => _service.GetPublicRequestsAsync(request, HttpContext.RequestAborted));

    [HttpPost("{id:int}/detail")]
    public Task<IActionResult> Detail(int id, [FromBody] ScheduleChangeLookupRequest request) =>
        ExecuteAsync(() => _service.GetPublicRequestAsync(id, request, HttpContext.RequestAborted));

    [HttpPost("{id:int}/quote/accept")]
    public Task<IActionResult> AcceptQuote(int id, [FromBody] AcceptScheduleChangeQuoteRequest request) =>
        ExecuteAsync(() => _service.AcceptQuoteAsync(id, request, HttpContext.RequestAborted));

    [HttpPost("{id:int}/withdraw")]
    public Task<IActionResult> Withdraw(int id, [FromBody] WithdrawScheduleChangeRequest request) =>
        ExecuteAsync(() => _service.WithdrawAsync(id, request, HttpContext.RequestAborted));

    [HttpPost("{id:int}/payment/status")]
    public Task<IActionResult> PaymentStatus(int id, [FromBody] ScheduleChangeLookupRequest request) =>
        ExecuteAsync(() => _service.GetPaymentStatusAsync(id, request, HttpContext.RequestAborted));

    private async Task<IActionResult> ExecuteAsync<T>(Func<Task<T>> operation, int successStatus = StatusCodes.Status200OK)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try { return StatusCode(successStatus, new { success = true, data = await operation() }); }
        catch (ScheduleChangeException ex) { return StatusCode(ex.StatusCode, new { success = false, message = ex.Message }); }
        catch (DbUpdateException) { return Conflict(new { success = false, message = "Dữ liệu vừa thay đổi hoặc lịch đã được giữ bởi yêu cầu khác." }); }
    }
}

