using System;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Expenses;
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
    public Task<Expense?> FindOwnedAsync(Guid id, string ownerId)
    {
        return _dbContext.Expenses.FirstOrDefaultAsync(expense => expense.Id == id && expense.OwnerId == ownerId);
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
    }
}
