using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiteratureMillionaire.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuizModeIsPreview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPreview",
                table: "QuizModes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // The label the frontend used to hard-code. Set here so an existing database keeps showing exactly
            // what it showed before the flag moved into it; from now on the panel decides.
            migrationBuilder.Sql(@"UPDATE ""QuizModes"" SET ""IsPreview"" = TRUE WHERE ""Slug"" = 'green-garden';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPreview",
                table: "QuizModes");
        }
    }
}
