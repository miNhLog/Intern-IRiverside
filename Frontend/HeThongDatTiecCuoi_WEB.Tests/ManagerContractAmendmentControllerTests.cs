using System.Net;
using System.Net.Http.Headers;
using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace HeThongDatTiecCuoi_WEB.Tests;

public sealed class ManagerContractAmendmentControllerTests : IClassFixture<PaymentWebFactory>
{
    private readonly PaymentWebFactory _factory;

    public ManagerContractAmendmentControllerTests(PaymentWebFactory factory) => _factory = factory;

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(RoleNames.Admin, HttpStatusCode.Forbidden)]
    [InlineData(RoleNames.HallManager, HttpStatusCode.Forbidden)]
    [InlineData(RoleNames.Coordinator, HttpStatusCode.Forbidden)]
    [InlineData(RoleNames.Customer, HttpStatusCode.Forbidden)]
    public async Task AmendmentHistoryAndPdf_AreManagerOnly(string? role, HttpStatusCode expected)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        if (role is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Uc17Test", role);

        using var history = await client.GetAsync("/quan-ly/hop-dong-thanh-toan/contracts/1/amendments");
        using var pdf = await client.GetAsync($"/quan-ly/hop-dong-thanh-toan/contracts/1/amendments/{Guid.NewGuid():D}/pdf");

        Assert.Equal(expected, history.StatusCode);
        Assert.Equal(expected, pdf.StatusCode);
    }
}
