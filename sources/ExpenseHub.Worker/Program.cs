using System.Threading.Tasks;
using ExpenseHub.Api.Deployment;
using ExpenseHub.Api.Persistence;
using ExpenseHub.Api.Reports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ExpenseHub.Worker;

/// <summary>
/// The worker of the reports: a host with the database of the application, the settings of RabbitMQ and the processor of the
/// report messages. It has no controllers, no authentication and no seed of the Admin. All the rules live in the Api project; the
/// worker only connects the queue to them.
/// </summary>
internal static class Program
{
    public static async Task Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddExpenseHubBuildInfo(builder.Configuration, "worker");
        builder.Services.AddExpenseHubPersistence(builder.Configuration, builder.Environment.ContentRootPath);
        builder.Services.AddExpenseHubRabbitMqOptions(builder.Configuration);
        builder.Services.AddExpenseHubReportProcessing();
        builder.Services.AddHostedService<ReportConsumerService>();

        using IHost host = builder.Build();

        await host.RunAsync();
    }
}
