using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseHub.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReportRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequesterId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FromDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    StartedAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    CompletedAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    ResultJson = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportRequests", x => x.Id);
                    table.CheckConstraint("CK_ReportRequests_Completed_Has_Result", "\"Status\" <> 'Completed' OR \"ResultJson\" IS NOT NULL");
                    table.CheckConstraint("CK_ReportRequests_Failed_Has_Error", "\"Status\" <> 'Failed' OR \"ErrorCode\" IS NOT NULL");
                    table.CheckConstraint("CK_ReportRequests_Period_Order", "\"FromDate\" <= \"ToDate\"");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportRequests_RequesterId_CreatedAtUtc",
                table: "ReportRequests",
                columns: new[] { "RequesterId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportRequests");
        }
    }
}
