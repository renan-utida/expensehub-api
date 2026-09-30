using ExpenseHub.Api.Identity;
using ExpenseHub.Api.Persistence;
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
        builder.Services.AddOpenApi();
        builder.Services.AddExpenseHubPersistence(builder.Configuration, builder.Environment.ContentRootPath);
        builder.Services.AddExpenseHubIdentity();

        WebApplication app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
            .WithName("GetHealth");

        app.Run();
    }
}
