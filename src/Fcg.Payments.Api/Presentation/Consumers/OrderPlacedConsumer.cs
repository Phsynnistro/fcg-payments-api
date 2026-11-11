using Fcg.Contracts.Events;
using Fcg.Payments.Api.Application.Payments;
using MassTransit;

namespace Fcg.Payments.Api.Presentation.Consumers;

/// <summary>
/// Competing consumers: com N réplicas do PaymentsAPI, todas leem a MESMA fila "payments.order-placed"
/// e o RabbitMQ distribui as mensagens entre elas — escala horizontal sem nenhuma mudança de código.
/// </summary>
public sealed class OrderPlacedConsumer(ProcessOrderPaymentHandler handler) : IConsumer<OrderPlacedEvent>
{
    public Task Consume(ConsumeContext<OrderPlacedEvent> context) =>
        handler.HandleAsync(context.Message, context.CancellationToken);
}
