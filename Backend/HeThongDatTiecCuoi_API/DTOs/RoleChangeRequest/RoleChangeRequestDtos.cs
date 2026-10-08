using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.RoleChangeRequest;

public sealed class RoleChangeCandidateDto
{
    public int EmployeeId { get; init; }
    public int UserId { get; init; }
    public string EmployeeCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public byte CurrentRoleId { get; init; }
    public string CurrentRoleName { get; init; } = string.Empty;
    public byte RequestedRoleId { get; init; }
    public string RequestedRoleName { get; init; } = string.Empty;
    public string AccountStatus { get; init; } = string.Empty;
}

public sealed class CreateRoleChangeRequestDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Nhân viên không hợp lệ.")]
    public int EmployeeId { get; init; }

    [StringLength(500, ErrorMessage = "Lý do thay đổi không được vượt quá 500 ký tự.")]
    public string? Reason { get; init; }
}

public sealed class RoleChangeRequestDto
{
    public int RoleChangeRequestId { get; init; }
    public int EmployeeId { get; init; }
    public string EmployeeCode { get; init; } = string.Empty;
    public string? EmployeeCodeBefore { get; init; }
    public string? EmployeeCodeAfter { get; init; }
    public string FullName { get; init; } = string.Empty;
    public byte CurrentRoleId { get; init; }
    public string CurrentRoleName { get; init; } = string.Empty;
    public byte RequestedRoleId { get; init; }
    public string RequestedRoleName { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public string? RejectionReason { get; init; }
    public string? ReviewedBy { get; init; }
}

public sealed class AdminRoleChangeRequestDto
{
    public int RoleChangeRequestId { get; init; }
    public int EmployeeId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? EmployeeCodeBefore { get; init; }
    public string? EmployeeCodeAfter { get; init; }
    public byte CurrentRoleId { get; init; }
    public string CurrentRoleName { get; init; } = string.Empty;
    public byte RequestedRoleId { get; init; }
    public string RequestedRoleName { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; }
    public int RequestedByUserId { get; init; }
    public string RequestedByName { get; init; } = string.Empty;
    public int? ReviewedByUserId { get; init; }
    public string? ReviewedByName { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public string? RejectionReason { get; init; }
    public bool CanApprove { get; set; }
    public string? BlockingReason { get; set; }
}

public sealed class RejectRoleChangeRequestDto
{
    [Required(ErrorMessage = "Lý do từ chối là bắt buộc.")]
    [StringLength(500, ErrorMessage = "Lý do từ chối không được vượt quá 500 ký tự.")]
    public string RejectionReason { get; init; } = string.Empty;
}
