using Fcg.Payments.Api.Domain.Common;
using Fcg.Payments.Api.Domain.Payments;

namespace Fcg.Payments.UnitTests;

public sealed class PaymentTests
{
    private readonly FixedTimeProvider _clock = new();

    [Fact]
    public void Record_Aprovado_NaoTemMotivo()
    {
        var payment = Payment.Record(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m, approved: true, reason: "ignorado", _clock);

        Assert.Equal(PaymentStatus.Approved, payment.Status);
        Assert.Null(payment.Reason);
    }

    [Fact]
    public void Record_Recusado_SemMotivo_UsaPadrao()
    {
        var payment = Payment.Record(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m, approved: false, reason: " ", _clock);

        Assert.Equal(PaymentStatus.Rejected, payment.Status);
        Assert.Equal("Pagamento recusado.", payment.Reason);
    }

    [Fact]
    public void Record_SemPedido_LancaDomainException() =>
        Assert.Throws<DomainException>(() =>
            Payment.Record(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), 10m, true, null, _clock));
}
