using System.Text;
using HeThongDatTiecCuoi_WEB.Models.AdminPricingPolicy;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = "Quản trị viên")]
[Route("admin/bang-gia-chinh-sach")]
public sealed class AdminPricingPolicyController : Controller
{
    private const string ApiTokenCookie = "rp_api_token";
    private const string DefaultTab = "pricing";

    private static readonly HashSet<string> SupportedCategories =
    ["halls", "menus", "decor-packages", "services"];

    private readonly IRiversideApiClient _apiClient;

    public AdminPricingPolicyController(IRiversideApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? tab, CancellationToken cancellationToken)
    {
        var accessToken = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return RedirectToAction("Login", "Auth");
        }

        var pricingTask = _apiClient.GetPricingAsync(null, accessToken, cancellationToken);
        var policiesTask = _apiClient.GetPoliciesAsync(accessToken, cancellationToken);
        await Task.WhenAll(pricingTask, policiesTask);

        var pricingResult = await pricingTask;
        var policiesResult = await policiesTask;

        return View(new AdminPricingPolicyViewModel
        {
            ActiveTab = NormalizeTab(tab),
            Pricing = pricingResult.Value,
            Policies = policiesResult.Value,
            PricingError = pricingResult.Succeeded
                ? null
                : pricingResult.Error ?? "Không thể tải bảng giá.",
            PoliciesError = policiesResult.Succeeded
                ? null
                : policiesResult.Error ?? "Không thể tải chính sách."
        });
    }

    [HttpPost("pricing/{category}/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePrice(
        string category,
        int id,
        [FromBody] UpdatePriceRequestDto? request,
        CancellationToken cancellationToken)
    {
        category = category.Trim().ToLowerInvariant();
        if (id <= 0 || !SupportedCategories.Contains(category) || request is null)
        {
            return ApiFailure(StatusCodes.Status400BadRequest, "Dữ liệu cập nhật giá chưa hợp lệ.");
        }

        if (!IsValidPrice(request.Price, "price") || !IsValidPrice(request.ExpectedPrice, "expectedPrice"))
        {
            return ApiFailure(
                StatusCodes.Status400BadRequest,
                "Dữ liệu cập nhật giá chưa hợp lệ.",
                ModelStateErrors());
        }

        var accessToken = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return ApiFailure(StatusCodes.Status401Unauthorized, "Phiên đăng nhập đã hết hạn.");
        }

        var result = await _apiClient.UpdatePriceAsync(
            category,
            id,
            request.Price!.Value,
            request.ExpectedPrice!.Value,
            accessToken,
            cancellationToken);

        if (result.Succeeded)
        {
            return Ok(ApiSuccess(result.Value, "Cập nhật giá thành công."));
        }

        return result.StatusCode == StatusCodes.Status409Conflict
            ? ApiFailure(
                StatusCodes.Status409Conflict,
                "Giá đã được thay đổi bởi người khác. Hãy tải lại dữ liệu trước khi lưu lại.",
                result.Errors)
            : ApiFailure(
                result.StatusCode ?? StatusCodes.Status503ServiceUnavailable,
                result.Error ?? "Không thể cập nhật giá.",
                result.Errors);
    }

    [HttpPost("policies")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePolicies(
        [FromBody] UpdatePolicyRequestDto? request,
        CancellationToken cancellationToken)
    {
        if (request is null || !ModelState.IsValid)
        {
            return ApiFailure(
                StatusCodes.Status400BadRequest,
                "Dữ liệu chính sách chưa hợp lệ.",
                ModelStateErrors());
        }

        var accessToken = Request.Cookies[ApiTokenCookie];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return ApiFailure(StatusCodes.Status401Unauthorized, "Phiên đăng nhập đã hết hạn.");
        }

        var result = await _apiClient.UpdatePoliciesAsync(
            NormalizePolicy(request),
            accessToken,
            cancellationToken);

        if (result.Succeeded)
        {
            return Ok(ApiSuccess(result.Value, "Cập nhật chính sách thành công."));
        }

        return result.StatusCode == StatusCodes.Status409Conflict
            ? ApiFailure(
                StatusCodes.Status409Conflict,
                "Chính sách đã được cập nhật bởi người khác. Hãy tải lại dữ liệu trước khi lưu lại.",
                result.Errors)
            : ApiFailure(
                result.StatusCode ?? StatusCodes.Status503ServiceUnavailable,
                result.Error ?? "Không thể cập nhật chính sách.",
                result.Errors);
    }

    private static string NormalizeTab(string? tab) =>
        tab is "pricing" or "policies" ? tab : DefaultTab;

    private bool IsValidPrice(decimal? value, string field)
    {
        if (!value.HasValue || value < 0 || value > 9999999999999999.99m)
        {
            ModelState.AddModelError(
                field,
                "Giá phải là số không âm trong giới hạn cho phép.");
            return false;
        }

        if (decimal.Round(value.Value, 2) != value.Value)
        {
            ModelState.AddModelError(field, "Giá chỉ được có tối đa hai chữ số thập phân.");
            return false;
        }

        return true;
    }

    private static UpdatePolicyRequestDto NormalizePolicy(UpdatePolicyRequestDto request) => new()
    {
        ExpectedVersion = request.ExpectedVersion,
        Deposit = request.Deposit is null
            ? null
            : new DepositPolicyRequestDto
            {
                TotalPercentageOfContractValue = request.Deposit.TotalPercentageOfContractValue,
                Installments = request.Deposit.Installments?
                    .Select(item => new DepositInstallmentRequestDto
                    {
                        Sequence = item.Sequence,
                        DueBeforeEventDays = item.DueBeforeEventDays,
                        PercentageOfContractValue = item.PercentageOfContractValue,
                        Note = NormalizeOptional(item.Note)
                    })
                    .ToList()
            },
        Cancellation = request.Cancellation is null
            ? null
            : new CancellationPolicyRequestDto
            {
                Tiers = request.Cancellation.Tiers?
                    .Select(item => new CancellationTierRequestDto
                    {
                        Sequence = item.Sequence,
                        NoticeDaysFrom = item.NoticeDaysFrom,
                        NoticeDaysTo = item.NoticeDaysTo,
                        DepositPenaltyPercentage = item.DepositPenaltyPercentage,
                        DepositRefundPercentage = item.DepositRefundPercentage,
                        Note = NormalizeOptional(item.Note)
                    })
                    .ToList()
            }
    };

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().Normalize(NormalizationForm.FormC);

    private Dictionary<string, string[]> ModelStateErrors() =>
        ModelState
            .Where(pair => pair.Value?.Errors.Count > 0)
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "Giá trị không hợp lệ."
                        : error.ErrorMessage)
                    .ToArray());

    private ObjectResult ApiFailure(
        int statusCode,
        string message,
        Dictionary<string, string[]>? errors = null) =>
        StatusCode(statusCode, new { success = false, message, errors });

    private static object ApiSuccess(object? value, string message) => new
    {
        success = true,
        message,
        value
    };
}
