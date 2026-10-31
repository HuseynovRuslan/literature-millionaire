namespace LiteratureMillionaire.API.Dtos;

/// <summary>What a file would do to a bank, before anything is written.</summary>
/// <param name="Token">Held for a few minutes; sent back to apply exactly what this report describes.</param>
/// <param name="Columns">Column names as they were found, so a mis-named header is visible rather than mysterious.</param>
public sealed record AdminImportReportDto(
    string Token,
    string FileName,
    string BankTitle,
    string QuizModeTitle,
    int TotalRows,
    int Ready,
    int Duplicates,
    int Problems,
    IReadOnlyList<string> Columns,
    IReadOnlyList<AdminImportRowDto> Rows);

/// <summary>One row of the file: what it says, and what the panel makes of it.</summary>
/// <param name="Row">The row number in the file, as Excel shows it.</param>
/// <param name="Status">"ready", "duplicate" or "problem".</param>
/// <param name="Problems">Plain-language problems; empty for a row that is ready.</param>
public sealed record AdminImportRowDto(
    int Row,
    string Status,
    string Text,
    string? Difficulty,
    string? Category,
    bool HasImage,
    IReadOnlyList<string> Problems);

/// <summary>What was written when a report was applied.</summary>
public sealed record AdminImportResultDto(int Added, int Skipped, string BankTitle);
