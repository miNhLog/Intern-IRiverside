using System.Reflection;
using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Controllers;
using HeThongDatTiecCuoi_WEB.Models.AdminReport;
using HeThongDatTiecCuoi_WEB.Models.Auth;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace HeThongDatTiecCuoi_WEB.Tests;

/// <summary>
/// UC23 – phạm vi Manager và việc truyền bộ lọc tháng/sảnh/ca từ WEB sang API.
/// Không khởi động app, không gọi API thật: API client được thay bằng DispatchProxy.
/// </summary>
public sealed class ReportUc23WebTests
{
    [Fact]
    public void AdminReportController_IsRestrictedToManagerRole()
    {
        var authorize = typeof(AdminReportController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(RoleNames.Manager, authorize!.Roles);
        Assert.DoesNotContain(RoleNames.Admin, authorize.Roles!);
    }

    [Fact]
    public async Task GetReportData_ForwardsMonthHallAndShiftToApiClient()
    {
        var (api, proxy) = CreateApi();
        var controller = CreateController(api);

        var result = await controller.GetReportData(
            year: 2026, quarter: 0, month: 10, hallId: 3, shift: "Ca tối");

        var json = Assert.IsType<JsonResult>(result);
        var success = json.Value!.GetType().GetProperty("success")!.GetValue(json.Value);
        Assert.Equal(true, success);

        Assert.Equal(2026, proxy.Year);
        Assert.Equal(0, proxy.Quarter);
        Assert.Equal(10, proxy.Month);
        Assert.Equal(3, proxy.HallId);
        Assert.Equal("Ca tối", proxy.Shift);
    }

    [Fact]
    public async Task Index_ForwardsMonthHallAndShiftToApiClient()
    {
        var (api, proxy) = CreateApi();
        var controller = CreateController(api);

        var result = await controller.Index(
            year: 2026, quarter: 2, month: 0, hallId: 1, shift: "Ca trưa");

        Assert.IsType<ViewResult>(result);
        Assert.Equal(2026, proxy.Year);
        Assert.Equal(2, proxy.Quarter);
        Assert.Equal(0, proxy.Month);
        Assert.Equal(1, proxy.HallId);
        Assert.Equal("Ca trưa", proxy.Shift);
    }

    [Fact]
    public void ReportView_ExposesMonthHallShiftFiltersAndHonestUnavailableMarkers()
    {
        var view = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "Frontend", "HeThongDatTiecCuoi_WEB", "Views", "AdminReport", "Index.cshtml"));

        Assert.Contains("id=\"selectMonth\"", view);
        Assert.Contains("id=\"selectHall\"", view);
        Assert.Contains("id=\"selectShift\"", view);
        Assert.Contains("onMonthChange()", view);
        Assert.Contains("onQuarterChange()", view);
        Assert.Contains("id=\"shiftOccupancyContainer\"", view);
        Assert.Contains("id=\"qualityContainer\"", view);
        Assert.Contains("Điểm vệ sinh: chưa có dữ liệu", view);
        Assert.Contains("Tiền cọc bị mất (UC18): chưa có sổ theo dõi", view);
    }

    [Fact]
    public void ReportView_SeparatesActualAndEstimatedChartDatasets()
    {
        var view = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "Frontend", "HeThongDatTiecCuoi_WEB", "Views", "AdminReport", "Index.cshtml"));

        Assert.Contains("estimatedRevenues", view);
        Assert.Contains("hallEstimatedRevs", view);
        Assert.Contains("'Thực thu (Triệu VNĐ)'", view);
        Assert.Contains("'Dự kiến (Triệu VNĐ)'", view);
    }

    private static (IRiversideApiClient Api, ReportApiProxy Proxy) CreateApi()
    {
        var proxy = DispatchProxy.Create<IRiversideApiClient, ReportApiProxy>();
        return ((IRiversideApiClient)proxy, (ReportApiProxy)(object)proxy);
    }

    private static AdminReportController CreateController(IRiversideApiClient api)
    {
        var context = new DefaultHttpContext();
        context.Request.Cookies = new RequestCookieCollection(new Dictionary<string, string>
        {
            ["rp_api_token"] = "test-token"
        });

        return new AdminReportController(api)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "WeddingManager.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    public class ReportApiProxy : DispatchProxy
    {
        public int? Year { get; private set; }
        public int? Quarter { get; private set; }
        public int? Month { get; private set; }
        public int? HallId { get; private set; }
        public string? Shift { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IRiversideApiClient.GetRevenueBiReportAsync))
                throw new NotSupportedException(targetMethod?.Name);

            Year = (int?)args![0];
            Quarter = (int?)args[1];
            Month = (int?)args[4];
            HallId = (int?)args[5];
            Shift = (string?)args[6];

            return Task.FromResult(ApiCallResult<RevenueBiViewModel>.Success(new RevenueBiViewModel
            {
                Year = Year!.Value,
                Quarter = Quarter!.Value,
                Month = Month!.Value,
                HallId = HallId,
                Shift = Shift
            }));
        }
    }

    private sealed class RequestCookieCollection : Dictionary<string, string>, IRequestCookieCollection
    {
        public RequestCookieCollection(IDictionary<string, string> cookies) : base(cookies)
        {
        }

        public new string? this[string key]
        {
            get => TryGetValue(key, out var value) ? value : null;
            set { }
        }

        public new ICollection<string> Keys => base.Keys;
    }
}
