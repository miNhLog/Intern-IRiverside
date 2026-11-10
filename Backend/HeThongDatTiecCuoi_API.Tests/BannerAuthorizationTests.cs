using System.Reflection;
using HeThongDatTiecCuoi_API.Controllers;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class BannerAuthorizationTests
{
    [Fact]
    public void Public_list_is_anonymous()
    {
        var method = typeof(BannersController).GetMethod(nameof(BannersController.GetPublic));
        Assert.NotNull(method?.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Theory]
    [InlineData(nameof(BannersController.GetAll))]
    [InlineData(nameof(BannersController.Create))]
    [InlineData(nameof(BannersController.Update))]
    [InlineData(nameof(BannersController.UploadImage))]
    [InlineData(nameof(BannersController.Reorder))]
    [InlineData(nameof(BannersController.Visibility))]
    [InlineData(nameof(BannersController.Delete))]
    public void Management_endpoints_require_manager_role(string methodName)
    {
        var methods = typeof(BannersController).GetMethods().Where(x => x.Name == methodName);
        var authorize = Assert.Single(methods).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorize);
        Assert.Equal(RoleNames.Manager, authorize!.Roles);
    }

    [Fact]
    public void Controller_uses_expected_api_route()
    {
        var route = typeof(BannersController).GetCustomAttribute<RouteAttribute>();
        Assert.Equal("api/banners", route?.Template);
    }
}
