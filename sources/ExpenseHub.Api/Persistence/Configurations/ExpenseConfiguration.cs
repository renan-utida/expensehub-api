using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseHub.Api.Persistence.Configurations;

internal sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    // From 0.01 to Int32.MaxValue, expressed in cents.
    private const string AmountCentsRange = "\"AmountCents\" BETWEEN 1 AND 214748364700";

    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("Expenses", table => table.HasCheckConstraint("CK_Expenses_AmountCents_Range", AmountCentsRange));

        builder.HasKey(expense => expense.Id);

        builder.Property(expense => expense.OwnerId)
            .IsRequired()
            .HasMaxLength(MappingConstants.UserIdMaxLength);

        builder.Property(expense => expense.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(expense => expense.Amount)
            .HasColumnName("AmountCents")
            .HasConversion(new DecimalToCentsConverter());

        // The state is a concurrency token: an update only applies if the state is still the one that was read,
        // so two requests that try the same transition cannot both succeed.
        builder.Property(expense => expense.Status)
            .HasConversion<string>()
            .HasMaxLength(MappingConstants.EnumTextMaxLength)
            .IsConcurrencyToken();

        builder.Property(expense => expense.CreatedAtUtc)
            .HasConversion(new UtcDateTimeOffsetConverter());

        builder.HasIndex(expense => expense.OwnerId);
        builder.HasIndex(expense => expense.Status);

        builder.HasMany(expense => expense.History)
            .WithOne(history => history.Expense)
            .HasForeignKey(history => history.ExpenseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(expense => expense.Payment)
            .WithOne(payment => payment.Expense)
            .HasForeignKey<PaymentRecord>(payment => payment.ExpenseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
