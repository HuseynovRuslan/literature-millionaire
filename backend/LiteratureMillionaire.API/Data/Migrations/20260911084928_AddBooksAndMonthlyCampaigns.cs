using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiteratureMillionaire.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBooksAndMonthlyCampaigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Books",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Author = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CoverImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Books", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MonthlyCampaigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PassingScore = table.Column<int>(type: "int", nullable: false),
                    RewardTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyCampaigns", x => x.Id);
                    table.CheckConstraint("CK_MonthlyCampaigns_DateRange", "[EndDate] >= [StartDate]");
                    table.CheckConstraint("CK_MonthlyCampaigns_PassingScore", "[PassingScore] BETWEEN 1 AND 10");
                    table.ForeignKey(
                        name: "FK_MonthlyCampaigns_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Books_IsActive",
                table: "Books",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyCampaigns_BookId",
                table: "MonthlyCampaigns",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyCampaigns_IsEnabled_StartDate_EndDate",
                table: "MonthlyCampaigns",
                columns: new[] { "IsEnabled", "StartDate", "EndDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MonthlyCampaigns");

            migrationBuilder.DropTable(
                name: "Books");
        }
    }
}
