using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// Registers the settings of the broker and the real publisher of the report messages.
/// </summary>
public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// Binds the <c>RabbitMq</c> section of the configuration to <see cref="RabbitMqOptions"/>. The worker uses it alone, because
    /// it consumes the queue and does not publish.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddExpenseHubRabbitMqOptions(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));

        return services;
    }

    /// <summary>
    /// Registers the settings and <see cref="RabbitMqReportPublisher"/> as the publisher of the report messages, replacing the
    /// publisher that never confirms. It wins whether it is registered before or after <c>AddExpenseHubReports</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddExpenseHubMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddExpenseHubRabbitMqOptions(configuration);
        services.Replace(ServiceDescriptor.Singleton<IReportPublisher, RabbitMqReportPublisher>());

        return services;
    }
}
