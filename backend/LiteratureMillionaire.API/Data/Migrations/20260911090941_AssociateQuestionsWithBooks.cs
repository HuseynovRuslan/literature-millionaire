using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiteratureMillionaire.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AssociateQuestionsWithBooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BookId",
                table: "Questions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Questions_BookId",
                table: "Questions",
                column: "BookId");

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_Books_BookId",
                table: "Questions",
                column: "BookId",
                principalTable: "Books",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Questions_Books_BookId",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_Questions_BookId",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "BookId",
                table: "Questions");
        }
    }
}
