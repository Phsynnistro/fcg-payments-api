using Fcg.Payments.Api.Application.Common;
using Fcg.Payments.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fcg.Payments.Api.Application.Payments;

public sealed class PaymentQueries(PaymentsDbContext db)
{
    public async Task<Result<PaymentResponse>> GetByOrderIdAsync(Guid orderId, Guid requesterId, bool requesterIsAdmin, CancellationToken cancellationToken)
    {
        var payment = await db.Payments.AsNoTracking()
            .Where(p => p.OrderId == orderId)
            .Select(p => new PaymentResponse(p.Id, p.OrderId, p.UserId, p.GameId, p.Amount, p.Status, p.Reason, p.ProcessedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (payment is null || (!requesterIsAdmin && payment.UserId != requesterId))
        {
            return PaymentErrors.NotFoundForOrder(orderId);
        }

        return payment;
    }
}
