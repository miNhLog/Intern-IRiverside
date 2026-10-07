using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_WEB.Models.RoleChangeRequest;

public sealed class RoleChangeCandidateViewModel
{
    public int EmployeeId { get; set; }
    public int UserId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public byte CurrentRoleId { get; set; }
    public string CurrentRoleName { get; set; } = string.Empty;
    public byte RequestedRoleId { get; set; }
    public string RequestedRoleName { get; set; } = string.Empty;
    public string AccountStatus { get; set; } = string.Empty;
}

public sealed class RoleChangeRequestViewModel
{
    public int RoleChangeRequestId { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string? EmployeeCodeBefore { get; set; }
    public string? EmployeeCodeAfter { get; set; }
    public string FullName { get; set; } = string.Empty;
    public byte CurrentRoleId { get; set; }
    public string CurrentRoleName { get; set; } = string.Empty;
    public byte RequestedRoleId { get; set; }
    public string RequestedRoleName { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? ReviewedBy { get; set; }
}

public sealed class CreateRoleChangeRequestViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn nhân viên.")]
    [Display(Name = "Nhân viên")]
    public int EmployeeId { get; set; }

    [StringLength(500, ErrorMessage = "Lý do thay đổi không được vượt quá 500 ký tự.")]
    [Display(Name = "Lý do thay đổi")]
    public string? Reason { get; set; }
}

public sealed class ManagerRoleChangeRequestPageViewModel
{
    public CreateRoleChangeRequestViewModel Form { get; set; } = new();
    public List<RoleChangeCandidateViewModel> Candidates { get; set; } = [];
    public List<RoleChangeRequestViewModel> Requests { get; set; } = [];
    public string? ErrorMessage { get; set; }
}

public sealed class AdminRoleChangeRequestViewModel
{
    public int RoleChangeRequestId { get; set; }
    public int EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? EmployeeCodeBefore { get; set; }
    public string? EmployeeCodeAfter { get; set; }
    public byte CurrentRoleId { get; set; }
    public string CurrentRoleName { get; set; } = string.Empty;
    public byte RequestedRoleId { get; set; }
    public string RequestedRoleName { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public int RequestedByUserId { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public int? ReviewedByUserId { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
    public bool CanApprove { get; set; }
    public string? BlockingReason { get; set; }
}

public sealed class AdminRoleChangeRequestPageViewModel
{
    public List<AdminRoleChangeRequestViewModel> Requests { get; set; } = [];
    public string? Status { get; set; }
    public string? Keyword { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class RejectRoleChangeRequestViewModel
{
    [Required(ErrorMessage = "Lý do từ chối là bắt buộc.")]
    [StringLength(500, ErrorMessage = "Lý do từ chối không được vượt quá 500 ký tự.")]
    public string RejectionReason { get; set; } = string.Empty;
}
