using PayOS;
using PayOS.Models.V2.PaymentRequests;

namespace HeThongDatTiecCuoi_API.Services;

public interface IPayOSDepositPaymentGateway
{
    Task<CreatePaymentLinkResponse> CreateAsync(
        CreatePaymentLinkRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentLink> GetAsync(long orderCode, CancellationToken cancellationToken = default);
}

internal sealed class PayOSDepositPaymentGateway(PayOSClient client) : IPayOSDepositPaymentGateway
{
    public async Task<CreatePaymentLinkResponse> CreateAsync(
        CreatePaymentLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await client.PaymentRequests.CreateAsync(request);
    }

    public async Task<PaymentLink> GetAsync(long orderCode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await client.PaymentRequests.GetAsync(orderCode);
    }
}
