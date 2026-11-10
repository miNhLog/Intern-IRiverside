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
    public const string LoginSuccess = "LOGIN_SUCCESS";
    public const string LoginFailed = "LOGIN_FAILED";
    public const string LoginBlocked = "LOGIN_BLOCKED";
    public const string Logout = "LOGOUT";
    public const string PasswordChanged = "PASSWORD_CHANGED";
    public const string PasswordResetCompleted = "PASSWORD_RESET_COMPLETED";
    public const string HallManagerAssigned = "HALL_MANAGER_ASSIGNED";
    public const string HallManagerAssignmentEnded = "HALL_MANAGER_ASSIGNMENT_ENDED";
    public const string ContractCreated = "CONTRACT_CREATED";
    public const string ContractUpdated = "CONTRACT_UPDATED";
    public const string ContractActivated = "CONTRACT_ACTIVATED";
    public const string ContractCompleted = "CONTRACT_COMPLETED";
    public const string ContractCancelled = "CONTRACT_CANCELLED";
    public const string IncidentCreated = "INCIDENT_CREATED";
    public const string IncidentUpdated = "INCIDENT_UPDATED";
    public const string IncidentStatusChanged = "INCIDENT_STATUS_CHANGED";
    public const string IncidentResolutionUpdated = "INCIDENT_RESOLUTION_UPDATED";
    public const string CoordinationAssigned = "COORDINATION_ASSIGNED";
    public const string CoordinationChanged = "COORDINATION_CHANGED";
    public const string CoordinationCancelled = "COORDINATION_CANCELLED";
    public const string CoordinationCompleted = "COORDINATION_COMPLETED";
    public const string BookingCreated = "BOOKING_CREATED";
    public const string BookingStatusChanged = "BOOKING_STATUS_CHANGED";
    public const string BookingCancelled = "BOOKING_CANCELLED";
    public const string BookingHoldCancelled = "BOOKING_HOLD_CANCELLED";
    public const string Payment = "PAYMENT";
    public const string PaymentRecorded = "PAYMENT_RECORDED";
    public const string HallScheduleStatusChanged = "HALL_SCHEDULE_STATUS_CHANGED";
    public const string HallPriceUpdated = "HALL_PRICE_UPDATED";
    public const string HallCreated = "HALL_CREATED";
    public const string HallUpdated = "HALL_UPDATED";
    public const string MenuCreated = "MENU_CREATED";
    public const string MenuUpdated = "MENU_UPDATED";
    public const string MenuDishesChanged = "MENU_DISHES_CHANGED";
    public const string DishCreated = "DISH_CREATED";
    public const string DishUpdated = "DISH_UPDATED";
    public const string DecorCreated = "DECOR_CREATED";
    public const string DecorUpdated = "DECOR_UPDATED";
    public const string ServiceCreated = "SERVICE_CREATED";
    public const string ServiceUpdated = "SERVICE_UPDATED";

    public const string ScheduleChangeCreated = "SCHEDULE_CHANGE_CREATED";
    public const string ScheduleChangeReview = "SCHEDULE_CHANGE_REVIEW";
    public const string ScheduleChangeQuoted = "SCHEDULE_CHANGE_QUOTED";
    public const string ScheduleChangeRejected = "SCHEDULE_CHANGE_REJECTED";
    public const string ScheduleChangeWithdrawn = "SCHEDULE_CHANGE_WITHDRAWN";
    public const string ScheduleChangeReconciled = "SCHEDULE_CHANGE_RECONCILED";
    public const string ScheduleChangeApplied = "SCHEDULE_CHANGE_APPLIED";
    public const string ScheduleChangeRefundConfirmed = "SCHEDULE_CHANGE_REFUND_CONFIRMED";

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
        ,LoginSuccess, LoginFailed, LoginBlocked, Logout, PasswordChanged, PasswordResetCompleted,
        BookingCreated, BookingStatusChanged, BookingCancelled, BookingHoldCancelled,
        ContractCreated, ContractUpdated, ContractActivated, ContractCompleted, ContractCancelled,
        Payment, PaymentRecorded,
        ScheduleChangeCreated, ScheduleChangeReview, ScheduleChangeQuoted, ScheduleChangeRejected,
        ScheduleChangeWithdrawn, ScheduleChangeReconciled, ScheduleChangeApplied, ScheduleChangeRefundConfirmed,
        HallScheduleStatusChanged, HallPriceUpdated, HallCreated, HallUpdated,
        MenuCreated, MenuUpdated, MenuDishesChanged, DishCreated, DishUpdated,
        DecorCreated, DecorUpdated, ServiceCreated, ServiceUpdated,
        HallManagerAssigned, HallManagerAssignmentEnded,
        CoordinationAssigned, CoordinationChanged, CoordinationCancelled, CoordinationCompleted,
        IncidentCreated, IncidentUpdated, IncidentStatusChanged, IncidentResolutionUpdated,
        "CANCEL"
    ];

    public static readonly string[] AccountActions =
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

    public static readonly string[] AuthenticationActions =
    [
        LoginSuccess, LoginFailed, LoginBlocked, Logout, PasswordChanged,
        PasswordResetCompleted, FirstPasswordChanged, AdminPasswordChanged
    ];

    public static readonly string[] BookingActions = [BookingCreated, BookingStatusChanged, BookingCancelled, BookingHoldCancelled, "CANCEL"];
    public static readonly string[] ContractActions = [ContractCreated, ContractUpdated, ContractActivated, ContractCompleted, ContractCancelled];
    public static readonly string[] PaymentActions = [Payment, PaymentRecorded, ScheduleChangeRefundConfirmed];
    public static readonly string[] ScheduleActions = [ScheduleChangeCreated, ScheduleChangeReview, ScheduleChangeQuoted, ScheduleChangeRejected, ScheduleChangeWithdrawn, ScheduleChangeReconciled, ScheduleChangeApplied, ScheduleChangeRefundConfirmed, HallScheduleStatusChanged];
    public static readonly string[] CatalogActions = [HallPriceUpdated, HallCreated, HallUpdated, MenuCreated, MenuUpdated, MenuDishesChanged, DishCreated, DishUpdated, DecorCreated, DecorUpdated, ServiceCreated, ServiceUpdated];
    public static readonly string[] WorkforceActions = [HallManagerAssigned, HallManagerAssignmentEnded, CoordinationAssigned, CoordinationChanged, CoordinationCancelled, CoordinationCompleted];
    public static readonly string[] IncidentActions = [IncidentCreated, IncidentUpdated, IncidentStatusChanged, IncidentResolutionUpdated];

    public static string[]? ForGroup(string? group) => group switch
    {
        "authentication" => AuthenticationActions,
        "account" => AccountActions,
        "booking" => BookingActions,
        "contract" => ContractActions,
        "payment" => PaymentActions,
        "schedule" => ScheduleActions,
        "catalog" => CatalogActions,
        "workforce" => WorkforceActions,
        "incident" => IncidentActions,
        null => AdminVisibleActions,
        _ => null
    };

    public static string GroupOf(string action) =>
        AuthenticationActions.Contains(action) ? "authentication" :
        AccountActions.Contains(action) ? "account" :
        BookingActions.Contains(action) ? "booking" :
        ContractActions.Contains(action) ? "contract" :
        PaymentActions.Contains(action) ? "payment" :
        ScheduleActions.Contains(action) ? "schedule" :
        CatalogActions.Contains(action) ? "catalog" :
        WorkforceActions.Contains(action) ? "workforce" :
        IncidentActions.Contains(action) ? "incident" : "other";
}

public static class AuditLogModes
{
    public const string Important = "important";
    public const string All = "all";
}

public static class AuditImportantGroups
{
    public const string Security = "security";
    public const string Finance = "finance";
    public const string Booking = "booking";
    public const string Operations = "operations";

    public static readonly string[] SecurityActions =
    [
        AuditActions.LoginBlocked,
        AuditActions.CreateAccount,
        AuditActions.LockAccount,
        AuditActions.UnlockAccount,
        AuditActions.ChangeRole,
        AuditActions.RoleChangeApproved,
        AuditActions.RoleChangeRejected,
        AuditActions.ResetPassword
    ];

    public static readonly string[] FinanceActions =
    [
        AuditActions.Payment,
        AuditActions.PaymentRecorded,
        AuditActions.ScheduleChangeRefundConfirmed,
        AuditActions.ContractActivated,
        AuditActions.ContractCancelled
    ];

    public static readonly string[] BookingActions =
    [
        AuditActions.BookingCancelled,
        AuditActions.BookingHoldCancelled,
        "CANCEL",
        AuditActions.BookingStatusChanged,
        AuditActions.ScheduleChangeApplied,
        AuditActions.HallScheduleStatusChanged
    ];

    public static readonly string[] OperationsActions =
    [
        AuditActions.HallPriceUpdated,
        AuditActions.HallManagerAssigned,
        AuditActions.HallManagerAssignmentEnded,
        AuditActions.IncidentCreated,
        AuditActions.IncidentStatusChanged
    ];

    public static readonly string[] AllActions =
        SecurityActions.Concat(FinanceActions).Concat(BookingActions).Concat(OperationsActions).Distinct().ToArray();

    public static readonly string[] RoleChangeActions =
    [
        AuditActions.ChangeRole,
        AuditActions.RoleChangeRequested,
        AuditActions.RoleChangeApproved,
        AuditActions.RoleChangeRejected
    ];

    // Sensitive actions are deliberately limited to account/security operations.
    // LOGIN_BLOCKED is counted regardless of outcome; other actions require FAILED/BLOCKED.
    public static readonly string[] SensitiveActions = SecurityActions;

    public static string[]? ForGroup(string? group) => group switch
    {
        Security => SecurityActions,
        Finance => FinanceActions,
        Booking => BookingActions,
        Operations => OperationsActions,
        null => AllActions,
        _ => null
    };

    public static bool IsImportant(string action) => AllActions.Contains(action);

    public static bool RequiresReview(string action, string? outcome) =>
        action == AuditActions.LoginBlocked ||
        (SensitiveActions.Contains(action) && outcome is AuditOutcomes.Failed or AuditOutcomes.Blocked);
}

public static class AuditOutcomes
{
    public const string Success = "SUCCESS";
    public const string Failed = "FAILED";
    public const string Blocked = "BLOCKED";
}

public static class AuditAuthMethods
{
    public const string Password = "PASSWORD";
    public const string Google = "GOOGLE";
    public const string Jwt = "JWT";
    public const string ResetToken = "RESET_TOKEN";
}

public static class AuditEntityNames
{
    public const string User = "User";
    public const string RoleChangeRequest = "RoleChangeRequest";
    public const string HallManagerAssignment = "HallManagerAssignment";
    public const string Contract = "Contract";
    public const string Incident = "Incident";
    public const string CoordinationAssignment = "CoordinationAssignment";
    public const string Booking = "DatTiec";
    public const string Payment = "Payment";
    public const string HallSchedule = "HallSchedule";
    public const string ScheduleChange = "ScheduleChange";
    public const string ScheduleChangeRefund = "ScheduleChangeRefund";
    public const string Hall = "Hall";
    public const string Menu = "Menu";
    public const string Dish = "Dish";
    public const string DecorPackage = "DecorPackage";
    public const string Service = "Service";
}
