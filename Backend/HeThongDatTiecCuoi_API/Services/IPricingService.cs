using HeThongDatTiecCuoi_API.DTOs.Pricing;

namespace HeThongDatTiecCuoi_API.Services;

public interface IPricingService
{
    Task<PricingResponseDto> GetAllAsync(CancellationToken cancellationToken);

    Task<PricingCategoryResponseDto> GetCategoryAsync(
        string category,
        CancellationToken cancellationToken);

    Task<PricingItemDto> UpdatePriceAsync(
        string category,
        int id,
        decimal price,
        decimal expectedPrice,
        CancellationToken cancellationToken);
}
