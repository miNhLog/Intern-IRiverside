using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.AdminAccount;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = RoleNames.Admin)]
[Route("admin/nhat-ky-he-thong")]
public sealed class AdminAuditLogController : Controller
{
    private const string ApiTokenCookie = "rp_api_token";
    private readonly IRiversideApiClient _apiClient;

    public AdminAuditLogController(IRiversideApiClient apiClient) => _apiClient = apiClient;

    [HttpGet("")]
    public async Task<IActionResult> Index(
        int page = 1,
        int pageSize = 20,
        string? auditAction = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var accessToken = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(accessToken)) return RedirectToAction("Login", "Auth");

        page = Math.Max(1, page);
        pageSize = new[] { 10, 20, 50 }.Contains(pageSize) ? pageSize : 20;
        auditAction = string.IsNullOrWhiteSpace(auditAction) ? null : auditAction.Trim().ToUpperInvariant();
        keyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        fromDate = fromDate?.Date;
        toDate = toDate?.Date;

        var result = await _apiClient.GetAuditLogsAsync(
            accessToken, page, pageSize, auditAction, fromDate, toDate, keyword, cancellationToken);

        var model = result.Succeeded && result.Value is not null
            ? result.Value
            : new AuditLogPageViewModel { Page = page, PageSize = pageSize };
        model.Action = auditAction;
        model.FromDate = fromDate;
        model.ToDate = toDate;
        model.Keyword = keyword;
        if (!result.Succeeded) model.ErrorMessage = result.Error ?? "Không thể tải nhật ký hệ thống.";
        return View(model);
    }
}
