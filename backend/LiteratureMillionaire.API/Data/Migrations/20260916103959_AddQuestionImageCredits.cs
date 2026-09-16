using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiteratureMillionaire.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionImageCredits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageLicense",
                table: "Questions",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageSource",
                table: "Questions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageLicense",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "ImageSource",
                table: "Questions");
        }
    }
}
