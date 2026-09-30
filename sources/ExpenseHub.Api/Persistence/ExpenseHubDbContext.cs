using System;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Persistence;

/// <summary>
/// Entity Framework Core context of ExpenseHub, including the ASP.NET Core Identity tables.
/// </summary>
public class ExpenseHubDbContext : IdentityDbContext<IdentityUser>
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
    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.OnModelCreating(builder);

        builder.ApplyConfiguration(new ExpenseConfiguration());
        builder.ApplyConfiguration(new ExpenseCategoryConfiguration());
        builder.ApplyConfiguration(new ExpenseHistoryConfiguration());
        builder.ApplyConfiguration(new PaymentRecordConfiguration());
    }
}
