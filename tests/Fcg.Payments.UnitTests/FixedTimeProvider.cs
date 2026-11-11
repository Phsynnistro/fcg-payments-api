namespace Fcg.Payments.UnitTests;

/// <summary>Relógio congelado: torna asserções sobre datas determinísticas.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public static readonly DateTimeOffset Default = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    public FixedTimeProvider() : this(Default)
    {
    }

    public override DateTimeOffset GetUtcNow() => now;
}
