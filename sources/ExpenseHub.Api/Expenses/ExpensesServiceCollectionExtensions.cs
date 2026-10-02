using System;
using ExpenseHub.Api.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ExpenseHub.Api.Expenses;

/// <summary>
/// Registers the expense services.
/// </summary>
public static class ExpensesServiceCollectionExtensions
{
    /// <summary>
    /// Registers the expense service, its repository and the clock of the server.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddExpenseHubExpenses(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<ExpenseService>();

        return services;
    }
}
