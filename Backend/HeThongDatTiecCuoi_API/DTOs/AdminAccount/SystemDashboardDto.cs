using HeThongDatTiecCuoi_API.DTOs.AuditLog;

namespace HeThongDatTiecCuoi_API.DTOs.AdminAccount;

public sealed class SystemDashboardDto
{
    public int TotalAccounts { get; init; }
    public int ActiveAccounts { get; init; }
    public int LockedAccounts { get; init; }
    public int MustChangePasswordEmployees { get; init; }
    public int TotalInternalEmployees { get; init; }
    public DateTime FromDate { get; init; }
    public DateTime ToDate { get; init; }
    public List<RoleDistributionDto> RoleDistribution { get; init; } = [];
    public List<DashboardAccountDto> RecentInternalAccounts { get; init; } = [];
    public List<DashboardAttentionAccountDto> AttentionAccounts { get; init; } = [];
    public List<AuditLogDto> RecentAuditLogs { get; init; } = [];
}

public sealed class RoleDistributionDto
{
    public string RoleName { get; init; } = string.Empty;
    public int Count { get; init; }
}

public class DashboardAccountDto
{
    public int UserId { get; init; }
    public string? EmployeeCode { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string RoleName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public sealed class DashboardAttentionAccountDto : DashboardAccountDto
{
    public string Issue { get; init; } = string.Empty;
    public DateTime? UpdatedAt { get; init; }
}
