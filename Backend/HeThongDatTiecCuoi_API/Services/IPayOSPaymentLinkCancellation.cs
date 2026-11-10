using PayOS;

namespace HeThongDatTiecCuoi_API.Services;

public interface IPayOSPaymentLinkCancellation
{
    Task CancelAsync(long orderCode, string reason, CancellationToken cancellationToken = default);
}

internal sealed class PayOSPaymentLinkCancellation(PayOSClient client) : IPayOSPaymentLinkCancellation
{
    public async Task CancelAsync(long orderCode, string reason, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await client.PaymentRequests.CancelAsync(orderCode, reason);
    }
}
