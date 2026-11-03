using ExpenseHub.Api.Deployment;
using ExpenseHub.Api.Expenses;
using ExpenseHub.Api.Identity;
using ExpenseHub.Api.Persistence;
using ExpenseHub.Api.Reports;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ExpenseHub.Api;

internal static class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.Services.AddExpenseHubBuildInfo(builder.Configuration, "web");
        builder.Services.AddControllers();
        builder.Services.AddProblemDetails();
        builder.Services.AddOpenApi();
        builder.Services.AddExpenseHubPersistence(builder.Configuration, builder.Environment.ContentRootPath);

        // Hosted services start in the order they are registered: the database is prepared (only when the switch is on)
        // before the seed of the Admin, which needs the tables.
        builder.Services.AddExpenseHubDatabaseInitialization(builder.Configuration);
        builder.Services.AddExpenseHubIdentity();
        builder.Services.AddExpenseHubDataProtection(builder.Configuration, builder.Environment.ContentRootPath);
        builder.Services.AddExpenseHubIdentitySeed(builder.Configuration);
        builder.Services.AddExpenseHubExpenses();
        builder.Services.AddExpenseHubMessaging(builder.Configuration);
        builder.Services.AddExpenseHubReports();

        WebApplication app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseStatusCodePages();
        app.UseAuthentication();
        app.UseSecurityStampValidation();
        app.UseAuthorization();

        app.MapControllers();

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
            .WithName("GetHealth");

        app.Run();
    }
}
