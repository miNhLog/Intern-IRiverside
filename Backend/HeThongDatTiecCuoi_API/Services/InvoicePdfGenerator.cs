using System.Globalization;
using HeThongDatTiecCuoi_API.DTOs.Invoice;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HeThongDatTiecCuoi_API.Services;

public interface IInvoicePdfGenerator
{
    byte[] Generate(InvoicePdfDto invoice);
}

public sealed class InvoicePdfGenerator : IInvoicePdfGenerator
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

    static InvoicePdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.ThrowOnMissingTextGlyphs = false;
    }

    public byte[] Generate(InvoicePdfDto invoice) => Document.Create(document =>
    {
        document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(32);
            page.DefaultTextStyle(style => style.FontFamily("Lato").FontSize(9.5f).FontColor("#17233A"));

            // 1. HEADER
            page.Header().Column(header =>
            {
                header.Spacing(3);
                header.Item().Row(row =>
                {
                    row.RelativeItem(2).Column(c =>
                    {
                        c.Item().Text("RIVERSIDE PALACE").FontSize(17).Bold().FontColor("#A77B2D");
                        c.Item().Text(invoice.CompanyName).FontSize(8.5f).Bold();
                        c.Item().Text($"Địa chỉ: {invoice.CompanyAddress}").FontSize(8).FontColor("#64748B");
                        c.Item().Text($"Mã số thuế: {invoice.CompanyTaxCode} · Hotline: {invoice.CompanyHotline}").FontSize(8).FontColor("#64748B");
                    });

                    row.RelativeItem(1.2f).AlignRight().Column(c =>
                    {
                        c.Item().Text("BIÊN LAI ĐIỆN TỬ").FontSize(13).Bold().FontColor("#0F2740");
                        c.Item().Text("(ELECTRONIC RECEIPT)").FontSize(8).Italic().FontColor("#64748B");
                        c.Item().Text($"Số HĐ: {invoice.InvoiceNumber}").FontSize(9).Bold().FontColor("#DC2626");
                        c.Item().Text($"Ngày lập: {invoice.InvoiceDate:dd/MM/yyyy HH:mm}").FontSize(8.5f);
                        c.Item().Text($"Ký hiệu: RSP/26E").FontSize(8).FontColor("#64748B");
                    });
                });

                header.Item().PaddingTop(6).LineHorizontal(1.5f).LineColor("#A77B2D");
            });

            // 2. CONTENT
            page.Content().PaddingVertical(10).Column(column =>
            {
                column.Spacing(9);

                // Thông tin khách hàng & Đơn tiệc
                column.Item().Border(1).BorderColor("#E2E8F0").Padding(10).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Spacing(3);
                        c.Item().Text("THÔNG TIN KHÁCH HÀNG").Bold().FontSize(9).FontColor("#A77B2D");
                        c.Item().Text($"Khách hàng: {invoice.CustomerName}").Bold();
                        c.Item().Text($"Số điện thoại: {invoice.CustomerPhone}");
                        c.Item().Text($"Email: {(string.IsNullOrWhiteSpace(invoice.CustomerEmail) ? "Chưa cung cấp" : invoice.CustomerEmail)}");
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Spacing(3);
                        c.Item().Text("THÔNG TIN TIỆC CƯỚI").Bold().FontSize(9).FontColor("#A77B2D");
                        c.Item().Text($"Mã đặt tiệc: {invoice.BookingCode}").Bold();
                        c.Item().Text($"Đại sảnh: {invoice.HallName} · Ca: {invoice.Shift}");
                        c.Item().Text($"Ngày tổ chức: {invoice.EventDate:dd/MM/yyyy}");
                        c.Item().Text($"Quy mô: {invoice.GuestCount} khách ({invoice.TotalTables} bàn)");
                    });
                });

                // Bảng chi tiết thanh toán
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(30);
                        cols.RelativeColumn(3.5f);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(1.8f);
                    });

                    table.Header(h =>
                    {
                        h.Cell().Background("#F8FAFC").Border(1).BorderColor("#CBD5E1").Padding(5).AlignCenter().Text("STT").Bold().FontSize(8.5f);
                        h.Cell().Background("#F8FAFC").Border(1).BorderColor("#CBD5E1").Padding(5).Text("Nội dung khoản thu").Bold().FontSize(8.5f);
                        h.Cell().Background("#F8FAFC").Border(1).BorderColor("#CBD5E1").Padding(5).Text("Hình thức / Mã GD").Bold().FontSize(8.5f);
                        h.Cell().Background("#F8FAFC").Border(1).BorderColor("#CBD5E1").Padding(5).AlignRight().Text("Số tiền (VNĐ)").Bold().FontSize(8.5f);
                    });

                    table.Cell().Border(1).BorderColor("#E2E8F0").Padding(6).AlignCenter().Text("1");
                    table.Cell().Border(1).BorderColor("#E2E8F0").Padding(6).Column(c =>
                    {
                        c.Item().Text(invoice.PaymentType).Bold();
                        c.Item().Text($"Giữ chỗ tiệc cưới hồ sơ {invoice.BookingCode} tại {invoice.HallName}").FontSize(8).FontColor("#64748B");
                        if (!string.IsNullOrWhiteSpace(invoice.MenuName))
                            c.Item().Text($"Thực đơn: {invoice.MenuName}").FontSize(8).FontColor("#64748B");
                    });
                    table.Cell().Border(1).BorderColor("#E2E8F0").Padding(6).Column(c =>
                    {
                        c.Item().Text(invoice.PaymentMethod).Bold();
                        c.Item().Text($"Mã GD: {invoice.TransactionCode}").FontSize(8).FontColor("#64748B");
                    });
                    table.Cell().Border(1).BorderColor("#E2E8F0").Padding(6).AlignRight().Text($"{invoice.AmountPaid:N0} đ").Bold().FontColor("#A77B2D");
                });

                // Tóm tắt công nợ
                column.Item().AlignRight().Width(260).Border(1).BorderColor("#E2E8F0").Padding(8).Column(c =>
                {
                    c.Spacing(3);
                    c.Item().Row(r => { r.RelativeItem().Text("Tổng dự toán:"); r.ConstantItem(100).AlignRight().Text($"{invoice.TotalEstimatedCost:N0} đ"); });
                    c.Item().Row(r => { r.RelativeItem().Text("Số tiền thanh toán kỳ này:").Bold(); r.ConstantItem(100).AlignRight().Text($"{invoice.AmountPaid:N0} đ").Bold().FontColor("#A77B2D"); });
                    c.Item().Row(r => { r.RelativeItem().Text("Lũy kế đã thanh toán:"); r.ConstantItem(100).AlignRight().Text($"{invoice.AccumulatedPaid:N0} đ"); });
                    c.Item().Row(r => { r.RelativeItem().Text("Số tiền còn lại:").Bold(); r.ConstantItem(100).AlignRight().Text($"{invoice.RemainingDebt:N0} đ").Bold().FontColor("#DC2626"); });
                });

                // Khối Chữ ký số điện tử (Digital Signature Section)
                column.Item().PaddingTop(10).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("NGƯỜI NỘP TIỀN / KHÁCH HÀNG").Bold().FontSize(9);
                        c.Item().Text("(Ký, ghi rõ họ tên)").Italic().FontSize(8).FontColor("#64748B");
                        c.Item().PaddingTop(42).Text(invoice.CustomerName).Bold();
                    });

                    // Khung con dấu số điện tử Riverside Palace
                    row.RelativeItem(1.4f).Border(1.5f).BorderColor("#DC2626").Background("#FEF2F2").Padding(8).Column(c =>
                    {
                        c.Spacing(2);
                        c.Item().Row(r =>
                        {
                            r.RelativeItem().Text("[ĐÃ KÝ SỐ ĐIỆN TỬ HỢP LỆ]").Bold().FontSize(8.5f).FontColor("#DC2626");
                        });
                        c.Item().Text($"Ký bởi: {invoice.CompanyName}").FontSize(8).Bold();
                        c.Item().Text($"Mã số thuế: {invoice.CompanyTaxCode}").FontSize(8);
                        c.Item().Text($"Thời gian ký: {invoice.SignedAt ?? DateTime.Now:dd/MM/yyyy HH:mm:ss} (GMT+7)").FontSize(8);
                        c.Item().Text($"Tiêu chuẩn: {invoice.SignatureAlgorithm}").FontSize(7.5f).FontColor("#64748B");
                        c.Item().Text($"Tra cứu trực tuyến: {invoice.InvoiceNumber}").FontSize(8).FontColor("#A77B2D").Bold();
                    });
                });
            });

            // 3. FOOTER
            page.Footer().Column(footer =>
            {
                footer.Item().LineHorizontal(0.5f).LineColor("#E2E8F0");
                footer.Item().PaddingTop(4).Row(row =>
                {
                    row.RelativeItem().Text($"Hóa đơn điện tử khởi tạo từ Hệ thống Đặt tiệc cưới Riverside Palace · Tra cứu tại: {invoice.VerificationUrl}").FontSize(7.5f).FontColor("#94A3B8");
                    row.ConstantItem(100).AlignRight().Text(text =>
                    {
                        text.Span("Trang ");
                        text.CurrentPageNumber();
                        text.Span(" / ");
                        text.TotalPages();
                    });
                });
            });
        });
    }).GeneratePdf();
}
