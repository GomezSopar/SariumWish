using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PriceTrackerWorkspace.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DomainScrapeRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NormalizedDomain = table.Column<string>(type: "nvarchar(253)", maxLength: 253, nullable: false),
                    PriceCssSelector = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    TitleCssSelector = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    LastVerifiedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    SuccessCount = table.Column<int>(type: "int", nullable: false),
                    FailureCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainScrapeRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkspaceProjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TargetBudget = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceProjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TodoItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkspaceProjectId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CompletedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TodoItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TodoItems_WorkspaceProjects_WorkspaceProjectId",
                        column: x => x.WorkspaceProjectId,
                        principalTable: "WorkspaceProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WishlistItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkspaceProjectId = table.Column<int>(type: "int", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ProductUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    NormalizedDomain = table.Column<string>(type: "nvarchar(253)", maxLength: 253, nullable: false),
                    CurrentPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PreviousPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CurrencySymbol = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    IsIncludedInTotal = table.Column<bool>(type: "bit", nullable: false),
                    CustomCssSelectorOverride = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    LastScrapedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    LastErrorMessage = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WishlistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WishlistItems_WorkspaceProjects_WorkspaceProjectId",
                        column: x => x.WorkspaceProjectId,
                        principalTable: "WorkspaceProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DomainScrapeRules_NormalizedDomain",
                table: "DomainScrapeRules",
                column: "NormalizedDomain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TodoItems_DueDate",
                table: "TodoItems",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_TodoItems_WorkspaceProjectId_IsCompleted",
                table: "TodoItems",
                columns: new[] { "WorkspaceProjectId", "IsCompleted" });

            migrationBuilder.CreateIndex(
                name: "IX_WishlistItems_NormalizedDomain",
                table: "WishlistItems",
                column: "NormalizedDomain");

            migrationBuilder.CreateIndex(
                name: "IX_WishlistItems_WorkspaceProjectId_IsIncludedInTotal",
                table: "WishlistItems",
                columns: new[] { "WorkspaceProjectId", "IsIncludedInTotal" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceProjects_Name",
                table: "WorkspaceProjects",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DomainScrapeRules");

            migrationBuilder.DropTable(
                name: "TodoItems");

            migrationBuilder.DropTable(
                name: "WishlistItems");

            migrationBuilder.DropTable(
                name: "WorkspaceProjects");
        }
    }
}
