namespace HeThongDatTiecCuoi_API.Services;

public sealed record ContractTemplateData(
    string BookingCode,
    string CustomerName,
    DateTime EventDate,
    string Shift,
    string HallName,
    int GuestCount,
    string? MenuName,
    string? DecorationPackageName,
    IReadOnlyCollection<string> Services);

public interface IContractTemplateProvider
{
    string BuildContractContent(ContractTemplateData data);
    string BuildPaymentTerms();
}

public sealed class ContractTemplateProvider : IContractTemplateProvider
{
    private readonly IPaymentPolicyProvider _paymentPolicies;

    public ContractTemplateProvider(IPaymentPolicyProvider paymentPolicies) =>
        _paymentPolicies = paymentPolicies;

    public string BuildContractContent(ContractTemplateData data)
    {
        var services = data.Services.Count == 0 ? "Không có" : string.Join(", ", data.Services);
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
            """;
    }

    public string BuildPaymentTerms() => _paymentPolicies.BuildContractPaymentTerms();
}
