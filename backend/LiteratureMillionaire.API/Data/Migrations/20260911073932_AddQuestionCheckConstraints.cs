using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiteratureMillionaire.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Questions_CorrectOption",
                table: "Questions",
                sql: "[CorrectOption] IN ('A', 'B', 'C', 'D')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Questions_Difficulty",
                table: "Questions",
                sql: "[Difficulty] IN (1, 2, 3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Questions_CorrectOption",
                table: "Questions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Questions_Difficulty",
                table: "Questions");
        }
    }
}
