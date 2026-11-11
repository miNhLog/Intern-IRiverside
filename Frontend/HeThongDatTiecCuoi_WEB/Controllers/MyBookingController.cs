// Alias route /MyBooking theo yêu cầu UC14/UC15 (giữ nguyên /MyBookings/Index tương thích).
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using HeThongDatTiecCuoi_WEB.Models.MyBookings;
using HeThongDatTiecCuoi_WEB.Services;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Route("MyBooking")]
public class MyBookingController : Controller
{
    private readonly IRiversideApiClient _apiClient;
    private readonly ILogger<MyBookingController> _logger;

    public MyBookingController(IRiversideApiClient apiClient, ILogger<MyBookingController> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    // GET /MyBooking renders only the public proof form; proof is never read from a URL or cookie.
    [HttpGet("")]
    [HttpGet("Index")]
    public IActionResult Index()
    {
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
        ViewData["PublicActivePage"] = "my-bookings";
        return View(new PublicBookingLookupInputViewModel());
    }

    // POST /MyBooking/Lookup — gọi Backend lookup, không throw 500.
    [HttpPost("Lookup")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Lookup(PublicBookingLookupInputViewModel input, CancellationToken cancellationToken)
    {
        try
        {
            Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.Expires = "0";
            ViewData["PublicActivePage"] = "my-bookings";
            input.BookingCode = (input.BookingCode ?? string.Empty).Trim().ToUpperInvariant();
            input.PhoneNumber = (input.PhoneNumber ?? string.Empty).Trim();
            ViewBag.LookupInput = input;

            if (!ModelState.IsValid)
            {
                ViewBag.LookupError = "Vui lòng nhập đúng mã đặt tiệc (DT-yyyyMMdd-XXXX) và SĐT 10 số.";
                return View("Index", input);
            }

            var result = await _apiClient.LookupPublicBookingAsync(input.BookingCode, input.PhoneNumber, cancellationToken);
            if (result.Succeeded && result.Value is not null)
            {
                ViewBag.LookupDetail = result.Value;
            }
            else
            {
                ViewBag.LookupError = result.Error ?? "Thông tin mã đặt tiệc hoặc số điện thoại không chính xác.";
                if (result.StatusCode == 429)
                    Response.StatusCode = 429;
            }

            return View("Index", input);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MyBooking Lookup thất bại. traceId={TraceId}", HttpContext.TraceIdentifier);
            ViewBag.LookupError = "Không thể tra cứu lúc này, vui lòng thử lại.";
            ViewBag.LookupInput = input;
            return View("Index", input);
        }
    }

    // POST /MyBooking/CancelHold — gọi Backend cancel-hold, trả JSON cho modal.
    [HttpPost("CancelHold")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelHold(
        [FromForm] int bookingId,
        [FromForm] string bookingCode,
        [FromForm] string phoneNumber,
        [FromForm] string? reason,
        CancellationToken cancellationToken)
    {
        try
        {
            bookingCode = (bookingCode ?? string.Empty).Trim().ToUpperInvariant();
            phoneNumber = (phoneNumber ?? string.Empty).Trim();
            if (bookingId <= 0 || !IsLookupShapeValid(bookingCode, phoneNumber))
                return Json(new { success = false, message = "Dữ liệu hủy giữ chỗ chưa hợp lệ." });

            var result = await _apiClient.CancelPublicBookingHoldAsync(
                bookingId, bookingCode, phoneNumber, reason?.Trim(), cancellationToken);
            if (result.Succeeded)
                return Json(new { success = true, message = result.Value ?? "Đã hủy giữ chỗ thành công." });
            return Json(new { success = false, message = result.Error ?? "Không thể hủy giữ chỗ lúc này." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MyBooking CancelHold thất bại. traceId={TraceId}. Chi tiết: {Detail}",
                HttpContext.TraceIdentifier, ex.ToString());
            return Json(new { success = false, message = "Không thể hủy giữ chỗ lúc này, vui lòng thử lại." });
        }
    }

    // POST /MyBooking/RescheduleEligibility — kiểm tra điều kiện đổi lịch qua API hiện tại.
    [HttpPost("RescheduleEligibility")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RescheduleEligibility(
        [FromForm] string bookingCode,
        [FromForm] string phoneNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!TryNormalizeProof(bookingCode, phoneNumber, out var code, out var phone))
                return Json(new { success = false, message = "Dữ liệu xác thực đổi lịch chưa hợp lệ." });

            var result = await _apiClient.GetScheduleChangeEligibilityAsync(code, phone, cancellationToken);
            if (!result.Succeeded || result.Value is null)
                return Json(new { success = false, message = result.Error ?? "Không thể kiểm tra điều kiện đổi lịch lúc này." });

            var e = result.Value;
            return Json(new
            {
                success = true,
                canRequestOnline = e.CanRequestOnline,
                reason = e.Reason,
                minimumNewDate = e.MinimumNewDate.ToString("yyyy-MM-dd"),
                scheduleChangeFee = e.ScheduleChangeFee,
                policyVersion = e.PolicyVersion,
                currentHallId = e.CurrentHallId,
                currentHallName = e.CurrentHallName,
                currentDate = e.CurrentDate.ToString("yyyy-MM-dd"),
                currentShift = e.CurrentShift
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MyBooking RescheduleEligibility thất bại. traceId={TraceId}", HttpContext.TraceIdentifier);
            return Json(new { success = false, message = "Không thể kiểm tra điều kiện đổi lịch lúc này, vui lòng thử lại." });
        }
    }

    // POST /MyBooking/RescheduleAvailability — tra cứu lịch trống đổi lịch, không đưa proof vào query string.
    [HttpPost("RescheduleAvailability")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RescheduleAvailability(
        [FromForm] string bookingCode,
        [FromForm] string phoneNumber,
        [FromForm] string date,
        [FromForm] string shift,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!TryNormalizeProof(bookingCode, phoneNumber, out var code, out var phone))
                return Json(new { success = false, message = "Dữ liệu xác thực đổi lịch chưa hợp lệ." });
            if (!TryParseDate(date, out var parsedDate) || !IsSupportedShift(shift))
                return Json(new { success = false, message = "Vui lòng chọn ngày và ca hợp lệ." });

            var result = await _apiClient.GetScheduleChangeAvailabilityAsync(code, phone, parsedDate, shift.Trim(), cancellationToken);
            if (!result.Succeeded || result.Value is null)
                return Json(new { success = false, message = result.Error ?? "Không thể kiểm tra lịch trống lúc này." });

            return Json(new
            {
                success = true,
                halls = result.Value.Select(h => new
                {
                    hallId = h.HallId,
                    hallName = h.HallName,
                    available = h.Available,
                    unavailableReason = h.UnavailableReason
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MyBooking RescheduleAvailability thất bại. traceId={TraceId}", HttpContext.TraceIdentifier);
            return Json(new { success = false, message = "Không thể kiểm tra lịch trống lúc này, vui lòng thử lại." });
        }
    }

    // POST /MyBooking/SendRescheduleOtp — gửi OTP xác thực đổi lịch qua email.
    [HttpPost("SendRescheduleOtp")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendRescheduleOtp(
        [FromForm] string bookingCode,
        [FromForm] string phoneNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!TryNormalizeProof(bookingCode, phoneNumber, out var code, out var phone))
                return Json(new { success = false, message = "Dữ liệu xác thực đổi lịch chưa hợp lệ." });

            var result = await _apiClient.SendScheduleChangeOtpAsync(
                code, phone, "SCHEDULE_CHANGE_CREATE", null, cancellationToken);
            return result.Succeeded
                ? Json(new { success = true, message = "Nếu thông tin hợp lệ, mã OTP đã được gửi tới email của booking." })
                : Json(new { success = false, message = result.Error ?? "Không thể gửi OTP lúc này." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MyBooking SendRescheduleOtp thất bại. traceId={TraceId}", HttpContext.TraceIdentifier);
            return Json(new { success = false, message = "Không thể gửi OTP lúc này, vui lòng thử lại." });
        }
    }

    // POST /MyBooking/VerifyRescheduleOtp — xác minh OTP, trả token để tạo yêu cầu đổi lịch.
    [HttpPost("VerifyRescheduleOtp")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyRescheduleOtp(
        [FromForm] string bookingCode,
        [FromForm] string phoneNumber,
        [FromForm] string otpCode,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!TryNormalizeProof(bookingCode, phoneNumber, out var code, out var phone))
                return Json(new { success = false, message = "Dữ liệu xác thực đổi lịch chưa hợp lệ." });
            var normalizedOtp = (otpCode ?? string.Empty).Trim();
            if (!Regex.IsMatch(normalizedOtp, @"^\d{6}$"))
                return Json(new { success = false, message = "Mã OTP phải gồm 6 chữ số." });

            var result = await _apiClient.VerifyScheduleChangeOtpAsync(
                code, phone, "SCHEDULE_CHANGE_CREATE", null, normalizedOtp, cancellationToken);
            return result.Succeeded && result.Value is not null
                ? Json(new { success = true, verificationToken = result.Value.VerificationToken, expiresAt = result.Value.ExpiresAt.ToString("o") })
                : Json(new { success = false, message = result.Error ?? "Không thể xác minh OTP." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MyBooking VerifyRescheduleOtp thất bại. traceId={TraceId}", HttpContext.TraceIdentifier);
            return Json(new { success = false, message = "Không thể xác minh OTP lúc này, vui lòng thử lại." });
        }
    }

    // POST /MyBooking/CreateRescheduleRequest — tạo yêu cầu đổi lịch qua quy trình hiện tại.
    [HttpPost("CreateRescheduleRequest")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRescheduleRequest(
        [FromForm] string bookingCode,
        [FromForm] string phoneNumber,
        [FromForm] string verificationToken,
        [FromForm] int newHallId,
        [FromForm] string newDate,
        [FromForm] string newShift,
        [FromForm] string? reason,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!TryNormalizeProof(bookingCode, phoneNumber, out var code, out var phone))
                return Json(new { success = false, message = "Dữ liệu xác thực đổi lịch chưa hợp lệ." });
            if (string.IsNullOrWhiteSpace(verificationToken) || newHallId <= 0 || !TryParseDate(newDate, out var parsedDate) || !IsSupportedShift(newShift))
                return Json(new { success = false, message = "Dữ liệu yêu cầu đổi lịch chưa hợp lệ." });
            var trimmedReason = (reason ?? string.Empty).Trim();
            if (trimmedReason.Length < 3 || trimmedReason.Length > 500)
                return Json(new { success = false, message = "Vui lòng nhập lý do đổi lịch từ 3 đến 500 ký tự." });

            var result = await _apiClient.CreateScheduleChangeAsync(new HeThongDatTiecCuoi_WEB.Models.ScheduleChange.CreateScheduleChangeInput
            {
                BookingCode = code,
                PhoneNumber = phone,
                VerificationToken = verificationToken.Trim(),
                NewHallId = newHallId,
                NewDate = parsedDate,
                NewShift = newShift.Trim(),
                Reason = trimmedReason
            }, cancellationToken);

            if (!result.Succeeded || result.Value is null)
                return Json(new { success = false, message = result.Error ?? "Không thể tạo yêu cầu đổi lịch lúc này." });

            return Json(new
            {
                success = true,
                message = "Yêu cầu đổi lịch đã được ghi nhận.",
                status = result.Value.Status,
                statusName = result.Value.StatusName,
                scheduleChangeId = result.Value.ScheduleChangeId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MyBooking CreateRescheduleRequest thất bại. traceId={TraceId}", HttpContext.TraceIdentifier);
            return Json(new { success = false, message = "Không thể tạo yêu cầu đổi lịch lúc này, vui lòng thử lại." });
        }
    }

    // POST /MyBooking/RescheduleRequests — tra cứu danh sách yêu cầu đổi lịch hiện có.
    [HttpPost("RescheduleRequests")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RescheduleRequests(
        [FromForm] string bookingCode,
        [FromForm] string phoneNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!TryNormalizeProof(bookingCode, phoneNumber, out var code, out var phone))
                return Json(new { success = false, message = "Dữ liệu xác thực đổi lịch chưa hợp lệ." });

            var result = await _apiClient.LookupScheduleChangesAsync(code, phone, cancellationToken);
            if (!result.Succeeded || result.Value is null)
                return Json(new { success = false, message = result.Error ?? "Không thể tải danh sách yêu cầu đổi lịch." });

            return Json(new
            {
                success = true,
                requests = result.Value.Select(r => new
                {
                    scheduleChangeId = r.ScheduleChangeId,
                    status = r.Status,
                    statusName = r.StatusName,
                    displayStatus = r.DisplayStatus,
                    requestedAt = r.RequestedAt.ToString("o"),
                    reason = r.Reason,
                    oldHallName = r.OldHallName,
                    oldDate = r.OldDate.ToString("yyyy-MM-dd"),
                    oldShift = r.OldShift,
                    newHallName = r.NewHallName,
                    newDate = r.NewDate.ToString("yyyy-MM-dd"),
                    newShift = r.NewShift,
                    resolutionReason = r.ResolutionReason,
                    quote = r.Quote is null ? null : new
                    {
                        quoteId = r.Quote.QuoteId,
                        version = r.Quote.Version,
                        scheduleChangeFee = r.Quote.ScheduleChangeFee,
                        hallPriceDifference = r.Quote.HallPriceDifference,
                        adjustmentTotal = r.Quote.AdjustmentTotal,
                        amountDue = r.Quote.AmountDue,
                        creditAmount = r.Quote.CreditAmount,
                        refundAmount = r.Quote.RefundAmount,
                        status = r.Quote.Status
                    }
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MyBooking RescheduleRequests thất bại. traceId={TraceId}", HttpContext.TraceIdentifier);
            return Json(new { success = false, message = "Không thể tải danh sách yêu cầu đổi lịch lúc này, vui lòng thử lại." });
        }
    }

    // POST /MyBooking/SendRescheduleQuoteOtp — gửi OTP xác nhận báo giá (SCHEDULE_CHANGE_ACCEPT_QUOTE).
    [HttpPost("SendRescheduleQuoteOtp")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendRescheduleQuoteOtp(
        [FromForm] string bookingCode,
        [FromForm] string phoneNumber,
        [FromForm] int scheduleChangeId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!TryNormalizeProof(bookingCode, phoneNumber, out var code, out var phone) || scheduleChangeId <= 0)
                return Json(new { success = false, message = "Dữ liệu xác thực báo giá chưa hợp lệ." });

            var result = await _apiClient.SendScheduleChangeOtpAsync(
                code, phone, "SCHEDULE_CHANGE_ACCEPT_QUOTE", scheduleChangeId, cancellationToken);
            return result.Succeeded
                ? Json(new { success = true, message = "Nếu thông tin hợp lệ, mã OTP đã được gửi tới email của booking." })
                : Json(new { success = false, message = result.Error ?? "Không thể gửi OTP xác nhận báo giá." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MyBooking SendRescheduleQuoteOtp thất bại. traceId={TraceId}", HttpContext.TraceIdentifier);
            return Json(new { success = false, message = "Không thể gửi OTP lúc này, vui lòng thử lại." });
        }
    }

    // POST /MyBooking/VerifyRescheduleQuoteOtp — xác minh OTP xác nhận báo giá.
    [HttpPost("VerifyRescheduleQuoteOtp")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyRescheduleQuoteOtp(
        [FromForm] string bookingCode,
        [FromForm] string phoneNumber,
        [FromForm] int scheduleChangeId,
        [FromForm] string otpCode,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!TryNormalizeProof(bookingCode, phoneNumber, out var code, out var phone) || scheduleChangeId <= 0)
                return Json(new { success = false, message = "Dữ liệu xác thực báo giá chưa hợp lệ." });
            var normalizedOtp = (otpCode ?? string.Empty).Trim();
            if (!Regex.IsMatch(normalizedOtp, @"^\d{6}$"))
                return Json(new { success = false, message = "Mã OTP phải gồm 6 chữ số." });

            var result = await _apiClient.VerifyScheduleChangeOtpAsync(
                code, phone, "SCHEDULE_CHANGE_ACCEPT_QUOTE", scheduleChangeId, normalizedOtp, cancellationToken);
            return result.Succeeded && result.Value is not null
                ? Json(new { success = true, verificationToken = result.Value.VerificationToken, expiresAt = result.Value.ExpiresAt.ToString("o") })
                : Json(new { success = false, message = result.Error ?? "Không thể xác minh OTP." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MyBooking VerifyRescheduleQuoteOtp thất bại. traceId={TraceId}", HttpContext.TraceIdentifier);
            return Json(new { success = false, message = "Không thể xác minh OTP lúc này, vui lòng thử lại." });
        }
    }

    // POST /MyBooking/AcceptRescheduleQuote — chấp nhận báo giá chính xác bằng token đã xác minh.
    [HttpPost("AcceptRescheduleQuote")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptRescheduleQuote(
        [FromForm] string bookingCode,
        [FromForm] string phoneNumber,
        [FromForm] int scheduleChangeId,
        [FromForm] int quoteId,
        [FromForm] string verificationToken,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!TryNormalizeProof(bookingCode, phoneNumber, out var code, out var phone)
                || scheduleChangeId <= 0 || quoteId <= 0 || string.IsNullOrWhiteSpace(verificationToken))
                return Json(new { success = false, message = "Dữ liệu chấp nhận báo giá chưa hợp lệ." });

            var result = await _apiClient.AcceptScheduleChangeQuoteAsync(
                scheduleChangeId, code, phone, quoteId, verificationToken.Trim(), cancellationToken);
            if (!result.Succeeded || result.Value is null)
                return Json(new { success = false, message = result.Error ?? "Không thể chấp nhận báo giá." });

            return Json(new
            {
                success = true,
                message = "Đã xử lý xác nhận báo giá.",
                scheduleChange = new
                {
                    scheduleChangeId = result.Value.ScheduleChange.ScheduleChangeId,
                    status = result.Value.ScheduleChange.Status,
                    statusName = result.Value.ScheduleChange.StatusName,
                    displayStatus = result.Value.ScheduleChange.DisplayStatus
                },
                payment = result.Value.Payment is null ? null : new
                {
                    status = result.Value.Payment.Status,
                    paymentStatus = result.Value.Payment.PaymentStatus,
                    scheduleChangeStatus = result.Value.Payment.ScheduleChangeStatus,
                    businessAmount = result.Value.Payment.BusinessAmount,
                    actualAmount = result.Value.Payment.ActualAmount,
                    checkoutUrl = result.Value.Payment.CheckoutUrl,
                    message = result.Value.Payment.Message,
                    orderCode = result.Value.Payment.OrderCode
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MyBooking AcceptRescheduleQuote thất bại. traceId={TraceId}", HttpContext.TraceIdentifier);
            return Json(new { success = false, message = "Không thể chấp nhận báo giá lúc này, vui lòng thử lại." });
        }
    }

    private static bool TryNormalizeProof(string? bookingCode, string? phoneNumber, out string code, out string phone)
    {
        code = (bookingCode ?? string.Empty).Trim().ToUpperInvariant();
        phone = (phoneNumber ?? string.Empty).Trim();
        return IsLookupShapeValid(code, phone);
    }

    private static bool TryParseDate(string? date, out DateTime parsedDate) =>
        DateTime.TryParseExact((date ?? string.Empty).Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate);

    private static bool IsSupportedShift(string? shift) => (shift ?? string.Empty).Trim() is "Ca trưa" or "Ca tối";

    private static bool IsLookupShapeValid(string code, string phone) =>
        Regex.IsMatch(code ?? string.Empty, @"^DT-\d{8}-[A-Z0-9]{4,}$")
        && Regex.IsMatch(phone ?? string.Empty, @"^0\d{9}$");
}
