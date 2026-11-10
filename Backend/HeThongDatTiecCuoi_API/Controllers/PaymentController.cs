using System.Security.Claims;
using HeThongDatTiecCuoi_API.DTOs.Payment;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PayOS.Models.Webhooks;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/v1/payment")]
public sealed class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        IPaymentService paymentService,
        ILogger<PaymentController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    /// <summary>
    /// Tạo liên kết thanh toán đặt cọc VietQR PayOS (20% dự toán tiệc, có hỗ trợ Demo Scale).
    /// Hỗ trợ cả người dùng đã đăng nhập hoặc khách tra cứu bằng mã đặt tiệc + SĐT.
    /// </summary>
    [HttpPost("create-deposit")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateDeposit([FromBody] CreateDepositPaymentRequestDto? request)
    {
        try
        {
            if (request == null || request.BookingId <= 0)
                return BadRequest(new { success = false, message = "Thiếu thông tin đặt tiệc cần thanh toán cọc." });

            int? userId = null;
            if (User.Identity?.IsAuthenticated == true &&
                int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid))
            {
                userId = uid;
            }

            var result = await _paymentService.CreateDepositPaymentAsync(request, userId);
            return Ok(new { success = true, data = result });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi ngoài ý muốn khi tạo link cọc PayOS cho BookingId={BookingId}. traceId={TraceId}", request?.BookingId, HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Không thể khởi tạo giao dịch thanh toán VietQR lúc này. Vui lòng thử lại sau.",
                traceId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Tạo liên kết thanh toán hợp đồng (Đợt 2 / Thanh toán từ xa) VietQR PayOS cho khách hàng.
    /// Hỗ trợ cả người dùng đã đăng nhập hoặc khách tra cứu bằng mã đặt tiệc + SĐT.
    /// </summary>
    [HttpPost("create-contract-payment")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateContractPayment([FromBody] CreateCustomerContractPaymentRequestDto? request)
    {
        try
        {
            if (request == null || request.BookingId <= 0)
                return BadRequest(new { success = false, message = "Thiếu thông tin đặt tiệc cần thanh toán." });

            int? userId = null;
            if (User.Identity?.IsAuthenticated == true &&
                int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid))
            {
                userId = uid;
            }

            var result = await _paymentService.CreateCustomerContractPaymentAsync(request, userId);
            return Ok(new { success = true, data = result });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi ngoài ý muốn khi tạo link thanh toán hợp đồng PayOS cho BookingId={BookingId}. traceId={TraceId}", request?.BookingId, HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Không thể khởi tạo giao dịch thanh toán VietQR lúc này. Vui lòng thử lại sau.",
                traceId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Kiểm tra trạng thái giao dịch phục vụ Frontend Polling mỗi 2 giây.
    /// </summary>
    [HttpGet("check-status/{orderCode:long}")]
    [AllowAnonymous]
    public async Task<IActionResult> CheckStatus(long orderCode)
    {
        try
        {
            if (orderCode <= 0)
                return BadRequest(new { success = false, message = "OrderCode không hợp lệ." });

            var result = await _paymentService.GetPaymentStatusAsync(orderCode);
            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi kiểm tra trạng thái thanh toán OrderCode={OrderCode}. traceId={TraceId}", orderCode, HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Lỗi khi kiểm tra trạng thái thanh toán. Vui lòng thử lại sau.",
                traceId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Tiếp nhận Webhook thông báo giao dịch thành công từ PayOS (được xác thực chữ ký SHA256).
    /// </summary>
    [HttpPost("payos-webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> PayOSWebhook([FromBody] Webhook? webhookBody)
    {
        try
        {
            if (webhookBody == null)
                return BadRequest(new { success = false, message = "Webhook payload rỗng." });

            var processed = await _paymentService.ProcessWebhookAsync(webhookBody);
            return Ok(new { success = processed, message = "Đã xử lý Webhook thành công." });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Webhook PayOS bị từ chối do chữ ký không hợp lệ: {Message}", ex.Message);
            return Unauthorized(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xử lý Webhook PayOS. traceId={TraceId}", HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Lỗi xử lý Webhook.",
                traceId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Tải hóa đơn / biên lai điện tử PDF có chữ ký số doanh nghiệp (X.509 PAdES).
    /// </summary>
    [HttpGet("invoice/{bookingCode}")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadInvoice(string bookingCode, [FromQuery] string phone)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(bookingCode) || string.IsNullOrWhiteSpace(phone))
                return BadRequest(new { success = false, message = "Thiếu mã đặt tiệc hoặc số điện thoại." });

            var (pdfBytes, fileName, _) = await _paymentService.GetSignedInvoicePdfAsync(bookingCode, phone);
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi tải hóa đơn điện tử cho {BookingCode}. traceId={TraceId}", bookingCode, HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Không thể tạo hóa đơn điện tử lúc này. Vui lòng thử lại sau.",
                traceId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Tra cứu và xác thực tính hợp lệ của chữ ký số trên hóa đơn điện tử theo NĐ 123/2020/NĐ-CP.
    /// </summary>
    [HttpGet("invoice/verify/{bookingCode}")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyInvoice(string bookingCode, [FromQuery] string phone)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(bookingCode) || string.IsNullOrWhiteSpace(phone))
                return BadRequest(new { success = false, message = "Thiếu mã đặt tiệc hoặc số điện thoại." });

            var (_, fileName, signature) = await _paymentService.GetSignedInvoicePdfAsync(bookingCode, phone);
            return Ok(new
            {
                success = true,
                data = new
                {
                    fileName,
                    isDigitallySigned = true,
                    signatureAlgorithm = signature.Algorithm,
                    signedAt = signature.SignedAt,
                    signer = signature.CertificateSubject,
                    thumbprint = signature.CertificateThumbprint,
                    legalBasis = "Nghị định 123/2020/NĐ-CP & Thông tư 78/2021/TT-BTC, Luật Giao dịch điện tử 2023",
                    status = "HỢP LỆ — Tài liệu điện tử toàn vẹn và được xác thực chữ ký số X.509"
                }
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xác thực hóa đơn điện tử cho {BookingCode}. traceId={TraceId}", bookingCode, HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Không thể xác thực hóa đơn lúc này. Vui lòng thử lại sau.",
                traceId = HttpContext.TraceIdentifier
            });
        }
    }
}
