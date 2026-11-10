namespace HeThongDatTiecCuoi_API.Services;

public interface IHallPricingPolicyService
{
    decimal CalculateHallPrice(decimal basePrice, string? shift, DateTime eventDate);
    (decimal BasePrice, decimal NoonPrice, decimal EveningPrice, decimal WeekendPrice) GetShiftPriceBreakdown(decimal basePrice);
}

public sealed class HallPricingPolicyService : IHallPricingPolicyService
{
    public const decimal EveningSurchargeRate = 0.15m; // Ca tối (+15%)
    public const decimal WeekendSurchargeRate = 0.25m; // Cuối tuần (+25%)

    public decimal CalculateHallPrice(decimal basePrice, string? shift, DateTime eventDate)
    {
        if (basePrice <= 0) return 0;
        bool isWeekend = eventDate.DayOfWeek == DayOfWeek.Saturday || eventDate.DayOfWeek == DayOfWeek.Sunday;
        bool isDinner = !string.IsNullOrWhiteSpace(shift) &&
            (shift.Contains("tối", StringComparison.OrdinalIgnoreCase) || shift.Contains("toi", StringComparison.OrdinalIgnoreCase));

        // Chính sách:
        // 1. Cuối tuần (Thứ 7 & Chủ Nhật): +25%
        // 2. Ngày thường Ca tối: +15%
        // 3. Ngày thường Ca trưa: Giá gốc (100%)
        if (isWeekend)
        {
            return Math.Round(basePrice * (1.0m + WeekendSurchargeRate), 0);
        }
        if (isDinner)
        {
            return Math.Round(basePrice * (1.0m + EveningSurchargeRate), 0);
        }
        return Math.Round(basePrice, 0);
    }

    public (decimal BasePrice, decimal NoonPrice, decimal EveningPrice, decimal WeekendPrice) GetShiftPriceBreakdown(decimal basePrice)
    {
        return (
            basePrice,
            basePrice,
            Math.Round(basePrice * (1.0m + EveningSurchargeRate), 0),
            Math.Round(basePrice * (1.0m + WeekendSurchargeRate), 0)
        );
    }
}

