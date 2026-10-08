namespace HeThongDatTiecCuoi_API.Constants;

public static class AuditActions
{
    public const string CreateAccount = "CREATE_ACCOUNT";
    public const string UpdateAccount = "UPDATE_ACCOUNT";
    public const string LockAccount = "LOCK_ACCOUNT";
    public const string UnlockAccount = "UNLOCK_ACCOUNT";
    public const string ChangeRole = "CHANGE_ROLE";
    public const string ResetPassword = "RESET_PASSWORD";
    public const string RoleChangeRequested = "ROLE_CHANGE_REQUESTED";
    public const string RoleChangeApproved = "ROLE_CHANGE_APPROVED";
    public const string RoleChangeRejected = "ROLE_CHANGE_REJECTED";
    public const string FirstPasswordChanged = "FIRST_PASSWORD_CHANGED";
    public const string AdminPasswordChanged = "ADMIN_PASSWORD_CHANGED";
    public const string HallManagerAssigned = "HALL_MANAGER_ASSIGNED";
    public const string HallManagerAssignmentEnded = "HALL_MANAGER_ASSIGNMENT_ENDED";

    public static readonly string[] AdminVisibleActions =
    [
        CreateAccount,
        UpdateAccount,
        LockAccount,
        UnlockAccount,
        ChangeRole,
        ResetPassword,
        RoleChangeRequested,
        RoleChangeApproved,
        RoleChangeRejected,
        FirstPasswordChanged,
        AdminPasswordChanged
    ];

    public static readonly string[] AccountActions =
    [
        CreateAccount,
        UpdateAccount,
        LockAccount,
        UnlockAccount,
        ChangeRole,
        ResetPassword,
        FirstPasswordChanged,
        AdminPasswordChanged
    ];
}

public static class AuditEntityNames
{
    public const string User = "User";
    public const string RoleChangeRequest = "RoleChangeRequest";
    public const string HallManagerAssignment = "HallManagerAssignment";
}
