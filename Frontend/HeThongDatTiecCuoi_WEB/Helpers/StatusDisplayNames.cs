using HeThongDatTiecCuoi_WEB.Constants.StatusCodes;

namespace HeThongDatTiecCuoi_WEB.Helpers;

public static class StatusDisplayNames
{
    public static string GetAccountStatusName(string? status) => status switch
    {
        AccountStatusCodes.Active => "Hoạt động",
        AccountStatusCodes.Locked => "Bị khóa",
        _ => status ?? string.Empty
    };

    public static string GetEmployeeStatusName(string? status) => status switch
    {
        EmployeeStatusCodes.Active => "Đang làm việc",
        EmployeeStatusCodes.OnLeave => "Tạm nghỉ",
        EmployeeStatusCodes.Terminated => "Đã nghỉ việc",
        _ => status ?? string.Empty
    };

    public static string GetHallStatusName(string? status) => status switch
    {
        HallStatusCodes.Active => "Hoạt động",
        HallStatusCodes.Maintenance => "Bảo trì",
        HallStatusCodes.Inactive => "Ngừng hoạt động",
        _ => status ?? string.Empty
    };

    public static string GetHallScheduleStatusName(string? status) => status switch
    {
        HallScheduleStatusCodes.Available => "Trống",
        HallScheduleStatusCodes.Locked => "Tạm khóa",
        HallScheduleStatusCodes.Booked => "Đã đặt",
        _ => status ?? string.Empty
    };

    public static string GetMenuStatusName(string? status) => status switch
    {
        MenuStatusCodes.Active => "Áp dụng",
        MenuStatusCodes.Inactive => "Ngừng áp dụng",
        _ => status ?? string.Empty
    };

    public static string GetDishStatusName(string? status) => status switch
    {
        DishStatusCodes.Active => "Đang phục vụ",
        DishStatusCodes.Inactive => "Ngừng phục vụ",
        _ => status ?? string.Empty
    };
}
