namespace HeThongDatTiecCuoi_API.Services;

public static class PriceValidation
{
    public const decimal MaxPrice = 9999999999999999.99m;

    public static bool IsValid(decimal value) =>
        value >= 0 &&
        value <= MaxPrice &&
        GetScale(value) <= 2;

    private static int GetScale(decimal value) =>
        (decimal.GetBits(value)[3] >> 16) & 0x7F;
}
