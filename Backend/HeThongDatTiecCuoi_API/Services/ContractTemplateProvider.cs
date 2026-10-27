using System.Globalization;

namespace HeThongDatTiecCuoi_API.Services;

public sealed record ContractTemplateServiceLine(string Name, int Quantity, decimal UnitPrice);

public sealed record ContractTemplateData(
    string BookingCode,
    string CustomerName,
    DateTime EventDate,
    string Shift,
    string HallName,
    int GuestCount,
    string? MenuName,
    string? DecorationPackageName,
    IReadOnlyCollection<string> Services,
    decimal? HallPrice = null,
    decimal? MenuPricePerTable = null,
    decimal? DecorationPrice = null,
    IReadOnlyCollection<ContractTemplateServiceLine>? ServiceLines = null,
    decimal? EstimatedTotal = null,
    decimal ContractTotalValue = 0m);

public interface IContractTemplateProvider
{
    string BuildContractContent(ContractTemplateData data);
    string BuildPaymentTerms();
}

public sealed class ContractTemplateProvider : IContractTemplateProvider
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");
    private readonly IPaymentPolicyProvider _paymentPolicies;

    public ContractTemplateProvider(IPaymentPolicyProvider paymentPolicies) => _paymentPolicies = paymentPolicies;

    public string BuildContractContent(ContractTemplateData data)
    {
        var services = data.Services.Count == 0 ? "Không có" : string.Join(", ", data.Services);
        var breakdown = BuildPriceBreakdown(data);
        return $"""
            Riverside Palace cung cấp dịch vụ tổ chức tiệc theo thông tin booking {data.BookingCode}.

            Khách hàng: {data.CustomerName}
            Ngày tổ chức: {data.EventDate:dd/MM/yyyy}
            Ca tổ chức: {data.Shift}
            Sảnh: {data.HallName}
            Số lượng khách: {data.GuestCount}
            Thực đơn: {data.MenuName ?? "Chưa chọn"}
            Gói trang trí: {data.DecorationPackageName ?? "Chưa chọn"}
            Dịch vụ đi kèm: {services}

            BẢNG GIÁ CHỐT TẠI THỜI ĐIỂM LẬP HỢP ĐỒNG
            {breakdown}

            Tổng giá trị hợp đồng: {FormatMoney(data.ContractTotalValue)}
            """;
    }

    public string BuildPaymentTerms() => _paymentPolicies.BuildContractPaymentTerms();

    /// <summary>
    /// BR04: chỉ dùng snapshot giá của booking (Final*) để dựng bảng giá; không đọc giá catalogue hiện hành.
    /// Dòng điều chỉnh giữ cho tổng các hạng mục luôn bằng tổng giá trị hợp đồng đã thỏa thuận.
    /// </summary>
    private static string BuildPriceBreakdown(ContractTemplateData data)
    {
        var lines = new List<string>();
        var serviceLines = data.ServiceLines ?? [];
        var servicesTotal = serviceLines.Sum(line => line.Quantity * line.UnitPrice);
        var hallPrice = data.HallPrice ?? 0m;
        var decorationPrice = data.DecorationPrice ?? 0m;
        var menuPricePerTable = data.MenuPricePerTable ?? 0m;
        var tableCount = ResolveTableCount(data, hallPrice, decorationPrice, servicesTotal, menuPricePerTable);
        var menuTotal = menuPricePerTable * tableCount;

        if (data.HallPrice.HasValue)
            lines.Add($"- Thuê sảnh {data.HallName}: {FormatMoney(hallPrice)}");
        if (menuPricePerTable > 0)
        {
            var menuName = string.IsNullOrWhiteSpace(data.MenuName) ? "Thực đơn tùy chỉnh" : data.MenuName;
            lines.Add($"- {menuName} ({tableCount} bàn x {FormatMoney(menuPricePerTable)}): {FormatMoney(menuTotal)}");
        }
        if (data.DecorationPrice.HasValue)
            lines.Add($"- Gói trang trí {data.DecorationPackageName ?? "Chưa chọn"}: {FormatMoney(decorationPrice)}");
        foreach (var line in serviceLines)
            lines.Add($"- Dịch vụ {line.Name} ({line.Quantity} x {FormatMoney(line.UnitPrice)}): {FormatMoney(line.Quantity * line.UnitPrice)}");

        if (lines.Count == 0)
            lines.Add("- Chưa có hạng mục giá nào được chốt từ booking.");

        var itemsTotal = hallPrice + menuTotal + decorationPrice + servicesTotal;
        var adjustment = data.ContractTotalValue - itemsTotal;
        if (adjustment != 0m)
            lines.Add($"- Điều chỉnh giá trị hợp đồng: {FormatSignedMoney(adjustment)}");

        return string.Join(Environment.NewLine, lines);
    }

    private static int ResolveTableCount(
        ContractTemplateData data,
        decimal hallPrice,
        decimal decorationPrice,
        decimal servicesTotal,
        decimal menuPricePerTable)
    {
        if (menuPricePerTable <= 0m) return 0;

        if (data.EstimatedTotal.HasValue)
        {
            var menuTotal = data.EstimatedTotal.Value - hallPrice - decorationPrice - servicesTotal;
            if (menuTotal > 0m)
            {
                var derived = (int)Math.Round(menuTotal / menuPricePerTable, MidpointRounding.AwayFromZero);
                if (derived > 0) return derived;
            }
        }

        return Math.Max(1, (int)Math.Ceiling(data.GuestCount / 10m));
    }

    private static string FormatMoney(decimal amount) =>
        $"{amount.ToString("N0", VietnameseCulture)} đ";

    private static string FormatSignedMoney(decimal amount) =>
        $"{(amount > 0m ? "+" : string.Empty)}{amount.ToString("N0", VietnameseCulture)} đ";
}
