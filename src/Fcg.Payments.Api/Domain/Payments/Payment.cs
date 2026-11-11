using Fcg.Payments.Api.Domain.Common;

namespace Fcg.Payments.Api.Domain.Payments;

/// <summary>
/// Status no modelo de DOMÍNIO do Payments. Propositalmente separado do enum do contrato
/// (Fcg.Contracts.Events.PaymentStatus): o modelo interno pode evoluir (ex.: Pending, Refunded)
/// sem quebrar o contrato público — o mapeamento acontece na borda.
/// </summary>
public enum PaymentStatus
{
    Approved = 1,
    Rejected = 2
}

public sealed class Payment
{
    public const int ReasonMaxLength = 300;

    private Payment()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Chave de idempotência: um pedido gera no máximo UM pagamento (índice único).</summary>
    public Guid OrderId { get; private set; }

    public Guid UserId { get; private set; }
    public Guid GameId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string? Reason { get; private set; }
    public DateTime ProcessedAt { get; private set; }

    public static Payment Record(Guid orderId, Guid userId, Guid gameId, decimal amount, bool approved, string? reason, TimeProvider clock)
    {
        if (orderId == Guid.Empty || userId == Guid.Empty || gameId == Guid.Empty)
        {
            throw new DomainException("Pagamento sem pedido, usuário ou jogo válidos.");
        }

        if (amount < 0)
        {
            throw new DomainException("Valor de pagamento não pode ser negativo.");
        }

        var text = approved ? null : (string.IsNullOrWhiteSpace(reason) ? "Pagamento recusado." : reason.Trim());

        return new Payment
        {
            Id = Guid.CreateVersion7(),
            OrderId = orderId,
            UserId = userId,
            GameId = gameId,
            Amount = amount,
            Status = approved ? PaymentStatus.Approved : PaymentStatus.Rejected,
            Reason = text is { Length: > ReasonMaxLength } ? text[..ReasonMaxLength] : text,
            ProcessedAt = clock.GetUtcNow().UtcDateTime
        };
    }
}
