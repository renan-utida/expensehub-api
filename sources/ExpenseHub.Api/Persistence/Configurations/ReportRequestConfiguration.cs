using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseHub.Api.Persistence.Configurations;

internal sealed class ReportRequestConfiguration : IEntityTypeConfiguration<ReportRequest>
{
    // The dates are stored as text in the form yyyy-MM-dd, so comparing them as text compares them as dates.
    private const string PeriodOrder = "\"FromDate\" <= \"ToDate\"";

    // A completed report always carries its result, and a failed one always carries its error code.
    private const string CompletedHasResult = "\"Status\" <> 'Completed' OR \"ResultJson\" IS NOT NULL";
    private const string FailedHasError = "\"Status\" <> 'Failed' OR \"ErrorCode\" IS NOT NULL";

    public void Configure(EntityTypeBuilder<ReportRequest> builder)
    {
        builder.ToTable("ReportRequests", table =>
        {
            table.HasCheckConstraint("CK_ReportRequests_Period_Order", PeriodOrder);
            table.HasCheckConstraint("CK_ReportRequests_Completed_Has_Result", CompletedHasResult);
            table.HasCheckConstraint("CK_ReportRequests_Failed_Has_Error", FailedHasError);
        });

        builder.HasKey(report => report.Id);

        builder.Property(report => report.RequesterId)
            .IsRequired()
            .HasMaxLength(MappingConstants.UserIdMaxLength);

        builder.Property(report => report.Scope)
            .HasConversion<string>()
            .HasMaxLength(MappingConstants.EnumTextMaxLength);

        // The state is a concurrency token: an update only applies if the state is still the one that was read,
        // so two attempts that try the same transition cannot both succeed.
        builder.Property(report => report.Status)
            .HasConversion<string>()
            .HasMaxLength(MappingConstants.EnumTextMaxLength)
            .IsConcurrencyToken();

        builder.Property(report => report.CreatedAtUtc)
            .HasConversion(new UtcDateTimeOffsetConverter());

        builder.Property(report => report.StartedAtUtc)
            .HasConversion(new UtcDateTimeOffsetConverter());

        builder.Property(report => report.CompletedAtUtc)
            .HasConversion(new UtcDateTimeOffsetConverter());

        builder.Property(report => report.ErrorCode)
            .HasMaxLength(50);

        builder.Property(report => report.ErrorMessage)
            .HasMaxLength(500);

        builder.HasIndex(report => new { report.RequesterId, report.CreatedAtUtc });
    }
}
