using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.ManagerContractPayment;
using HeThongDatTiecCuoi_WEB.Models.Auth;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = RoleNames.Manager)]
[Route("quan-ly/hop-dong-thanh-toan")]
public sealed class ManagerContractPaymentController : Controller
{
    private const string TokenCookie = "rp_api_token";
    private readonly IRiversideApiClient _api;
    public ManagerContractPaymentController(IRiversideApiClient api) => _api = api;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var token = Token();
        if (token is null) return RedirectToAction("Login", "Auth");
        var result = await _api.GetManagerContractsAsync(token, null, null, null, null, 1, cancellationToken);
        if (!result.Succeeded)
            ViewBag.ErrorMessage = result.Error ?? "Không thể tải danh sách hợp đồng.";
        return View(new ManagerContractPaymentPageViewModel
        {
            Contracts = result.Value ?? new ManagerContractListResponseViewModel()
        });
    }

    [HttpGet("contracts")]
    public async Task<IActionResult> Contracts(
        string? keyword, string? status, DateTime? fromDate, DateTime? toDate, int page = 1,
        CancellationToken cancellationToken = default)
    {
        var token = Token();
        if (token is null) return UnauthorizedJson();
        var result = await _api.GetManagerContractsAsync(token, keyword, status, fromDate, toDate, page, cancellationToken);
        return ApiJson(result);
    }

    [HttpGet("payments")]
    public async Task<IActionResult> Payments(
        string? keyword, string? type, string? status, DateTime? fromDate, DateTime? toDate, int page = 1,
        CancellationToken cancellationToken = default)
    {
        var token = Token();
        if (token is null) return UnauthorizedJson();
        var result = await _api.GetManagerPaymentsAsync(token, keyword, type, status, fromDate, toDate, page, cancellationToken);
        return ApiJson(result);
    }

    [HttpGet("contracts/{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken cancellationToken)
    {
        var token = Token();
        if (token is null) return UnauthorizedJson();
        return ApiJson(await _api.GetManagerContractAsync(token, id, cancellationToken));
    }

    [HttpGet("eligible-bookings")]
    public async Task<IActionResult> EligibleBookings(string? keyword, CancellationToken cancellationToken)
    {
        var token = Token();
        if (token is null) return UnauthorizedJson();
        return ApiJson(await _api.GetEligibleContractBookingsAsync(token, keyword, cancellationToken));
    }

    [HttpPost("contracts")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] CreateContractViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, message = "Dữ liệu hợp đồng chưa hợp lệ." });
        var token = Token();
        if (token is null) return UnauthorizedJson();
        return ApiJson(await _api.CreateManagerContractAsync(token, model, cancellationToken));
    }

    [HttpPut("contracts/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDraftContractViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, message = "Dữ liệu hợp đồng chưa hợp lệ." });
        var token = Token();
        if (token is null) return UnauthorizedJson();
        return ApiJson(await _api.UpdateManagerContractAsync(token, id, model, cancellationToken));
    }

    [HttpPost("contracts/{id:int}/activate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken)
    {
        var token = Token();
        if (token is null) return UnauthorizedJson();
        return ApiJson(await _api.ActivateManagerContractAsync(token, id, cancellationToken));
    }

    [HttpPost("contracts/{id:int}/complete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id, CancellationToken cancellationToken)
    {
        var token = Token();
        if (token is null) return UnauthorizedJson();
        return ApiJson(await _api.CompleteManagerContractAsync(token, id, cancellationToken));
    }

    [HttpPost("contracts/{id:int}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var token = Token();
        if (token is null) return UnauthorizedJson();
        return ApiJson(await _api.CancelManagerContractAsync(token, id, cancellationToken));
    }

    [HttpGet("contracts/{id:int}/pdf")]
    public async Task<IActionResult> Pdf(int id, CancellationToken cancellationToken)
    {
        var token = Token();
        if (token is null) return RedirectToAction("Login", "Auth");
        var result = await _api.DownloadManagerContractPdfAsync(token, id, cancellationToken);
        if (!result.Succeeded || result.Value is null)
            return StatusCode(result.StatusCode ?? StatusCodes.Status502BadGateway, result.Error);
        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    private string? Token() => Request.Cookies[TokenCookie];
    private JsonResult UnauthorizedJson() => new(new { success = false, message = "Phiên đăng nhập đã hết hạn." })
    {
        StatusCode = StatusCodes.Status401Unauthorized
    };

    private JsonResult ApiJson<T>(ApiCallResult<T> result)
    {
        if (result.Succeeded && result.Value is not null)
            return Json(new { success = true, data = result.Value });
        Response.StatusCode = result.StatusCode ?? StatusCodes.Status502BadGateway;
        return Json(new { success = false, message = result.Error ?? "Yêu cầu không thành công." });
    }
}
