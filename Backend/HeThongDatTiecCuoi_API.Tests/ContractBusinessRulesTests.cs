using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Services;
using HeThongDatTiecCuoi_API.Controllers;
using HeThongDatTiecCuoi_API.DTOs.Contract;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class ContractBusinessRulesTests
{
    [Theory]
    [InlineData(BookingStatusCodes.Confirmed, false, true)]
    [InlineData(BookingStatusCodes.Pending, false, false)]
    [InlineData(BookingStatusCodes.Preparing, false, false)]
    [InlineData(BookingStatusCodes.Confirmed, true, false)]
    public void EligibleBooking_RequiresConfirmedAndNoContract(string status, bool hasContract, bool expected) =>
        Assert.Equal(expected, ContractBusinessRules.IsEligibleBooking(status, hasContract));

    [Fact]
    public void PaymentProgress_CountsOnlySuccessfulPayments()
    {
        var payments = new[]
        {
            (30_000_000m, PaymentStatusCodes.Success),
            (30_000_000m, PaymentStatusCodes.Success),
            (20_000_000m, PaymentStatusCodes.Pending),
            (10_000_000m, PaymentStatusCodes.Failed)
        };

        var result = ContractBusinessRules.CalculateProgress(120_000_000m, payments);

        Assert.Equal(60_000_000m, result.PaidAmount);
        Assert.Equal(60_000_000m, result.RemainingBalance);
        Assert.Equal(50m, result.Percentage);
        Assert.False(result.HasInconsistency);
    }

    [Theory]
    [InlineData(ContractStatusCodes.Active, 100, 40, true)]
    [InlineData(ContractStatusCodes.Active, 100, 100, false)]
    [InlineData(ContractStatusCodes.Draft, 100, 0, false)]
    [InlineData(ContractStatusCodes.Cancelled, 100, 0, false)]
    public void AwaitingPayment_RequiresActiveContractWithBalance(string status, decimal total, decimal paid, bool expected) =>
        Assert.Equal(expected, ContractBusinessRules.IsAwaitingPayment(status, total, paid));

    [Fact]
    public void Activate_ValidDraft_IsAccepted() =>
        Assert.Null(ContractBusinessRules.ValidateActivation(
            ContractStatusCodes.Draft, 100m, "Nội dung", "Điều khoản", BookingStatusCodes.Confirmed));

    [Theory]
    [InlineData(0, "Nội dung", "Điều khoản", BookingStatusCodes.Confirmed)]
    [InlineData(100, "", "Điều khoản", BookingStatusCodes.Confirmed)]
    [InlineData(100, "Nội dung", "", BookingStatusCodes.Confirmed)]
    [InlineData(100, "Nội dung", "Điều khoản", BookingStatusCodes.Pending)]
    public void Activate_InvalidDraft_IsRejected(decimal total, string content, string terms, string bookingStatus) =>
        Assert.NotNull(ContractBusinessRules.ValidateActivation(
            ContractStatusCodes.Draft, total, content, terms, bookingStatus));

    [Theory]
    [InlineData(100, 100, null)]
    [InlineData(100, 80, "Hợp đồng chưa được thanh toán đủ.")]
    [InlineData(100, 120, "Tổng thanh toán vượt giá trị hợp đồng. Vui lòng kiểm tra dữ liệu.")]
    public void Complete_RequiresExactSuccessfulPayment(decimal total, decimal paid, string? expected) =>
        Assert.Equal(expected, ContractBusinessRules.ValidateCompletion(ContractStatusCodes.Active, total, paid));

    [Theory]
    [InlineData(ContractStatusCodes.Draft, true)]
    [InlineData(ContractStatusCodes.Active, true)]
    [InlineData(ContractStatusCodes.Completed, false)]
    [InlineData(ContractStatusCodes.Cancelled, false)]
    public void Cancel_AllowsOnlyDraftOrActive(string status, bool expected) =>
        Assert.Equal(expected, ContractBusinessRules.CanCancel(status));

    [Fact]
    public void ManagementControllers_AreManagerOnly()
    {
        foreach (var controllerType in new[] { typeof(ManagerContractController), typeof(ManagerPaymentController) })
        {
            var attribute = Assert.Single(controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
            Assert.Equal(RoleNames.Manager, attribute.Roles);
        }
    }

    [Fact]
    public void PaymentController_ExposesOnlyAuthorizedWriteActions()
    {
        var methods = typeof(ManagerPaymentController).GetMethods()
            .Where(method => method.DeclaringType == typeof(ManagerPaymentController) &&
                             method.GetCustomAttributes(true).Any(attribute =>
                                 attribute.GetType().Name is "HttpPostAttribute" or "HttpPutAttribute" or "HttpPatchAttribute" or "HttpDeleteAttribute"));
        Assert.All(methods, m => Assert.Contains(m.Name, new[] { "RecordCashPayment", "CreatePayOsLink" }));
    }

    [Fact]
    public void PdfGenerator_GeneratesVietnamesePdfInMemory()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var detail = new ManagerContractDetailDto
        {
            ContractId = 1,
            ContractCode = "HD00001",
            CreatedDate = new DateTime(2026, 10, 13),
            TotalValue = 120_000_000m,
            ContractContent = "Tổ chức tiệc cưới cho Nguyễn Thị Minh Anh.",
            PaymentTerms = "Điều khoản thanh toán theo chính sách hiện hành.",
            StatusCode = ContractStatusCodes.Draft,
            StatusName = "Bản nháp",
            Booking = new ManagerContractBookingDto
            {
                BookingCode = "DT-20261013-TEST",
                CustomerName = "Nguyễn Thị Minh Anh",
                CustomerPhone = "0900000000",
                CustomerEmail = "minhanh@example.com",
                EventDate = new DateTime(2026, 12, 20),
                Shift = "Ca tối",
                HallName = "Sảnh Seine",
                GuestCount = 300,
                MenuName = "Thực đơn Riverside",
                DecorationPackageName = "Thanh lịch",
                Services = ["Âm thanh", "Ánh sáng"]
            }
        };

        var bytes = new ContractPdfGenerator().Generate(detail);

        Assert.True(bytes.Length > 1_000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }
}
