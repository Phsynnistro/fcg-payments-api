using Fcg.Payments.Api.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fcg.Payments.Api.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Amount).HasPrecision(10, 2);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.Reason).HasMaxLength(Payment.ReasonMaxLength);

        // Última linha de defesa contra cobrança duplicada: mesmo que tudo falhe, o banco recusa.
        builder.HasIndex(p => p.OrderId).IsUnique();
        builder.HasIndex(p => p.UserId);
    }
}
