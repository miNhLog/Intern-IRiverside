using System.Threading;
using System.Threading.Tasks;

namespace HeThongDatTiecCuoi_API.Services;

/// <summary>
/// Dịch vụ đồng bộ danh mục (Halls, Menus, Decors, Services) sang AI Microservice.
/// </summary>
public interface IAiCatalogSyncService
{
    /// <summary>
    /// Đồng bộ đồng bộ danh mục thực tế từ SQL Server sang AI Microservice (non-blocking failure).
    /// </summary>
    Task<bool> SyncCatalogToAiServiceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Kích hoạt đồng bộ nền (fire-and-forget) để không làm chậm luồng CRUD của Admin.
    /// </summary>
    void SyncCatalogInBackground();
}
