using System.Text;
using HeThongDatTiecCuoi_API.DTOs.Invoice;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.Extensions.Logging.Abstractions;
using QuestPDF.Infrastructure;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public class InvoicePdfAndSignatureTests
{
    public InvoicePdfAndSignatureTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public void GenerateInvoicePdf_ReturnsValidPdfBytes()
    {
        // Arrange
        var generator = new InvoicePdfGenerator();
        var dto = new InvoicePdfDto
        {
            InvoiceNumber = "HDDT-DT-20261021-TEST-01",
            BookingCode = "DT-20261021-TEST",
            CustomerName = "Nguyễn Văn An & Trần Thị Bình",
            CustomerPhone = "0901234567",
            CustomerEmail = "customer@example.com",
            PaymentType = "Đặt cọc (10%)",
            PaymentMethod = "VIETQR_PAYOS",
            TransactionCode = "123456789",
            InvoiceDate = DateTime.Now,
            EventDate = DateTime.Today.AddMonths(2),
            Shift = "Tối",
            HallName = "Sảnh Grand Ballroom",
            GuestCount = 350,
            TotalTables = 35,
            MenuName = "Thực đơn Hoàng Gia Riverside 8 Món",
            DecorName = "Gói Trang Trí Luxury Gold & Flora",
            TotalEstimatedCost = 350_000_000m,
            AmountPaid = 35_000_000m,
            AccumulatedPaid = 35_000_000m,
            RemainingDebt = 315_000_000m,
            VerificationUrl = "https://localhost:7102/MyBooking?bookingCode=DT-20261021-TEST&phone=0901234567",
            IsDigitallySigned = true,
            SignedAt = DateTime.Now
        };

        // Act
        var pdfBytes = generator.Generate(dto);

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 1000);
        var header = Encoding.ASCII.GetString(pdfBytes.AsSpan(0, 5));
        Assert.Equal("%PDF-", header);
    }

    [Fact]
    public void SignPdfDocument_AndVerify_SucceedsAndDetectsTampering()
    {
        // Arrange
        var generator = new InvoicePdfGenerator();
        var signer = new InvoiceDigitalSignatureService(NullLogger<InvoiceDigitalSignatureService>.Instance);
        var dto = new InvoicePdfDto
        {
            InvoiceNumber = "HDDT-DT-20261021-TEST-02",
            BookingCode = "DT-20261021-TEST",
            CustomerName = "Trần Minh Trí",
            CustomerPhone = "0987654321",
            PaymentType = "Đặt cọc (10%)",
            PaymentMethod = "VIETQR_PAYOS",
            TransactionCode = "987654321",
            InvoiceDate = DateTime.Now,
            EventDate = DateTime.Today.AddMonths(1),
            Shift = "Trưa",
            HallName = "Sảnh Amber",
            GuestCount = 200,
            TotalTables = 20,
            TotalEstimatedCost = 180_000_000m,
            AmountPaid = 18_000_000m,
            AccumulatedPaid = 18_000_000m,
            RemainingDebt = 162_000_000m,
            IsDigitallySigned = true,
            SignedAt = DateTime.Now
        };

        var unsignedPdf = generator.Generate(dto);

        // Act
        var signResult = signer.SignPdfDocument(unsignedPdf, dto.InvoiceNumber);

        // Assert signature generation
        Assert.NotNull(signResult);
        Assert.True(signResult.SignedPdfBytes.Length > 0);
        Assert.False(string.IsNullOrWhiteSpace(signResult.SignatureHex));
        Assert.False(string.IsNullOrWhiteSpace(signResult.CertificateThumbprint));
        Assert.Contains("Riverside Palace", signResult.CertificateSubject, StringComparison.OrdinalIgnoreCase);

        // Assert verification on valid signed PDF
        var isValid = signer.VerifySignature(signResult.SignedPdfBytes, signResult.SignatureHex);
        Assert.True(isValid);

        // Assert tampering detection
        var tamperedBytes = (byte[])signResult.SignedPdfBytes.Clone();
        tamperedBytes[tamperedBytes.Length / 2] ^= 0xFF; // Modify a byte
        var isTamperedValid = signer.VerifySignature(tamperedBytes, signResult.SignatureHex);
        Assert.False(isTamperedValid);
    }

    [Fact]
    public void BuildBookingConfirmationContent_DoesNotContainSpamTriggers()
    {
        var (html, plain) = SmtpEmailService.BuildBookingConfirmationContent(
            "Nguyễn Văn An", "DT-20261021-0001", DateTime.Today.AddDays(30), "Tối",
            "Sảnh Grand", 30, 300, "Menu A", "Decor B", 300_000_000m, DateTime.Now.AddHours(48),
            "https://riversidepalace.vn", "0901234567");

        // Không chứa chữ ẩn 1px hoặc opacity:0 (dấu hiệu SpamAssassin PHISH_HIDDEN)
        Assert.DoesNotContain("font-size:1px", html);
        Assert.DoesNotContain("opacity:0", html);

        // Chứa đầy đủ bản plain text và html
        Assert.NotEmpty(plain);
        Assert.NotEmpty(html);
        Assert.Contains("DT-20261021-0001", plain);
        Assert.Contains("DT-20261021-0001", html);
    }

    [Fact]
    public void BuildDepositSuccessContent_DoesNotContainSpamTriggers()
    {
        var (html, plain) = SmtpEmailService.BuildDepositSuccessContent(
            "Nguyễn Văn An", "DT-20261021-0001", 123456789, 30_000_000m, 270_000_000m,
            DateTime.Today.AddDays(30), "Tối", "Sảnh Grand",
            "https://riversidepalace.vn", "0901234567");

        Assert.DoesNotContain("font-size:1px", html);
        Assert.DoesNotContain("opacity:0", html);
        Assert.NotEmpty(plain);
        Assert.NotEmpty(html);
    }
}

