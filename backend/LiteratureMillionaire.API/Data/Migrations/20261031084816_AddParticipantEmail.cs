using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiteratureMillionaire.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddParticipantEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "NormalizedPhoneNumber",
                table: "Participants",
                type: "character varying(16)",
                unicode: false,
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldUnicode: false,
                oldMaxLength: 16);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Participants",
                type: "character varying(254)",
                unicode: false,
                maxLength: 254,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Participants_Email",
                table: "Participants",
                column: "Email",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Participants_Identity",
                table: "Participants",
                sql: "(\"NormalizedPhoneNumber\" IS NOT NULL AND \"Email\" IS NULL) OR (\"NormalizedPhoneNumber\" IS NULL AND \"Email\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Participants_Email",
                table: "Participants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Participants_Identity",
                table: "Participants");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Participants");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedPhoneNumber",
                table: "Participants",
                type: "character varying(16)",
                unicode: false,
                maxLength: 16,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldUnicode: false,
                oldMaxLength: 16,
                oldNullable: true);
        }
    }
}
