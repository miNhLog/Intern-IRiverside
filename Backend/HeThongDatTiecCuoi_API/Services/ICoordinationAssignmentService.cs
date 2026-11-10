using HeThongDatTiecCuoi_API.DTOs.EventCoordination;

namespace HeThongDatTiecCuoi_API.Services;

public interface ICoordinationAssignmentService
{
    Task<ServiceResult<HallManagerCoordinationListDto>> GetHallManagerListAsync(int userId,
        HallManagerCoordinationQuery query, CancellationToken cancellationToken);
    Task<ServiceResult<List<CoordinatorAvailabilityDto>>> GetCoordinatorAvailabilityAsync(int userId,
        int bookingId, CancellationToken cancellationToken);
    Task<ServiceResult<HallManagerCoordinationDetailDto>> GetHallManagerDetailAsync(int userId,
        int bookingId, CancellationToken cancellationToken);
    Task<ServiceResult<CoordinationMutationResultDto>> AssignAsync(int userId, int bookingId,
        CreateCoordinationAssignmentRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<CoordinationMutationResultDto>> ChangeAsync(int userId, int bookingId,
        ChangeCoordinationAssignmentRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<CoordinationMutationResultDto>> CancelAsync(int userId, int bookingId,
        CancelCoordinationAssignmentRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<List<CoordinatorAssignedPartyDto>>> GetCoordinatorPartiesAsync(int userId,
        CancellationToken cancellationToken);
    Task SynchronizeBookingAssignmentsAsync(int bookingId, string bookingStatusCode, int? actorUserId,
        string? reason, CancellationToken cancellationToken);
    Task<bool> HasScheduleConflictAsync(int coordinatorEmployeeId, int hallScheduleId, int? excludedBookingId,
        CancellationToken cancellationToken);
}
