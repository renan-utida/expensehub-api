using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Expenses;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IExpenseRepository"/>.
/// </summary>
public sealed class ExpenseRepository : IExpenseRepository
{
    private readonly ExpenseHubDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpenseRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The EF Core context.</param>
    public ExpenseRepository(ExpenseHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task AddAsync(Expense expense)
    {
        ArgumentNullException.ThrowIfNull(expense);

        _dbContext.Expenses.Add(expense);
        await _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Expense>> ListAsync(ExpenseScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        List<Expense> expenses = await _dbContext.Expenses
            .AsNoTracking()
            .Where(scope.Predicate)
            .OrderByDescending(expense => expense.CreatedAtUtc)
            .ThenByDescending(expense => expense.Id)
            .ToListAsync();

        return expenses;
    }

    /// <inheritdoc />
    public Task<Expense?> FindVisibleAsync(Guid id, ExpenseScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return _dbContext.Expenses
            .Where(scope.Predicate)
            .FirstOrDefaultAsync(expense => expense.Id == id);
    }

    /// <inheritdoc />
    public Task<Expense?> FindByIdAsync(Guid id)
    {
        return _dbContext.Expenses.FirstOrDefaultAsync(expense => expense.Id == id);
    }

    /// <inheritdoc />
    public Task<Expense?> FindVisibleWithHistoryAsync(Guid id, ExpenseScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return _dbContext.Expenses
            .AsNoTracking()
            .Where(scope.Predicate)
            .Include(expense => expense.History)
            .FirstOrDefaultAsync(expense => expense.Id == id);
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync()
    {
        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ExpenseConflictException("The expense changed while it was being saved.", exception);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new ExpenseConflictException("A unique index rejected the change because the same change was saved first.", exception);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqliteException sqliteException
            && SqliteConstraintErrors.IsUniqueConstraint(sqliteException.SqliteErrorCode, sqliteException.SqliteExtendedErrorCode);
    }
}
