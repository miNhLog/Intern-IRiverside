using HeThongDatTiecCuoi_API.DTOs.Incident;

namespace HeThongDatTiecCuoi_API.Services;

public interface IIncidentService
{
    Task<ServiceResult<IncidentListResponseDto>> GetListAsync(int userId, string role, string? search,
        string? status, string? severity, int? hallId, DateTime? fromDate, DateTime? toDate,
        int page, int pageSize, CancellationToken cancellationToken);
    Task<ServiceResult<List<IncidentBookingOptionDto>>> GetBookingOptionsAsync(int userId, string role,
        CancellationToken cancellationToken);
    Task<ServiceResult<IncidentDetailDto>> GetDetailAsync(int userId, string role, int incidentId,
        CancellationToken cancellationToken);
    Task<ServiceResult<IncidentDetailDto>> CreateAsync(int userId, string role, CreateIncidentRequest request,
        CancellationToken cancellationToken);
    Task<ServiceResult<IncidentDetailDto>> UpdateAsync(int userId, string role, int incidentId,
        UpdateIncidentRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<IncidentDetailDto>> UpdateStatusAsync(int userId, string role, int incidentId,
        UpdateIncidentStatusRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<IncidentDetailDto>> UpdateResolutionAsync(int userId, string role, int incidentId,
        UpdateIncidentResolutionRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<List<IncidentHistoryItemDto>>> GetHistoryAsync(int userId, string role, int incidentId,
        CancellationToken cancellationToken);
}
