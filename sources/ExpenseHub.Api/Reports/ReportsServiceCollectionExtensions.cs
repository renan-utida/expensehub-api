using System;
using ExpenseHub.Api.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// Registers the report services.
/// </summary>
public static class ReportsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the report service, its repository, the clock of the server and, when no real publisher is registered, a
    /// publisher that never confirms. The registration of the publisher uses <c>TryAdd</c>, so the RabbitMQ publisher
    /// (<c>AddExpenseHubMessaging</c>) is kept whether it is registered before or after this method.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddExpenseHubReports(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IReportRepository, ReportRepository>();
        services.TryAddSingleton<IReportPublisher, UnavailableReportPublisher>();
        services.AddScoped<ReportService>();

        return services;
    }
}
