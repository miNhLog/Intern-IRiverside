using System.Reflection;
using HeThongDatTiecCuoi_WEB.Controllers;
using HeThongDatTiecCuoi_WEB.Models.AdminAccount;
using HeThongDatTiecCuoi_WEB.Models.Auth;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace HeThongDatTiecCuoi_WEB.Tests;

public sealed class AdminAuditLogControllerTests
{
    [Fact]
    public async Task Index_DoesNotCallApiForMalformedDate()
    {
        var api = DispatchProxy.Create<IRiversideApiClient, AuditLogApiProxy>();
        var controller = CreateController(api);
        controller.ModelState.AddModelError("fromDate", "Invalid date.");

        var result = Assert.IsType<ViewResult>(await controller.Index(fromDate: null));

        Assert.False(((AuditLogApiProxy)(object)api).Called);
        var model = Assert.IsType<AuditLogPageViewModel>(result.Model);
        Assert.Contains("Ngày lọc không hợp lệ", model.ErrorMessage);
        Assert.False(model.IsLoadError);
    }

    [Fact]
    public async Task Index_ExposesApiFailureWithoutMarkingQueryAsSuccessful()
    {
        var api = DispatchProxy.Create<IRiversideApiClient, AuditLogApiProxy>();
        ((AuditLogApiProxy)(object)api).Result = ApiCallResult<AuditLogPageViewModel>.Failure("API unavailable", 503);
        var controller = CreateController(api);

        var result = Assert.IsType<ViewResult>(await controller.Index());
        var model = Assert.IsType<AuditLogPageViewModel>(result.Model);

        Assert.Equal("API unavailable", model.ErrorMessage);
        Assert.Empty(model.Items);
        Assert.True(model.IsLoadError);
    }

    [Fact]
    public async Task Index_ForwardsEntityFilterToApi()
    {
        var api = DispatchProxy.Create<IRiversideApiClient, AuditLogApiProxy>();
        var controller = CreateController(api);

        await controller.Index(entityName: "RoleChangeRequest");

        Assert.Equal("RoleChangeRequest", ((AuditLogApiProxy)(object)api).EntityName);
    }

    private static AdminAuditLogController CreateController(IRiversideApiClient api)
    {
        var controller = new AdminAuditLogController(api)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.Request.Headers.Cookie = "rp_api_token=test-token";
        return controller;
    }

    public class AuditLogApiProxy : DispatchProxy
    {
        public bool Called { get; private set; }
        public string? EntityName { get; private set; }
        public ApiCallResult<AuditLogPageViewModel> Result { get; set; } = ApiCallResult<AuditLogPageViewModel>.Success(new());

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IRiversideApiClient.GetAuditLogsAsync))
            {
                Called = true;
                EntityName = (string?)args?[4];
                return Task.FromResult(Result);
            }

            throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
