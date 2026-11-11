namespace Fcg.Payments.Api.Application.Abstractions;

public sealed record PaymentCharge(Guid OrderId, Guid UserId, decimal Amount);

public sealed record PaymentDecision(bool Approved, string? Reason)
{
    public static PaymentDecision Approve() => new(true, null);
    public static PaymentDecision Reject(string reason) => new(false, reason);
}

/// <summary>
/// Porta para o provedor de pagamento. Hoje: simulação. Amanhã: Stripe/Pagar.me/Cielo —
/// troca-se o adapter em Infrastructure/Gateway sem tocar no caso de uso.
/// </summary>
public interface IPaymentGateway
{
    Task<PaymentDecision> ChargeAsync(PaymentCharge charge, CancellationToken cancellationToken);
}
