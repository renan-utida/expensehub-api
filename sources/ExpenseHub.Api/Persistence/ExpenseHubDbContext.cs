using System;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Persistence;

/// <summary>
/// Entity Framework Core context of ExpenseHub.
/// </summary>
public class ExpenseHubDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExpenseHubDbContext"/> class.
    /// </summary>
    /// <param name="options">The options of the context.</param>
    public ExpenseHubDbContext(DbContextOptions<ExpenseHubDbContext> options)
        : base(options)
    {
    }

    /// <summary>Gets the expenses.</summary>
    public DbSet<Expense> Expenses => Set<Expense>();

    /// <summary>Gets the expense categories.</summary>
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();

    /// <summary>Gets the history entries of the expenses.</summary>
    public DbSet<ExpenseHistory> ExpenseHistories => Set<ExpenseHistory>();

    /// <summary>Gets the payment records.</summary>
    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(new ExpenseConfiguration());
        modelBuilder.ApplyConfiguration(new ExpenseCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new ExpenseHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentRecordConfiguration());
    }
}
