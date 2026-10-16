using System.Globalization;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.DTOs.Contract;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HeThongDatTiecCuoi_API.Services;

public interface IContractPdfGenerator
{
    byte[] Generate(ManagerContractDetailDto contract);
}

public sealed class ContractPdfGenerator : IContractPdfGenerator
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

    public byte[] Generate(ManagerContractDetailDto contract) => Document.Create(document =>
    {
        document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(style => style.FontFamily("Lato").FontSize(10).FontColor("#17233A"));

            page.Header().Column(header =>
            {
                header.Item().AlignCenter().Text("RIVERSIDE PALACE")
                    .FontSize(20).Bold().FontColor("#A77B2D");
                header.Item().AlignCenter().Text("HỢP ĐỒNG TỔ CHỨC TIỆC CƯỚI")
                    .FontSize(14).Bold();
                if (contract.StatusCode == ContractStatusCodes.Draft)
                    header.Item().PaddingTop(4).AlignCenter().Text("BẢN NHÁP")
                        .Bold().FontColor(Colors.Orange.Darken2);
            });

            page.Content().PaddingVertical(18).Column(column =>
            {
                column.Spacing(12);
                column.Item().BorderBottom(1).BorderColor("#D6DCE6").PaddingBottom(8).Row(row =>
                {
                    row.RelativeItem().Text($"Mã hợp đồng: {contract.ContractCode}").Bold();
                    row.RelativeItem().AlignCenter().Text($"Ngày lập: {contract.CreatedDate:dd/MM/yyyy}");
                    row.RelativeItem().AlignRight().Text($"Trạng thái: {contract.StatusName}");
                });

                AddSection(column, "THÔNG TIN KHÁCH HÀNG", section =>
                {
                    AddLabelValue(section, "Họ tên", contract.Booking.CustomerName);
                    AddLabelValue(section, "Số điện thoại", contract.Booking.CustomerPhone);
                    AddLabelValue(section, "Email", contract.Booking.CustomerEmail ?? "Chưa cung cấp");
                });

                AddSection(column, "THÔNG TIN TIỆC", section =>
                {
                    AddLabelValue(section, "Mã booking", contract.Booking.BookingCode);
                    AddLabelValue(section, "Ngày / ca", $"{contract.Booking.EventDate:dd/MM/yyyy} - {contract.Booking.Shift}");
                    AddLabelValue(section, "Sảnh", contract.Booking.HallName);
                    AddLabelValue(section, "Số khách", contract.Booking.GuestCount.ToString(VietnameseCulture));
                    AddLabelValue(section, "Thực đơn", contract.Booking.MenuName ?? "Chưa chọn");
                    AddLabelValue(section, "Trang trí", contract.Booking.DecorationPackageName ?? "Chưa chọn");
                    AddLabelValue(section, "Dịch vụ", contract.Booking.Services.Count == 0
                        ? "Không có"
                        : string.Join(", ", contract.Booking.Services));
                });

                AddSection(column, "GIÁ TRỊ HỢP ĐỒNG", section =>
                    section.Item().Text(FormatMoney(contract.TotalValue)).FontSize(14).Bold().FontColor("#A77B2D"));

                AddSection(column, "NỘI DUNG HỢP ĐỒNG", section =>
                    section.Item().Text(contract.ContractContent).LineHeight(1.35f));

                AddSection(column, "ĐIỀU KHOẢN THANH TOÁN", section =>
                    section.Item().Text(contract.PaymentTerms).LineHeight(1.35f));

                column.Item().PaddingTop(24).Row(row =>
                {
                    row.RelativeItem().AlignCenter().Column(signature =>
                    {
                        signature.Item().Text("ĐẠI DIỆN RIVERSIDE PALACE").Bold();
                        signature.Item().PaddingTop(52).Text("(Ký và ghi rõ họ tên)").Italic().FontSize(9);
                    });
                    row.RelativeItem().AlignCenter().Column(signature =>
                    {
                        signature.Item().Text("KHÁCH HÀNG").Bold();
                        signature.Item().PaddingTop(52).Text("(Ký và ghi rõ họ tên)").Italic().FontSize(9);
                    });
                });
            });

            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Trang ");
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        });
    }).GeneratePdf();

    private static void AddSection(ColumnDescriptor column, string title, Action<ColumnDescriptor> content)
    {
        column.Item().Border(1).BorderColor("#D6DCE6").Padding(12).Column(section =>
        {
            section.Spacing(5);
            section.Item().Text(title).Bold().FontSize(11).FontColor("#0F2740");
            content(section);
        });
    }

    private static void AddLabelValue(ColumnDescriptor section, string label, string value) =>
        section.Item().Row(row =>
        {
            row.ConstantItem(105).Text(label).FontColor("#64748B");
            row.RelativeItem().Text(value);
        });

    private static string FormatMoney(decimal amount) => $"{amount.ToString("N0", VietnameseCulture)} đ";
}
