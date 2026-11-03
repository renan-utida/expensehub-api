using System;
using ExpenseHub.Api.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// Registers what the worker needs to process the report messages. It has no controllers, no authentication and no seed of the
/// Admin: only the clock, the reading of the expenses, the state of the requests and the processor of the messages.
/// </summary>
public static class ReportProcessingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the processor of the report messages and what it uses. The processor and the store are scoped, because they
    /// share the database context of one message.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddExpenseHubReportProcessing(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IReportProcessingStore, ReportProcessingStore>();
        services.AddScoped<ReportMessageProcessor>();

        return services;
    }
}
