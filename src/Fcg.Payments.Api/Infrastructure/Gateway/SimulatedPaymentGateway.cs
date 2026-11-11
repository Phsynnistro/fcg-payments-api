using Fcg.Payments.Api.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Fcg.Payments.Api.Infrastructure.Gateway;

public sealed class PaymentSimulationOptions
{
    public const string SectionName = "PaymentSimulation";

    /// <summary>Compras acima deste valor são recusadas. Vem do ConfigMap — mude e reinicie para ver o efeito.</summary>
    public decimal MaxApprovedAmount { get; init; } = 300m;

    /// <summary>Latência artificial: torna visível, na demonstração, o estado intermediário Pending.</summary>
    public int ProcessingDelayMs { get; init; } = 1500;
}

/// <summary>
/// Gateway SIMULADO e DETERMINÍSTICO. Regra de negócio previsível (limite de valor) é melhor que
/// aleatoriedade: o fluxo de rejeição pode ser demonstrado e testado sob demanda.
/// </summary>
public sealed class SimulatedPaymentGateway(
    IOptions<PaymentSimulationOptions> options,
    ILogger<SimulatedPaymentGateway> logger) : IPaymentGateway
{
    public async Task<PaymentDecision> ChargeAsync(PaymentCharge charge, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (settings.ProcessingDelayMs > 0)
        {
            await Task.Delay(settings.ProcessingDelayMs, cancellationToken);
        }

        if (charge.Amount > settings.MaxApprovedAmount)
        {
            logger.LogInformation("[SIMULAÇÃO] Pedido {OrderId}: R$ {Amount} acima do limite de R$ {Limit} → RECUSADO.",
                charge.OrderId, charge.Amount, settings.MaxApprovedAmount);
            return PaymentDecision.Reject($"Valor acima do limite aprovado pela operadora (R$ {settings.MaxApprovedAmount:0.00}).");
        }

        logger.LogInformation("[SIMULAÇÃO] Pedido {OrderId}: R$ {Amount} → APROVADO.", charge.OrderId, charge.Amount);
        return PaymentDecision.Approve();
    }
}
