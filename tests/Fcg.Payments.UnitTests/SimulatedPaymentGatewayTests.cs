using Fcg.Payments.Api.Application.Abstractions;
using Fcg.Payments.Api.Infrastructure.Gateway;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fcg.Payments.UnitTests;

public sealed class SimulatedPaymentGatewayTests
{
    private static SimulatedPaymentGateway CreateGateway(decimal limit) => new(
        Options.Create(new PaymentSimulationOptions { MaxApprovedAmount = limit, ProcessingDelayMs = 0 }),
        NullLogger<SimulatedPaymentGateway>.Instance);

    [Theory]
    [InlineData(0)]
    [InlineData(149.90)]
    [InlineData(300.00)] // o limite é inclusivo
    public async Task ChargeAsync_DentroDoLimite_Aprova(double amount)
    {
        var decision = await CreateGateway(300m).ChargeAsync(new PaymentCharge(Guid.NewGuid(), Guid.NewGuid(), (decimal)amount), CancellationToken.None);

        Assert.True(decision.Approved);
        Assert.Null(decision.Reason);
    }

    [Fact]
    public async Task ChargeAsync_AcimaDoLimite_RecusaComMotivo()
    {
        var decision = await CreateGateway(300m).ChargeAsync(new PaymentCharge(Guid.NewGuid(), Guid.NewGuid(), 499.90m), CancellationToken.None);

        Assert.False(decision.Approved);
        Assert.Contains("limite", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }
}
