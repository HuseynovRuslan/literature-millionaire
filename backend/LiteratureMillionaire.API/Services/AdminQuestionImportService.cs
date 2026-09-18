using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Dtos;
using LiteratureMillionaire.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LiteratureMillionaire.API.Services;

/// <summary>
/// Bulk import (docs/admin-panel-plan.md, phase 7): a workbook of questions somebody prepared offline, checked
/// before anything is written and then applied as it was shown.
///
/// Two steps on purpose. A file of two hundred rows almost always has a few that cannot be played - a repeated
/// option, a missing answer, a question already in the bank - and the useful thing is to see which, by row
/// number, while the file is still open in Excel. So the upload only reports; applying takes the report's token
/// and writes exactly the rows that report called ready. Nothing is half-written either: it is one transaction.
///
/// Rows are held to <see cref="QuestionContentRules"/>, the same rules the editor enforces, because a question
/// that arrives in a workbook is played next to the ones typed by hand.
/// </summary>
public interface IAdminQuestionImportService
{
    /// <exception cref="AdminImportException">When the file cannot be read as a question bank at all.</exception>
    /// <param name="defaultCategory">Used for rows whose own sub-category cell is empty, or when the file has no such column.</param>
    Task<AdminImportReportDto> AnalyseAsync(Stream file, string fileName, int bookId, int quizModeId, string? defaultCategory,
        AdminActor actor, CancellationToken ct = default);

    /// <exception cref="AdminImportException">When the report has expired or belongs to somebody else.</exception>
    Task<AdminImportResultDto> ApplyAsync(string token, AdminActor actor, CancellationToken ct = default);

    /// <summary>An empty workbook with the columns this understands, and one example row.</summary>
    byte[] Template();
}

public sealed class AdminImportException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class AdminQuestionImportService : IAdminQuestionImportService
{
    /// <summary>Largest file accepted. A bank of a few thousand questions is far below this.</summary>
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MaxRows = 2000;

    /// <summary>How long a report can be applied for. Long enough to read it, short enough to be re-checked.</summary>
    public static readonly TimeSpan ReportLifetime = TimeSpan.FromMinutes(20);

    /// <summary>Column names this understands, in Azerbaijani and in the English the generated banks use.</summary>
    private static readonly Dictionary<string, string[]> Columns = new()
    {
        ["text"] = ["sual", "sualmətni", "question", "questiontext", "text"],
        ["optionA"] = ["varianta", "a", "optiona"],
        ["optionB"] = ["variantb", "b", "optionb"],
        ["optionC"] = ["variantc", "c", "optionc"],
        ["optionD"] = ["variantd", "d", "optiond"],
        ["correctOption"] = ["düzgüncavab", "duzguncavab", "cavab", "correct", "correctoption", "answer"],
        ["difficulty"] = ["çətinlik", "cetinlik", "difficulty"],
        ["category"] = ["altkateqoriya", "kateqoriya", "category"],
        ["explanation"] = ["izah", "explanation"],
        ["imageUrl"] = ["şəkil", "sekil", "şəkilünvanı", "image", "imageurl"],
        ["imageAltText"] = ["altmətn", "altmetn", "imagealttext", "alt"],
        ["imageSource"] = ["mənbə", "menbe", "imagesource", "source"],
        ["imageLicense"] = ["lisenziya", "imagelicense", "license", "licence"],
    };

    private static readonly Dictionary<string, Difficulty> Difficulties = new(StringComparer.OrdinalIgnoreCase)
    {
        ["asan"] = Difficulty.Easy,
        ["easy"] = Difficulty.Easy,
        ["orta"] = Difficulty.Medium,
        ["medium"] = Difficulty.Medium,
        ["çətin"] = Difficulty.Hard,
        ["cetin"] = Difficulty.Hard,
        ["hard"] = Difficulty.Hard,
    };

    private readonly ApplicationDbContext _db;
    private readonly IAdminAuditLog _audit;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AdminQuestionImportService> _logger;

    public AdminQuestionImportService(ApplicationDbContext db, IAdminAuditLog audit, IMemoryCache cache,
        ILogger<AdminQuestionImportService> logger)
    {
        _db = db;
        _audit = audit;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>A report waiting to be applied: what it would write, for whom, and into which bank.</summary>
    private sealed record PendingImport(string ActorPhone, string FileName, int BookId, string BankTitle, int QuizModeId,
        IReadOnlyList<AdminQuestionInput> Ready);

    public async Task<AdminImportReportDto> AnalyseAsync(Stream file, string fileName, int bookId, int quizModeId,
        string? defaultCategory, AdminActor actor, CancellationToken ct = default)
    {
        var book = await _db.Books.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bookId, ct)
            ?? throw new AdminImportException("BANK_NOT_FOUND", "Sual bankını seçin.");
        var mode = await _db.QuizModes.AsNoTracking().FirstOrDefaultAsync(m => m.Id == quizModeId, ct)
            ?? throw new AdminImportException("MODE_NOT_FOUND", "Kateqoriyanı seçin.");

        var rows = Read(await ReadAllAsync(file, ct), fileName);
        if (rows.Count < 2)
        {
            throw new AdminImportException("EMPTY_FILE", "Faylda sual yoxdur: birinci sətir başlıq, sonrakılar suallar olmalıdır.");
        }

        var header = rows[0].Select(Normalise).ToList();
        var columns = Columns.ToDictionary(c => c.Key, c => header.FindIndex(h => c.Value.Contains(h, StringComparer.Ordinal)));
        var missing = new[] { "text", "optionA", "optionB", "optionC", "optionD", "correctOption" }
            .Where(field => columns[field] < 0).ToList();
        if (missing.Count > 0)
        {
            throw new AdminImportException("COLUMNS_MISSING",
                "Faylda lazımi sütunlar yoxdur: Sual, Variant A, Variant B, Variant C, Variant D, Düzgün cavab. Nümunə faylı yükləyin.");
        }

        // What the bank already holds, so a question that is in it twice is called out rather than doubled.
        var existing = (await _db.Questions.AsNoTracking().Where(q => q.BookId == bookId).Select(q => q.Text).ToListAsync(ct))
            .Select(t => t.Trim())
            .ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        var seenInFile = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);

        var report = new List<AdminImportRowDto>();
        var ready = new List<AdminQuestionInput>();
        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            var number = i + 1; // the number Excel shows
            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var input = ToInput(row, columns, bookId, quizModeId, defaultCategory);
            var text = input.Text?.Trim() ?? string.Empty;
            var problems = QuestionContentRules.Validate(input);

            // A difficulty nobody recognises is worth its own line: "Çətinliyi seçin" would send them looking
            // at an empty cell that is not empty.
            if (columns["difficulty"] >= 0 && input.Difficulty is null && Cell(row, columns["difficulty"]).Length > 0)
            {
                QuestionContentRules.Add(problems, "difficulty", $"Çətinlik tanınmadı: \"{Cell(row, columns["difficulty"])}\". Asan, Orta və ya Çətin yazın.");
            }

            if (problems.Count > 0)
            {
                report.Add(new AdminImportRowDto(number, "problem", text, Cell(row, columns["difficulty"]),
                    Cell(row, columns["category"]), input.ImageUrl is { Length: > 0 },
                    problems.SelectMany(p => p.Value).ToList()));
                continue;
            }

            if (existing.Contains(text) || !seenInFile.Add(text))
            {
                report.Add(new AdminImportRowDto(number, "duplicate", text, input.Difficulty?.ToString(), input.Category,
                    input.ImageUrl is { Length: > 0 },
                    [existing.Contains(text) ? "Bu sual artıq bankdadır." : "Bu sual faylda təkrarlanır."]));
                continue;
            }

            ready.Add(input);
            report.Add(new AdminImportRowDto(number, "ready", text, input.Difficulty?.ToString(), input.Category,
                input.ImageUrl is { Length: > 0 }, []));
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        _cache.Set(TokenKey(token), new PendingImport(actor.PhoneNumber, fileName, bookId, book.Title, quizModeId, ready),
            ReportLifetime);

        return new AdminImportReportDto(token, fileName, book.Title, mode.Title, report.Count, ready.Count,
            report.Count(r => r.Status == "duplicate"), report.Count(r => r.Status == "problem"),
            rows[0].ToList(), report);
    }

    public async Task<AdminImportResultDto> ApplyAsync(string token, AdminActor actor, CancellationToken ct = default)
    {
        if (_cache.Get<PendingImport>(TokenKey(token)) is not { } pending || pending.ActorPhone != actor.PhoneNumber)
        {
            // Also the case where somebody else's report is replayed: a token is only good for the admin it was made for.
            throw new AdminImportException("REPORT_EXPIRED", "Hesabatın vaxtı bitib. Faylı yenidən yükləyin.");
        }

        _cache.Remove(TokenKey(token));
        if (pending.Ready.Count == 0)
        {
            return new AdminImportResultDto(0, 0, pending.BankTitle);
        }

        // Checked again against the bank as it is now: another import (or the editor) may have added one of
        // these while the report sat on somebody's screen.
        var existing = (await _db.Questions.AsNoTracking().Where(q => q.BookId == pending.BookId).Select(q => q.Text).ToListAsync(ct))
            .Select(t => t.Trim())
            .ToHashSet(StringComparer.CurrentCultureIgnoreCase);

        var now = DateTime.UtcNow;
        var added = 0;
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        foreach (var input in pending.Ready)
        {
            if (!existing.Add(input.Text!.Trim()))
            {
                continue;
            }

            var question = new Question { CreatedAt = now };
            QuestionContentRules.Apply(question, input);
            _db.Questions.Add(question);
            added++;
        }

        await _db.SaveChangesAsync(ct);
        await _audit.RecordAsync(actor, "questions-imported", "book", pending.BookId.ToString(CultureInfo.InvariantCulture),
            $"{pending.FileName} · {added} sual əlavə olundu · bank: {pending.BankTitle}", ct);
        await transaction.CommitAsync(ct);

        _logger.LogInformation("Imported {Added} questions into book {BookId}.", added, pending.BookId);
        return new AdminImportResultDto(added, pending.Ready.Count - added, pending.BankTitle);
    }

    public byte[] Template() => XlsxWriter.Write("Suallar",
    [
        new("Sual", 60), new("Variant A", 24), new("Variant B", 24), new("Variant C", 24), new("Variant D", 24),
        new("Düzgün cavab", 13), new("Çətinlik", 10), new("Alt kateqoriya", 16), new("İzah", 40),
        new("Şəkil", 40), new("Alt mətn", 30), new("Mənbə", 24), new("Lisenziya", 24),
    ],
    [
        new object?[]
        {
            "Hansı ağac payızda qırmızı olur?", "Palıd", "Ağcaqayın", "Şam", "Sərv", "B", "Orta", "Bitkilər",
            "Ağcaqayının yarpaqları payızda qırmızıya çalır.", string.Empty, string.Empty, string.Empty, string.Empty,
        },
    ]);

    // --- reading ----------------------------------------------------------------------------------------------

    private static async Task<byte[]> ReadAllAsync(Stream file, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[64 * 1024];
        int read;
        while (buffer.Length <= MaxBytes && (read = await file.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
        }

        if (buffer.Length == 0) throw new AdminImportException("EMPTY_FILE", "Fayl boşdur.");
        if (buffer.Length > MaxBytes) throw new AdminImportException("FILE_TOO_LARGE", $"Fayl {MaxBytes / (1024 * 1024)} MB-dan böyükdür.");
        return buffer.ToArray();
    }

    /// <summary>An .xlsx, or a .json array of objects with the same column names.</summary>
    private static IReadOnlyList<IReadOnlyList<string>> Read(byte[] content, string fileName)
    {
        if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return ReadJson(content);
        }

        try
        {
            return XlsxReader.Read(new MemoryStream(content), MaxRows + 1);
        }
        catch (XlsxReader.NotAWorkbookException ex)
        {
            throw new AdminImportException("NOT_A_WORKBOOK", ex.Message);
        }
    }

    /// <summary>JSON is turned into the same shape as a sheet, so everything downstream has one thing to read.</summary>
    private static IReadOnlyList<IReadOnlyList<string>> ReadJson(byte[] content)
    {
        JsonElement root;
        try
        {
            root = JsonDocument.Parse(content).RootElement;
        }
        catch (JsonException)
        {
            throw new AdminImportException("NOT_A_WORKBOOK", "JSON faylını oxumaq alınmadı.");
        }

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("questions", out var nested))
        {
            root = nested;
        }
        if (root.ValueKind != JsonValueKind.Array)
        {
            throw new AdminImportException("NOT_A_WORKBOOK", "JSON faylı sualların siyahısı olmalıdır.");
        }

        var items = root.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object).ToList();
        var keys = items.SelectMany(i => i.EnumerateObject().Select(p => p.Name)).Distinct(StringComparer.Ordinal).ToList();
        var rows = new List<IReadOnlyList<string>> { keys };
        foreach (var item in items.Take(MaxRows))
        {
            rows.Add(keys.Select(key => item.TryGetProperty(key, out var value)
                ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString()
                : string.Empty).ToList());
        }
        return rows;
    }

    private static AdminQuestionInput ToInput(IReadOnlyList<string> row, Dictionary<string, int> columns, int bookId,
        int quizModeId, string? defaultCategory)
    {
        var difficulty = Cell(row, columns["difficulty"]);
        var category = Cell(row, columns["category"]);
        return new AdminQuestionInput
        {
            Text = Cell(row, columns["text"]),
            OptionA = Cell(row, columns["optionA"]),
            OptionB = Cell(row, columns["optionB"]),
            OptionC = Cell(row, columns["optionC"]),
            OptionD = Cell(row, columns["optionD"]),
            CorrectOption = Cell(row, columns["correctOption"]).Trim().ToUpperInvariant(),
            // No difficulty column at all means a bank of one difficulty; Medium is what the game treats as ordinary.
            Difficulty = columns["difficulty"] < 0 ? Difficulty.Medium
                : difficulty.Length == 0 ? Difficulty.Medium
                : Difficulties.TryGetValue(difficulty.Trim(), out var parsed) ? parsed : null,
            // A bank imported in one go is usually one sub-category; the form asks for it once instead of
            // making somebody fill the same word into two hundred cells.
            Category = category.Length > 0 ? category : defaultCategory?.Trim() ?? string.Empty,
            Explanation = Cell(row, columns["explanation"]),
            BookId = bookId,
            QuizModeId = quizModeId,
            ImageUrl = Cell(row, columns["imageUrl"]),
            ImageAltText = Cell(row, columns["imageAltText"]),
            ImageSource = Cell(row, columns["imageSource"]),
            ImageLicense = Cell(row, columns["imageLicense"]),
        };
    }

    private static string Cell(IReadOnlyList<string> row, int index) =>
        index >= 0 && index < row.Count ? row[index].Trim() : string.Empty;

    /// <summary>Header names are matched without spaces, case or punctuation: "Variant A" is "varianta".</summary>
    private static string Normalise(string header) =>
        new(header.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string TokenKey(string token) => "import:" + token;
}
