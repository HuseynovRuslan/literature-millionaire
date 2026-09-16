using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LiteratureMillionaire.API.Data.Migrations
{
    /// <summary>
    /// Quiz modes. Creates the four modes, assigns every existing campaign and question to one (the "Bilik yarışı"
    /// book to bilik-dunyasi, every other book to ayin-kitabi, questions without a book stay without a mode), adds the
    /// per-campaign image target (existing campaigns get 2) and makes the campaign book optional. Participants,
    /// attempts and results are not touched.
    /// </summary>
    public partial class AddQuizModes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Modes. The same rows as Seed/QuizModeSeed; ON CONFLICT keeps the insert safe if a row already exists.
            migrationBuilder.CreateTable(
                name: "QuizModes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Slug = table.Column<string>(type: "character varying(60)", unicode: false, maxLength: 60, nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IconKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizModes", x => x.Id);
                    table.CheckConstraint("CK_QuizModes_Slug", "\"Slug\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuizModes_DisplayOrder",
                table: "QuizModes",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_QuizModes_Slug",
                table: "QuizModes",
                column: "Slug",
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO "QuizModes" ("Slug", "Title", "Description", "IconKey", "DisplayOrder", "IsActive")
                VALUES
                    ('bilik-dunyasi', 'Bilik Dünyası', 'Dünya bayraqları, paytaxtlar, Azərbaycan, ümumi biliklər və Azərbaycan kinosu üzrə qarışıq bilik yarışı.', 'globe', 1, TRUE),
                    ('ayin-kitabi', 'Ayın Kitabı', 'Ayın seçilmiş kitabı üzrə bilik yarışı.', 'book', 2, TRUE),
                    ('edebiyyat-dunyasi', 'Ədəbiyyat Dünyası', 'Azərbaycan və dünya ədəbiyyatı üzrə bilik yarışı.', 'feather', 3, TRUE),
                    ('yasil-baki', 'Yaşıl Bakı', 'Bakının bitkiləri, parkları və yaşıllıqları üzrə bilik yarışı.', 'leaf', 4, TRUE)
                ON CONFLICT ("Slug") DO NOTHING;
                """);

            // 2. Questions: nullable mode, backfilled from the book. Questions without a book (legacy) stay NULL.
            migrationBuilder.AddColumn<int>(
                name: "QuizModeId",
                table: "Questions",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Questions" AS q
                SET "QuizModeId" = m."Id"
                FROM "Books" AS b, "QuizModes" AS m
                WHERE q."BookId" = b."Id"
                  AND q."QuizModeId" IS NULL
                  AND m."Slug" = CASE WHEN b."Title" = 'Bilik yarışı' THEN 'bilik-dunyasi' ELSE 'ayin-kitabi' END;
                """);

            // 3. Campaigns: mode backfilled from the book (every existing campaign has one), then required.
            migrationBuilder.AddColumn<int>(
                name: "QuizModeId",
                table: "MonthlyCampaigns",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "MonthlyCampaigns" AS c
                SET "QuizModeId" = m."Id"
                FROM "Books" AS b, "QuizModes" AS m
                WHERE c."BookId" = b."Id"
                  AND c."QuizModeId" IS NULL
                  AND m."Slug" = CASE WHEN b."Title" = 'Bilik yarışı' THEN 'bilik-dunyasi' ELSE 'ayin-kitabi' END;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "QuizModeId",
                table: "MonthlyCampaigns",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            // 4. Image target: existing campaigns get the previous fixed value 2; new rows must set it explicitly.
            migrationBuilder.AddColumn<int>(
                name: "ImageQuestionsPerQuiz",
                table: "MonthlyCampaigns",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AlterColumn<int>(
                name: "ImageQuestionsPerQuiz",
                table: "MonthlyCampaigns",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 2);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MonthlyCampaigns_ImageQuestionsPerQuiz",
                table: "MonthlyCampaigns",
                sql: "\"ImageQuestionsPerQuiz\" BETWEEN 0 AND 10");

            // 5. The campaign book becomes optional (only "Ayın Kitabı" needs one).
            migrationBuilder.AlterColumn<int>(
                name: "BookId",
                table: "MonthlyCampaigns",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            // 6. Keys and indexes.
            migrationBuilder.CreateIndex(
                name: "IX_MonthlyCampaigns_QuizModeId_IsEnabled_StartDate_EndDate",
                table: "MonthlyCampaigns",
                columns: new[] { "QuizModeId", "IsEnabled", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Questions_QuizModeId_BookId",
                table: "Questions",
                columns: new[] { "QuizModeId", "BookId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_QuizModes_QuizModeId",
                table: "Questions",
                column: "QuizModeId",
                principalTable: "QuizModes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MonthlyCampaigns_QuizModes_QuizModeId",
                table: "MonthlyCampaigns",
                column: "QuizModeId",
                principalTable: "QuizModes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The old schema requires a book per campaign. Refuse rather than delete campaigns (and their attempts).
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "MonthlyCampaigns" WHERE "BookId" IS NULL) THEN
                        RAISE EXCEPTION 'AddQuizModes cannot be reverted: some campaigns have no book. Assign a book or remove those campaigns first.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Questions_QuizModes_QuizModeId",
                table: "Questions");

            migrationBuilder.DropForeignKey(
                name: "FK_MonthlyCampaigns_QuizModes_QuizModeId",
                table: "MonthlyCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_MonthlyCampaigns_QuizModeId_IsEnabled_StartDate_EndDate",
                table: "MonthlyCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_Questions_QuizModeId_BookId",
                table: "Questions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MonthlyCampaigns_ImageQuestionsPerQuiz",
                table: "MonthlyCampaigns");

            migrationBuilder.DropColumn(
                name: "ImageQuestionsPerQuiz",
                table: "MonthlyCampaigns");

            migrationBuilder.DropColumn(
                name: "QuizModeId",
                table: "MonthlyCampaigns");

            migrationBuilder.DropColumn(
                name: "QuizModeId",
                table: "Questions");

            migrationBuilder.DropTable(
                name: "QuizModes");

            migrationBuilder.AlterColumn<int>(
                name: "BookId",
                table: "MonthlyCampaigns",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
