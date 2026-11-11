namespace Fcg.Payments.Api.Infrastructure.Messaging;

public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    public PaymentsQueues Queues { get; init; } = new();
    public int PrefetchCount { get; init; } = 16;
    public int ConcurrentMessageLimit { get; init; } = 8;
}

public sealed class PaymentsQueues
{
    public string OrderPlaced { get; init; } = "payments.order-placed";
}
