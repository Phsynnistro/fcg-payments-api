using Fcg.Payments.Api.Application.Common;
using Fcg.Payments.Api.Domain.Payments;

namespace Fcg.Payments.Api.Application.Payments;

public sealed record PaymentResponse(
    Guid Id,
    Guid OrderId,
    Guid UserId,
    Guid GameId,
    decimal Amount,
    PaymentStatus Status,
    string? Reason,
    DateTime ProcessedAt);

public static class PaymentErrors
{
    public static Error NotFoundForOrder(Guid orderId) =>
        Error.NotFound("payments.not_found", $"Nenhum pagamento encontrado para o pedido {orderId}.");
}
