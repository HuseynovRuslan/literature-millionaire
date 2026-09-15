using LiteratureMillionaire.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace LiteratureMillionaire.API.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<MonthlyCampaign> MonthlyCampaigns => Set<MonthlyCampaign>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();
    public DbSet<QuizMode> QuizModes => Set<QuizMode>();

    // Schema-level ceiling for AttemptNumber, baked into the InitialPostgreSql migration's CHECK
    // constraint. Deliberately NOT QuizRules.MaxAttemptsPerCampaign: the product rule can be
    // tightened (it is 1 today) without a schema change. The application enforces the real limit,
    // and the unique (ParticipantId, CampaignId, AttemptNumber) index keeps parallel starts safe.
    private const int MaxStoredAttemptNumber = 3;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Question>(entity =>
        {
            entity.ToTable("Questions", t =>
            {
                // Defense in depth: DTO validation is the first line, the DB is the last.
                t.HasCheckConstraint("CK_Questions_CorrectOption", "\"CorrectOption\" IN ('A', 'B', 'C', 'D')");
                t.HasCheckConstraint("CK_Questions_Difficulty", "\"Difficulty\" IN (1, 2, 3)");
                // Media fields travel together: both null, or both present with a non-empty alt text.
                // length(), not the PostgreSQL-only char_length(): the two are equivalent for text/
                // varchar in PostgreSQL, and length() is also understood by the SQLite provider the
                // test project uses for isolated in-memory tests, so this constraint runs unmodified
                // in both places.
                t.HasCheckConstraint("CK_Questions_ImageMedia",
                    "(\"ImageUrl\" IS NULL AND \"ImageAltText\" IS NULL) OR (\"ImageUrl\" IS NOT NULL AND \"ImageAltText\" IS NOT NULL AND length(\"ImageAltText\") > 0)");
            });
            entity.HasKey(q => q.Id);

            entity.Property(q => q.Text).IsRequired().HasMaxLength(1000);
            entity.Property(q => q.OptionA).IsRequired().HasMaxLength(300);
            entity.Property(q => q.OptionB).IsRequired().HasMaxLength(300);
            entity.Property(q => q.OptionC).IsRequired().HasMaxLength(300);
            entity.Property(q => q.OptionD).IsRequired().HasMaxLength(300);
            entity.Property(q => q.CorrectOption).IsRequired().HasMaxLength(1).IsUnicode(false);
            entity.Property(q => q.Difficulty).IsRequired().HasConversion<int>();
            entity.Property(q => q.Category).IsRequired().HasMaxLength(100);
            entity.Property(q => q.Explanation).HasMaxLength(2000);
            entity.Property(q => q.ImageUrl).HasMaxLength(500);
            entity.Property(q => q.ImageAltText).HasMaxLength(300);
            // Defense in depth: force Kind=Utc on read so the value serializes with a trailing
            // "Z" and compares correctly with DateTime.UtcNow, regardless of provider quirks.
            entity.Property(q => q.CreatedAt)
                .IsRequired()
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            // Optional during the transition (legacy rows have no book); Restrict so a
            // book with questions cannot be deleted out from under them.
            entity.HasOne(q => q.Book)
                .WithMany()
                .HasForeignKey(q => q.BookId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(q => q.Difficulty);
            entity.HasIndex(q => q.Category);
            entity.HasIndex(q => q.BookId);

            // Legacy questions keep a null mode and are never played; a mode with questions cannot be deleted.
            entity.HasOne(q => q.QuizMode)
                .WithMany()
                .HasForeignKey(q => q.QuizModeId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            // Game pool lookup: questions of a mode, optionally of one book.
            entity.HasIndex(q => new { q.QuizModeId, q.BookId });
        });

        modelBuilder.Entity<QuizMode>(entity =>
        {
            entity.ToTable("QuizModes", t =>
            {
                // PostgreSQL enforces the exact slug pattern (lowercase a-z, 0-9, single inner hyphens). Other
                // providers - the SQLite test database - get a portable approximation; QuizMode.IsValidSlug is the
                // application-side check.
                t.HasCheckConstraint("CK_QuizModes_Slug", Database.IsNpgsql()
                    ? "\"Slug\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'"
                    : "length(\"Slug\") > 0 AND \"Slug\" = lower(\"Slug\") AND \"Slug\" NOT LIKE '% %' AND \"Slug\" NOT LIKE '-%' AND \"Slug\" NOT LIKE '%-'");
            });
            entity.HasKey(m => m.Id);

            entity.Property(m => m.Slug).IsRequired().HasMaxLength(QuizMode.SlugMaxLength).IsUnicode(false);
            entity.Property(m => m.Title).IsRequired().HasMaxLength(QuizMode.TitleMaxLength);
            entity.Property(m => m.Description).IsRequired().HasMaxLength(QuizMode.DescriptionMaxLength);
            entity.Property(m => m.IconKey).IsRequired().HasMaxLength(QuizMode.IconKeyMaxLength);
            entity.Property(m => m.DisplayOrder).IsRequired();
            entity.Property(m => m.IsActive).IsRequired();

            entity.HasIndex(m => m.Slug).IsUnique();
            entity.HasIndex(m => m.DisplayOrder);
        });

        modelBuilder.Entity<Book>(entity =>
        {
            entity.ToTable("Books");
            entity.HasKey(b => b.Id);

            entity.Property(b => b.Title).IsRequired().HasMaxLength(200);
            entity.Property(b => b.Author).IsRequired().HasMaxLength(200);
            entity.Property(b => b.Description).IsRequired().HasMaxLength(2000);
            entity.Property(b => b.CoverImageUrl).IsRequired().HasMaxLength(500);
            entity.Property(b => b.IsActive).IsRequired().HasDefaultValue(true);

            entity.HasIndex(b => b.IsActive);
        });

        modelBuilder.Entity<MonthlyCampaign>(entity =>
        {
            entity.ToTable("MonthlyCampaigns", t =>
            {
                t.HasCheckConstraint("CK_MonthlyCampaigns_DateRange", "\"EndDate\" >= \"StartDate\"");
                t.HasCheckConstraint("CK_MonthlyCampaigns_PassingScore", "\"PassingScore\" BETWEEN 1 AND 10");
                t.HasCheckConstraint("CK_MonthlyCampaigns_ImageQuestionsPerQuiz", "\"ImageQuestionsPerQuiz\" BETWEEN 0 AND 10");
            });
            entity.HasKey(c => c.Id);

            entity.Property(c => c.StartDate).IsRequired();
            entity.Property(c => c.EndDate).IsRequired();
            entity.Property(c => c.PassingScore).IsRequired();
            entity.Property(c => c.RewardTitle).IsRequired().HasMaxLength(200);
            entity.Property(c => c.IsEnabled).IsRequired().HasDefaultValue(true);

            entity.Property(c => c.ImageQuestionsPerQuiz).IsRequired();

            // A book that has campaigns must not be silently deleted with them. Optional: only "Ayın Kitabı"
            // campaigns need a book, which CampaignService enforces.
            entity.HasOne(c => c.Book)
                .WithMany(b => b.Campaigns)
                .HasForeignKey(c => c.BookId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            // Every campaign belongs to exactly one quiz mode.
            entity.HasOne(c => c.QuizMode)
                .WithMany()
                .HasForeignKey(c => c.QuizModeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Playable-campaign lookups: enabled rows filtered by date range, overall and per quiz mode.
            entity.HasIndex(c => new { c.IsEnabled, c.StartDate, c.EndDate });
            entity.HasIndex(c => new { c.QuizModeId, c.IsEnabled, c.StartDate, c.EndDate });
        });

        modelBuilder.Entity<Participant>(entity =>
        {
            entity.ToTable("Participants");
            entity.HasKey(p => p.Id);

            entity.Property(p => p.FullName).IsRequired().HasMaxLength(120);
            entity.Property(p => p.NormalizedPhoneNumber).IsRequired().HasMaxLength(16).IsUnicode(false);
            entity.Property(p => p.CreatedAtUtc)
                .IsRequired()
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            entity.HasIndex(p => p.NormalizedPhoneNumber).IsUnique();
        });

        modelBuilder.Entity<QuizAttempt>(entity =>
        {
            entity.ToTable("QuizAttempts", t =>
            {
                t.HasCheckConstraint("CK_QuizAttempts_AttemptNumber", $"\"AttemptNumber\" BETWEEN 1 AND {MaxStoredAttemptNumber}");
            });
            entity.HasKey(a => a.Id);

            entity.Property(a => a.AttemptNumber).IsRequired();
            entity.Property(a => a.TotalQuestions).IsRequired();
            entity.Property(a => a.PassingScore).IsRequired();
            entity.Property(a => a.MaxPoints).IsRequired();
            entity.Property(a => a.StartedAtUtc)
                .IsRequired()
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            entity.Property(a => a.CompletedAtUtc)
                .HasConversion(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

            entity.HasOne(a => a.Participant)
                .WithMany(p => p.Attempts)
                .HasForeignKey(a => a.ParticipantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Campaign)
                .WithMany()
                .HasForeignKey(a => a.CampaignId)
                .OnDelete(DeleteBehavior.Restrict);

            // The unique index prevents concurrent starts from claiming the same attempt number.
            entity.HasIndex(a => new { a.ParticipantId, a.CampaignId, a.AttemptNumber }).IsUnique();
            entity.HasIndex(a => new { a.CampaignId, a.ParticipantId });
        });
    }
}
