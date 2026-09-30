using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseHub.Api.Persistence.Configurations;

internal sealed class PaymentRecordConfiguration : IEntityTypeConfiguration<PaymentRecord>
{
    public void Configure(EntityTypeBuilder<PaymentRecord> builder)
    {
        builder.ToTable("PaymentRecords");

        builder.HasKey(payment => payment.Id);

        builder.Property(payment => payment.ActorId)
            .IsRequired()
            .HasMaxLength(MappingConstants.UserIdMaxLength);

        builder.Property(payment => payment.PaidAtUtc)
            .HasConversion(new UtcDateTimeOffsetConverter());

        // One payment per expense, enforced by the database as well.
        builder.HasIndex(payment => payment.ExpenseId)
            .IsUnique();
    }
}
