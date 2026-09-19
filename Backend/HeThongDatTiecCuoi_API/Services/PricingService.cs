using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Pricing;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Services;

public sealed class PricingService : IPricingService
{
    private readonly ApplicationDbContext _db;

    public PricingService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PricingResponseDto> GetAllAsync(CancellationToken cancellationToken)
    {
        var categories = new List<PricingCategoryResponseDto>(PricingCategoryCatalog.All.Count);

        foreach (var category in PricingCategoryCatalog.All)
        {
            categories.Add(await GetCategoryAsync(category, cancellationToken));
        }

        return new PricingResponseDto
        {
            Currency = "VND",
            Categories = categories
        };
    }

    public Task<PricingCategoryResponseDto> GetCategoryAsync(
        string category,
        CancellationToken cancellationToken)
    {
        var normalizedCategory = PricingCategoryCatalog.Normalize(category);

        return normalizedCategory switch
        {
            PricingCategoryCatalog.Halls => GetHallsAsync(cancellationToken),
            PricingCategoryCatalog.Menus => GetMenusAsync(cancellationToken),
            PricingCategoryCatalog.DecorPackages => GetDecorPackagesAsync(cancellationToken),
            PricingCategoryCatalog.Services => GetServicesAsync(cancellationToken),
            _ => throw PricingServiceException.InvalidCategory()
        };
    }

    public Task<PricingItemDto> UpdatePriceAsync(
        string category,
        int id,
        decimal price,
        decimal expectedPrice,
        CancellationToken cancellationToken)
    {
        var normalizedCategory = PricingCategoryCatalog.Normalize(category);

        return normalizedCategory switch
        {
            PricingCategoryCatalog.Halls => UpdateHallAsync(id, price, expectedPrice, cancellationToken),
            PricingCategoryCatalog.Menus => UpdateMenuAsync(id, price, expectedPrice, cancellationToken),
            PricingCategoryCatalog.DecorPackages => UpdateDecorPackageAsync(id, price, expectedPrice, cancellationToken),
            PricingCategoryCatalog.Services => UpdateServiceAsync(id, price, expectedPrice, cancellationToken),
            _ => throw PricingServiceException.InvalidCategory()
        };
    }

    private async Task<PricingCategoryResponseDto> GetHallsAsync(CancellationToken cancellationToken)
    {
        var items = await _db.SanhTiec
            .AsNoTracking()
            .OrderBy(x => x.SanhTiecID)
            .Select(x => new PricingItemDto
            {
                Id = x.SanhTiecID,
                Code = x.MaSanh,
                Name = x.TenSanh,
                Price = x.GiaThue,
                Status = x.TrangThai
            })
            .ToListAsync(cancellationToken);

        return CreateCategory(PricingCategoryCatalog.Halls, "Halls", items);
    }

    private async Task<PricingCategoryResponseDto> GetMenusAsync(CancellationToken cancellationToken)
    {
        var items = await _db.ThucDon
            .AsNoTracking()
            .OrderBy(x => x.ThucDonID)
            .Select(x => new PricingItemDto
            {
                Id = x.ThucDonID,
                Code = x.MaThucDon,
                Name = x.TenThucDon,
                Price = x.GiaMoiBan,
                Status = x.TrangThai
            })
            .ToListAsync(cancellationToken);

        return CreateCategory(PricingCategoryCatalog.Menus, "Menus", items);
    }

    private async Task<PricingCategoryResponseDto> GetDecorPackagesAsync(CancellationToken cancellationToken)
    {
        var items = await _db.GoiTrangTri
            .AsNoTracking()
            .OrderBy(x => x.GoiTrangTriID)
            .Select(x => new PricingItemDto
            {
                Id = x.GoiTrangTriID,
                Code = x.MaGoi,
                Name = x.TenGoi,
                Price = x.Gia,
                Status = x.TrangThai
            })
            .ToListAsync(cancellationToken);

        return CreateCategory(PricingCategoryCatalog.DecorPackages, "Decor packages", items);
    }

    private async Task<PricingCategoryResponseDto> GetServicesAsync(CancellationToken cancellationToken)
    {
        var items = await _db.DichVu
            .AsNoTracking()
            .OrderBy(x => x.DichVuID)
            .Select(x => new PricingItemDto
            {
                Id = x.DichVuID,
                Code = x.MaDichVu,
                Name = x.TenDichVu,
                Price = x.Gia,
                Status = x.TrangThai
            })
            .ToListAsync(cancellationToken);

        return CreateCategory(PricingCategoryCatalog.Services, "Services", items);
    }

    private async Task<PricingItemDto> UpdateHallAsync(
        int id,
        decimal price,
        decimal expectedPrice,
        CancellationToken cancellationToken)
    {
        var affected = await _db.SanhTiec
            .Where(x => x.SanhTiecID == id && x.GiaThue == expectedPrice)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.GiaThue, price),
                cancellationToken);

        if (affected == 0)
        {
            EnsureCurrentPriceUpdateCanProceed(
                await _db.SanhTiec.AnyAsync(x => x.SanhTiecID == id, cancellationToken),
                "hall");
        }

        var item = await _db.SanhTiec
            .AsNoTracking()
            .Where(x => x.SanhTiecID == id)
            .Select(x => new PricingItemDto
            {
                Id = x.SanhTiecID,
                Code = x.MaSanh,
                Name = x.TenSanh,
                Price = x.GiaThue,
                Status = x.TrangThai
            })
            .SingleOrDefaultAsync(cancellationToken);

        return item is null
            ? throw PricingServiceException.NotFound("hall")
            : NormalizeItem(item);
    }

    private async Task<PricingItemDto> UpdateMenuAsync(
        int id,
        decimal price,
        decimal expectedPrice,
        CancellationToken cancellationToken)
    {
        var affected = await _db.ThucDon
            .Where(x => x.ThucDonID == id && x.GiaMoiBan == expectedPrice)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.GiaMoiBan, price),
                cancellationToken);

        if (affected == 0)
        {
            EnsureCurrentPriceUpdateCanProceed(
                await _db.ThucDon.AnyAsync(x => x.ThucDonID == id, cancellationToken),
                "menu");
        }

        var item = await _db.ThucDon
            .AsNoTracking()
            .Where(x => x.ThucDonID == id)
            .Select(x => new PricingItemDto
            {
                Id = x.ThucDonID,
                Code = x.MaThucDon,
                Name = x.TenThucDon,
                Price = x.GiaMoiBan,
                Status = x.TrangThai
            })
            .SingleOrDefaultAsync(cancellationToken);

        return item is null
            ? throw PricingServiceException.NotFound("menu")
            : NormalizeItem(item);
    }

    private async Task<PricingItemDto> UpdateDecorPackageAsync(
        int id,
        decimal price,
        decimal expectedPrice,
        CancellationToken cancellationToken)
    {
        var affected = await _db.GoiTrangTri
            .Where(x => x.GoiTrangTriID == id && x.Gia == expectedPrice)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.Gia, price),
                cancellationToken);

        if (affected == 0)
        {
            EnsureCurrentPriceUpdateCanProceed(
                await _db.GoiTrangTri.AnyAsync(x => x.GoiTrangTriID == id, cancellationToken),
                "decor package");
        }

        var item = await _db.GoiTrangTri
            .AsNoTracking()
            .Where(x => x.GoiTrangTriID == id)
            .Select(x => new PricingItemDto
            {
                Id = x.GoiTrangTriID,
                Code = x.MaGoi,
                Name = x.TenGoi,
                Price = x.Gia,
                Status = x.TrangThai
            })
            .SingleOrDefaultAsync(cancellationToken);

        return item is null
            ? throw PricingServiceException.NotFound("decor package")
            : NormalizeItem(item);
    }

    private async Task<PricingItemDto> UpdateServiceAsync(
        int id,
        decimal price,
        decimal expectedPrice,
        CancellationToken cancellationToken)
    {
        var affected = await _db.DichVu
            .Where(x => x.DichVuID == id && x.Gia == expectedPrice)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.Gia, price),
                cancellationToken);

        if (affected == 0)
        {
            EnsureCurrentPriceUpdateCanProceed(
                await _db.DichVu.AnyAsync(x => x.DichVuID == id, cancellationToken),
                "service");
        }

        var item = await _db.DichVu
            .AsNoTracking()
            .Where(x => x.DichVuID == id)
            .Select(x => new PricingItemDto
            {
                Id = x.DichVuID,
                Code = x.MaDichVu,
                Name = x.TenDichVu,
                Price = x.Gia,
                Status = x.TrangThai
            })
            .SingleOrDefaultAsync(cancellationToken);

        return item is null
            ? throw PricingServiceException.NotFound("service")
            : NormalizeItem(item);
    }

    private static void EnsureCurrentPriceUpdateCanProceed(
        bool exists,
        string categoryName)
    {
        if (!exists)
        {
            throw PricingServiceException.NotFound(categoryName);
        }

        throw PricingServiceException.Conflict();
    }

    private static PricingCategoryResponseDto CreateCategory(
        string category,
        string displayName,
        IEnumerable<PricingItemDto> items) => new()
        {
            Category = TextNormalization.Required(category),
            DisplayName = TextNormalization.Required(displayName),
            Items = items.Select(NormalizeItem).ToList()
        };

    private static PricingItemDto NormalizeItem(PricingItemDto item) => new()
    {
        Id = item.Id,
        Code = TextNormalization.Required(item.Code),
        Name = TextNormalization.Required(item.Name),
        Price = item.Price,
        Status = TextNormalization.Required(item.Status)
    };
}

public static class PricingCategoryCatalog
{
    public const string Halls = "halls";
    public const string Menus = "menus";
    public const string DecorPackages = "decor-packages";
    public const string Services = "services";

    public static IReadOnlyList<string> All { get; } =
        [Halls, Menus, DecorPackages, Services];

    public static string Normalize(string? category) =>
        category?.Trim().ToLowerInvariant() ?? string.Empty;
}

public sealed class PricingServiceException : Exception
{
    private PricingServiceException(string message, int statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }

    public static PricingServiceException InvalidCategory() =>
        new("The pricing category is not supported.", StatusCodes.Status400BadRequest);

    public static PricingServiceException NotFound(string categoryName) =>
        new($"The requested {categoryName} pricing item was not found.", StatusCodes.Status404NotFound);

    public static PricingServiceException Conflict() =>
        new("The price was changed by another request. Reload the pricing list and try again.", StatusCodes.Status409Conflict);
}
