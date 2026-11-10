using HeThongDatTiecCuoi_API.DTOs.Dashboard;
using HeThongDatTiecCuoi_API.Models;

namespace HeThongDatTiecCuoi_API.Services;

public interface IHallManagerDashboardService
{
    Task<ServiceResult<HallManagerDashboardDto>> GetAsync(int userId, CancellationToken cancellationToken);
    Task<ServiceResult<HallManagerScheduleDto>> GetScheduleAsync(int userId, DateTime date,
        CancellationToken cancellationToken);
}
