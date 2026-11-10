using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HeThongDatTiecCuoi_API.Services;

public sealed class AiCatalogSyncService : IAiCatalogSyncService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiServiceOptions _options;
    private readonly ILogger<AiCatalogSyncService> _logger;

    public AiCatalogSyncService(
        IServiceScopeFactory serviceScopeFactory,
        IHttpClientFactory httpClientFactory,
        IOptions<AiServiceOptions> options,
        ILogger<AiCatalogSyncService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> SyncCatalogToAiServiceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var existingDataStatusId = await context.DataStatuses.AsNoTracking()
                .Where(ds => ds.DataStatusCode == DataStatusCodes.Existing)
                .Select(ds => ds.DataStatusId)
                .FirstOrDefaultAsync(cancellationToken);

            var activeHalls = await context.Halls.AsNoTracking()
                .Where(h => h.DataStatusId == existingDataStatusId &&
                            h.Status.StatusGroup == StatusGroups.Hall &&
                            h.Status.StatusCode == HallStatusCodes.Active)
                .OrderBy(h => h.HallId)
                .Select(h => new
                {
                    hall_id = h.HallId,
                    hall_code = h.HallCode,
                    hall_name = h.HallName,
                    min_guests = h.MinimumCapacity ?? 150,
                    max_guests = h.MaximumCapacity,
                    min_tables = (h.MinimumCapacity ?? 150) / 10,
                    max_tables = h.MaximumCapacity / 10,
                    optimal_tables = Math.Max(1, (int)Math.Round((h.MaximumCapacity / 10.0) * 0.8)),
                    base_price = (long)h.RentalPrice,
                    max_capacity = h.MaximumCapacity,
                    description = h.Description ?? string.Empty
                })
                .ToListAsync(cancellationToken);

            var activeMenus = await context.Menus.AsNoTracking()
                .Where(m => m.DataStatusId == existingDataStatusId &&
                            m.Status.StatusGroup == StatusGroups.Menu &&
                            m.Status.StatusCode == MenuStatusCodes.Active &&
                            m.MenuType == MenuTypes.Standard)
                .OrderBy(m => m.MenuId)
                .Select(m => new
                {
                    menu_id = m.MenuId,
                    menu_code = m.MenuCode,
                    menu_name = m.MenuName,
                    menu_type = m.MenuType,
                    price_per_table = (long)m.PricePerTable,
                    description = m.Description ?? string.Empty,
                    dishes = m.MenuDishes
                        .Where(md => md.DataStatusId == existingDataStatusId &&
                                     md.Dish.DataStatusId == existingDataStatusId)
                        .OrderBy(md => md.SortOrder)
                        .Select(md => md.Dish.DishName)
                        .ToList()
                })
                .ToListAsync(cancellationToken);

            var activeDecors = await context.DecorPackages.AsNoTracking()
                .Where(d => d.DataStatusId == existingDataStatusId &&
                            d.Status.StatusGroup == StatusGroups.DecorationPackage &&
                            d.Status.StatusCode == BusinessStatusCodes.Active)
                .OrderBy(d => d.GoiTrangTriID)
                .Select(d => new
                {
                    decor_id = d.GoiTrangTriID,
                    decor_name = d.TenGoi,
                    style = d.PhongCach,
                    price = (long)d.Gia,
                    description = d.MoTa ?? string.Empty
                })
                .ToListAsync(cancellationToken);

            var activeServices = await context.ServiceItems.AsNoTracking()
                .Where(s => s.DataStatusId == existingDataStatusId &&
                            s.Status.StatusGroup == StatusGroups.Service &&
                            s.Status.StatusCode == BusinessStatusCodes.Active)
                .OrderBy(s => s.MaDichVu)
                .Select(s => new
                {
                    service_id = s.MaDichVu,
                    service_name = s.TenDichVu,
                    service_type = s.LoaiDichVu,
                    description = s.MoTa ?? string.Empty,
                    price = (long)s.Gia
                })
                .ToListAsync(cancellationToken);

            var styleGroups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["romantic"] = new List<string> { "Lãng mạn", "Hiện đại / lãng mạn", "Sân vườn / lãng mạn", "Cổ tích / lãng mạn" },
                ["gentle"] = new List<string> { "Nhẹ nhàng" },
                ["nature"] = new List<string> { "Thiên nhiên / ngoài trời", "Sân vườn" }
            };

            var payload = new
            {
                meta = new
                {
                    venue = "Riverside Palace",
                    version = "2.1.0",
                    source = "Đồng bộ thời gian thực từ Backend (.NET 8 ApplicationDbContext)"
                },
                halls = activeHalls,
                menus = activeMenus,
                decors = activeDecors,
                services = activeServices,
                style_groups = styleGroups
            };

            var client = _httpClientFactory.CreateClient("AiService");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds > 0 ? _options.TimeoutSeconds : 5));

            using var response = await client.PostAsJsonAsync("api/v1/ai/catalog/reload", payload, cts.Token);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "[Catalog Sync] Đồng bộ danh mục sang AI Service thành công: {Halls} sảnh, {Menus} menu, {Decors} decor, {Services} dịch vụ.",
                    activeHalls.Count, activeMenus.Count, activeDecors.Count, activeServices.Count);
                return true;
            }

            var errBody = await response.Content.ReadAsStringAsync(cts.Token);
            _logger.LogWarning(
                "[Catalog Sync] AI Service trả lỗi ({StatusCode}): {Body}",
                response.StatusCode, errBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[Catalog Sync] Không thể đồng bộ danh mục sang AI Service ({BaseUrl}): {Message}. AI Service có thể đang ngoại tuyến.",
                _options.BaseUrl, ex.Message);
            return false;
        }
    }

    public void SyncCatalogInBackground()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await SyncCatalogToAiServiceAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "[Catalog Sync] Lỗi trong tác vụ đồng bộ nền sang AI Service: {Message}",
                    ex.Message);
            }
        });
    }
}
