using Fcg.Contracts.Events;
using Fcg.Payments.Api.Application.Abstractions;
using Fcg.Payments.Api.Domain.Payments;
using Fcg.Payments.Api.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using ContractPaymentStatus = Fcg.Contracts.Events.PaymentStatus;
using DomainPaymentStatus = Fcg.Payments.Api.Domain.Payments.PaymentStatus;

namespace Fcg.Payments.Api.Application.Payments;

/// <summary>
/// Processa o pagamento de um pedido e publica o resultado.
///
/// Executa DENTRO da transação do outbox do consumer (o MassTransit abre a transação, chama o consumer,
/// faz SaveChanges e Commit). Por isso Payment + PaymentProcessedEvent + registro no inbox são atômicos.
///
/// Idempotência: se o pedido já tiver pagamento (redelivery após o inbox expirar, por exemplo),
/// NÃO cobra de novo — republica o resultado original. Cobrar duas vezes é o pior bug possível aqui.
/// </summary>
public sealed class ProcessOrderPaymentHandler(
    PaymentsDbContext db,
    IPaymentGateway gateway,
    IPublishEndpoint publishEndpoint,
    TimeProvider clock,
    ILogger<ProcessOrderPaymentHandler> logger)
{
    public async Task HandleAsync(OrderPlacedEvent order, CancellationToken cancellationToken)
    {
        var existing = await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.OrderId == order.OrderId, cancellationToken);
        if (existing is not null)
        {
            logger.LogWarning("Pedido {OrderId} já possui o pagamento {PaymentId} ({Status}); republicando resultado sem nova cobrança.",
                order.OrderId, existing.Id, existing.Status);
            await publishEndpoint.Publish(ToEvent(existing, order), cancellationToken);
            return;
        }

        var decision = await gateway.ChargeAsync(new PaymentCharge(order.OrderId, order.UserId, order.Price), cancellationToken);

        var payment = Payment.Record(order.OrderId, order.UserId, order.GameId, order.Price, decision.Approved, decision.Reason, clock);
        db.Payments.Add(payment);

        await publishEndpoint.Publish(ToEvent(payment, order), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Pagamento {PaymentId} do pedido {OrderId}: {Status}{Reason}.",
            payment.Id, order.OrderId, payment.Status, payment.Reason is null ? string.Empty : $" ({payment.Reason})");
    }

    private static PaymentProcessedEvent ToEvent(Payment payment, OrderPlacedEvent order) => new(
        payment.OrderId,
        payment.UserId,
        payment.GameId,
        payment.Amount,
        payment.Status == DomainPaymentStatus.Approved ? ContractPaymentStatus.Approved : ContractPaymentStatus.Rejected,
        payment.Reason,
        order.UserEmail,
        order.GameTitle,
        payment.ProcessedAt);
}
