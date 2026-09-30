using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseHub.Api.Persistence.Configurations;

internal sealed class ExpenseHistoryConfiguration : IEntityTypeConfiguration<ExpenseHistory>
{
    public void Configure(EntityTypeBuilder<ExpenseHistory> builder)
    {
        builder.ToTable("ExpenseHistories");

        builder.HasKey(history => history.Id);

        builder.Property(history => history.Action)
            .HasConversion<string>()
            .HasMaxLength(MappingConstants.EnumTextMaxLength);

        builder.Property(history => history.ActorId)
            .IsRequired()
            .HasMaxLength(MappingConstants.UserIdMaxLength);

        builder.Property(history => history.OccurredAtUtc)
            .HasConversion(new UtcDateTimeOffsetConverter());

        builder.Property(history => history.PreviousStatus)
            .HasConversion<string>()
            .HasMaxLength(MappingConstants.EnumTextMaxLength);

        builder.Property(history => history.NewStatus)
            .HasConversion<string>()
            .HasMaxLength(MappingConstants.EnumTextMaxLength);

        builder.Property(history => history.Reason)
            .HasMaxLength(500);

        builder.Property(history => history.Changes)
            .HasMaxLength(2000);

        builder.HasIndex(history => new { history.ExpenseId, history.OccurredAtUtc });
    }
}
