using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiteratureMillionaire.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddParticipantsAndQuizAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Participants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NormalizedPhoneNumber = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Participants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuizAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParticipantId = table.Column<int>(type: "int", nullable: false),
                    CampaignId = table.Column<int>(type: "int", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CorrectAnswers = table.Column<int>(type: "int", nullable: true),
                    PointsEarned = table.Column<int>(type: "int", nullable: true),
                    Passed = table.Column<bool>(type: "bit", nullable: true),
                    TotalQuestions = table.Column<int>(type: "int", nullable: false),
                    PassingScore = table.Column<int>(type: "int", nullable: false),
                    MaxPoints = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizAttempts", x => x.Id);
                    table.CheckConstraint("CK_QuizAttempts_AttemptNumber", "[AttemptNumber] BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_QuizAttempts_MonthlyCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "MonthlyCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuizAttempts_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuizAttempts_CampaignId_ParticipantId",
                table: "QuizAttempts",
                columns: new[] { "CampaignId", "ParticipantId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuizAttempts_ParticipantId_CampaignId_AttemptNumber",
                table: "QuizAttempts",
                columns: new[] { "ParticipantId", "CampaignId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Participants_NormalizedPhoneNumber",
                table: "Participants",
                column: "NormalizedPhoneNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuizAttempts");

            migrationBuilder.DropTable(
                name: "Participants");
        }
    }
}
