using Fcg.Contracts.Events;
using MassTransit;

namespace Fcg.Payments.UnitTests;

/// <summary>Guarda a identidade dos contratos duplicados entre repositórios (ver ADR-0001).</summary>
public sealed class ContractTests
{
    [Theory]
    [InlineData(typeof(OrderPlacedEvent), "fcg.order-placed", new[] { "OrderId", "UserId", "GameId", "Price" })]
    [InlineData(typeof(PaymentProcessedEvent), "fcg.payment-processed", new[] { "OrderId", "UserId", "GameId", "Status" })]
    public void Evento_MantemIdentidadeDoContrato(Type type, string entityName, string[] requiredProperties)
    {
        Assert.Equal("Fcg.Contracts.Events", type.Namespace);
        Assert.Equal(entityName, type.GetCustomAttributes(typeof(EntityNameAttribute), false)
            .Cast<EntityNameAttribute>().Single().EntityName);

        var properties = type.GetProperties().Select(p => p.Name).ToHashSet();
        Assert.Superset(requiredProperties.ToHashSet(), properties);
    }

    [Fact]
    public void PaymentStatus_ValoresSaoEstaveis()
    {
        Assert.Equal(1, (int)PaymentStatus.Approved);
        Assert.Equal(2, (int)PaymentStatus.Rejected);
    }
}
