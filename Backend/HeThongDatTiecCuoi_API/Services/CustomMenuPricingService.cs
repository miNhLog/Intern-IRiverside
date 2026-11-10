using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Services;

public sealed record CustomMenuDishSnapshot(int DishId, string DishName, decimal Price);

public sealed record CustomMenuPricingResult(
    bool IsValid,
    int StatusCode,
    string? ErrorCode,
    string? Message,
    int MenuId,
    string MenuName,
    decimal OfficialPricePerTable,
    bool IsCustomized,
    bool RequiresPriceConfirmation,
    IReadOnlyList<CustomMenuDishSnapshot> SelectedDishes)
{
    public static CustomMenuPricingResult Failure(int statusCode, string code, string message) =>
        new(false, statusCode, code, message, 0, string.Empty, 0, false, false, []);
}

/// <summary>
/// Resolves all menu prices from the database. Client-provided names and item prices
/// are intentionally absent from this API and therefore cannot influence the result.
/// </summary>
public sealed class CustomMenuPricingService(ApplicationDbContext context)
{
    private const decimal MaxDatabaseMoney = 9_999_999_999_999_999.99m;

    public async Task<CustomMenuPricingResult> ResolveAsync(
        int menuId,
        int? customerId,
        IReadOnlyCollection<int>? customDishIds,
        bool customRequested,
        decimal? expectedCustomPricePerTable,
        CancellationToken cancellationToken = default)
    {
        var menu = await context.Menus.AsNoTracking()
            .Include(item => item.Status)
            .Include(item => item.DataStatus)
            .SingleOrDefaultAsync(item => item.MenuId == menuId, cancellationToken);
        if (menu is null ||
            menu.DataStatus is null || menu.DataStatus.DataStatusCode != DataStatusCodes.Existing ||
            menu.Status is null || menu.Status.StatusGroup != StatusGroups.Menu ||
            menu.Status.StatusCode != MenuStatusCodes.Active ||
            menu.PricePerTable <= 0 || menu.PricePerTable > MaxDatabaseMoney)
            return CustomMenuPricingResult.Failure(400, "INVALID_BASE_MENU",
                "Thực đơn gốc không tồn tại, đã ngừng phục vụ hoặc có giá không hợp lệ.");

        if (menu.CustomerId.HasValue && menu.CustomerId != customerId)
            return CustomMenuPricingResult.Failure(403, "MENU_ACCESS_DENIED",
                "Thực đơn này thuộc riêng một khách hàng khác.");

        if (!customRequested)
            return new CustomMenuPricingResult(true, 200, null, null,
                menu.MenuId, menu.MenuName, Money(menu.PricePerTable), false, false, []);

        if (customDishIds is null)
            return CustomMenuPricingResult.Failure(400, "CUSTOM_MENU_DISH_IDS_REQUIRED",
                "Client cũ không cung cấp mã món đáng tin cậy. Vui lòng tải lại trang và chọn lại thực đơn tùy chỉnh.");
        if (customDishIds.Count == 0)
            return CustomMenuPricingResult.Failure(400, "CUSTOM_MENU_EMPTY",
                "Thực đơn tùy chỉnh phải có ít nhất một món.");
        if (customDishIds.Any(id => id <= 0) || customDishIds.Distinct().Count() != customDishIds.Count)
            return CustomMenuPricingResult.Failure(400, "CUSTOM_MENU_DISH_IDS_INVALID",
                "Danh sách mã món không hợp lệ hoặc có món bị trùng.");

        var originalLinks = await context.MenuDishes.AsNoTracking()
            .Where(link => link.MenuId == menuId &&
                link.DataStatus.DataStatusCode == DataStatusCodes.Existing)
            .Include(link => link.Dish).ThenInclude(dish => dish.Status)
            .Include(link => link.Dish).ThenInclude(dish => dish.DataStatus)
            .OrderBy(link => link.SortOrder)
            .ToListAsync(cancellationToken);
        if (originalLinks.Count == 0 ||
            originalLinks.Select(link => link.DishId).Distinct().Count() != originalLinks.Count ||
            originalLinks.Any(link => link.Dish is null || !ValidDish(link.Dish)))
            return CustomMenuPricingResult.Failure(409, "BASE_MENU_DISH_DATA_INVALID",
                "Danh sách món của thực đơn gốc không đầy đủ hoặc không còn hợp lệ. Vui lòng liên hệ quản lý cập nhật thực đơn.");

        if (customDishIds.Count > originalLinks.Count)
            return CustomMenuPricingResult.Failure(400, "CUSTOM_MENU_TOO_MANY_DISHES",
                $"Thực đơn tùy chỉnh không được vượt quá {originalLinks.Count} món của thực đơn gốc.");

        var selectedIds = customDishIds.ToArray();
        var selectedEntities = await context.Dishes.AsNoTracking()
            .Where(dish => selectedIds.Contains(dish.DishId))
            .Include(dish => dish.Status)
            .Include(dish => dish.DataStatus)
            .ToListAsync(cancellationToken);
        if (selectedEntities.Count != selectedIds.Length || selectedEntities.Any(dish => !ValidDish(dish)))
            return CustomMenuPricingResult.Failure(409, "CUSTOM_MENU_DISH_UNAVAILABLE",
                "Có món không tồn tại, đã bị xóa, ngừng kinh doanh hoặc có giá không hợp lệ.");

        var originalIds = originalLinks.Select(link => link.DishId).ToHashSet();
        var restoredOriginal = selectedIds.Length == originalIds.Count && selectedIds.All(originalIds.Contains);
        if (restoredOriginal)
            return new CustomMenuPricingResult(true, 200, null, null,
                menu.MenuId, menu.MenuName, Money(menu.PricePerTable), false, false, []);

        try
        {
            var originalDishTotal = SumMoney(originalLinks.Select(link => link.Dish.Price));
            var selectedById = selectedEntities.ToDictionary(dish => dish.DishId);
            var selectedDishTotal = SumMoney(selectedIds.Select(id => selectedById[id].Price));
            var officialPrice = Money(checked(menu.PricePerTable + selectedDishTotal - originalDishTotal));
            if (officialPrice <= 0 || officialPrice > MaxDatabaseMoney)
                return CustomMenuPricingResult.Failure(409, "CUSTOM_MENU_PRICE_INVALID",
                    "Dữ liệu giá hiện tại tạo ra đơn giá thực đơn không hợp lệ.");

            var snapshots = selectedIds.Select(id =>
                new CustomMenuDishSnapshot(id, selectedById[id].DishName, Money(selectedById[id].Price))).ToList();
            var priceChanged = !expectedCustomPricePerTable.HasValue ||
                Money(expectedCustomPricePerTable.Value) != officialPrice;
            return new CustomMenuPricingResult(true, 200, null, null,
                menu.MenuId, menu.MenuName, officialPrice, true, priceChanged, snapshots);
        }
        catch (OverflowException)
        {
            return CustomMenuPricingResult.Failure(409, "CUSTOM_MENU_PRICE_INVALID",
                "Không thể tính giá thực đơn vì dữ liệu tiền vượt giới hạn cho phép.");
        }
    }

    public static decimal CalculateEstimatedTotal(
        decimal hallPrice,
        decimal menuPricePerTable,
        int tableCount,
        decimal decorationPrice,
        decimal servicesTotal)
    {
        if (hallPrice < 0 || menuPricePerTable < 0 || tableCount < 0 ||
            decorationPrice < 0 || servicesTotal < 0)
            throw new ArgumentOutOfRangeException(nameof(tableCount), "Các thành phần dự toán không được âm.");

        var total = checked(hallPrice + checked(menuPricePerTable * tableCount) +
            decorationPrice + servicesTotal);
        total = Money(total);
        if (total > MaxDatabaseMoney)
            throw new OverflowException("Tổng dự toán vượt giới hạn decimal(18,2).");
        return total;
    }

    private static bool ValidDish(Dish dish) =>
        dish.DataStatus is not null && dish.DataStatus.DataStatusCode == DataStatusCodes.Existing &&
        dish.Status is not null && dish.Status.StatusGroup == StatusGroups.Dish &&
        dish.Status.StatusCode == DishStatusCodes.Active &&
        dish.Price >= 0 && dish.Price <= MaxDatabaseMoney;

    private static decimal SumMoney(IEnumerable<decimal> values)
    {
        var total = 0m;
        foreach (var value in values)
            total = checked(total + value);
        return Money(total);
    }

    private static decimal Money(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
