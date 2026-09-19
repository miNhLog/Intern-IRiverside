using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.Pricing;

public sealed class PricingResponseDto
{
    public string Currency { get; init; } = "VND";

    public IReadOnlyList<PricingCategoryResponseDto> Categories { get; init; } = [];
}

public sealed class PricingCategoryResponseDto
{
    public string Category { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public IReadOnlyList<PricingItemDto> Items { get; init; } = [];
}

public sealed class PricingItemDto
{
    public int Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public decimal Price { get; init; }

    public string Status { get; init; } = string.Empty;
}

public sealed class UpdatePriceRequestDto
{
    [Required(ErrorMessage = "Price is required.")]
    [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "Price must be between 0 and 9999999999999999.99.")]
    public decimal? Price { get; init; }

    [Required(ErrorMessage = "Expected price is required for conflict detection.")]
    [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "Expected price must be between 0 and 9999999999999999.99.")]
    public decimal? ExpectedPrice { get; init; }
}
