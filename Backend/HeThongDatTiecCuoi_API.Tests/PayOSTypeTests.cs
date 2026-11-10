using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public class PayOSTypeTests
{
    [Fact]
    public void VerifyPayOSTypesAndRequestBuilding()
    {
        var client = new PayOSClient("test-cid", "test-key", "test-checksum");
        var item = new PaymentLinkItem
        {
            Name = "Coc tiec DT-20261021-0001",
            Quantity = 1,
            Price = 2000
        };
        var req = new CreatePaymentLinkRequest
        {
            OrderCode = 123456789,
            Amount = 2000,
            Description = "RSP 101",
            CancelUrl = "https://localhost/cancel",
            ReturnUrl = "https://localhost/return",
            Items = new List<PaymentLinkItem> { item }
        };

        Assert.NotNull(client);
        Assert.Equal(123456789, req.OrderCode);
        Assert.Equal(2000, req.Amount);
        Assert.Single(req.Items);
    }
}
