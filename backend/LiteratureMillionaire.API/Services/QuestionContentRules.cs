using LiteratureMillionaire.API.Dtos;
using LiteratureMillionaire.API.Entities;

namespace LiteratureMillionaire.API.Services;

/// <summary>
/// What makes a question answerable, wherever it comes from: typed in the editor, or a row of somebody's
/// workbook. Both go through here, so an import cannot put anything into a bank that the editor would have
/// refused - which is the whole reason the rules live in one place rather than in each caller.
///
/// Only the content is judged here. Whether the bank and the category exist is the database's answer, and the
/// services ask it themselves.
/// </summary>
public static class QuestionContentRules
{
    public const int TextMaxLength = 1000;
    public const int OptionMaxLength = 300;
    public const int CategoryMaxLength = 100;
    public const int ExplanationMaxLength = 2000;
    public const int AltTextMaxLength = 300;
    public const int CreditMaxLength = 300;

    /// <summary>Field → messages, in Azerbaijani. Empty when the question is fit to be played.</summary>
    public static Dictionary<string, List<string>> Validate(AdminQuestionInput input)
    {
        var errors = new Dictionary<string, List<string>>();

        var text = input.Text?.Trim() ?? string.Empty;
        if (text.Length == 0) Add(errors, "text", "Sualı yazın.");
        else if (text.Length > TextMaxLength) Add(errors, "text", $"Sual ən çox {TextMaxLength} simvol ola bilər.");

        var options = new[]
        {
            ("optionA", input.OptionA?.Trim() ?? string.Empty),
            ("optionB", input.OptionB?.Trim() ?? string.Empty),
            ("optionC", input.OptionC?.Trim() ?? string.Empty),
            ("optionD", input.OptionD?.Trim() ?? string.Empty),
        };
        foreach (var (field, value) in options)
        {
            if (value.Length == 0) Add(errors, field, "Variantı yazın.");
            else if (value.Length > OptionMaxLength) Add(errors, field, $"Variant ən çox {OptionMaxLength} simvol ola bilər.");
        }

        // Four options that are really four. A repeated option makes the question unanswerable: two of the
        // buttons are the same answer and only one of them counts.
        var filled = options.Where(o => o.Item2.Length > 0).ToList();
        foreach (var duplicate in filled.GroupBy(o => o.Item2, StringComparer.CurrentCultureIgnoreCase).Where(g => g.Count() > 1))
        {
            foreach (var (field, _) in duplicate.Skip(1))
            {
                Add(errors, field, "Variantlar bir-birindən fərqli olmalıdır.");
            }
        }

        var correct = (input.CorrectOption ?? string.Empty).Trim().ToUpperInvariant();
        if (correct is not ("A" or "B" or "C" or "D")) Add(errors, "correctOption", "Düzgün cavabı seçin (A, B, C və ya D).");

        if (input.Difficulty is null) Add(errors, "difficulty", "Çətinliyi seçin (Asan, Orta, Çətin).");

        var category = input.Category?.Trim() ?? string.Empty;
        if (category.Length == 0) Add(errors, "category", "Alt kateqoriyanı yazın (məsələn: Bayraqlar).");
        else if (category.Length > CategoryMaxLength) Add(errors, "category", $"Alt kateqoriya ən çox {CategoryMaxLength} simvol ola bilər.");

        if ((input.Explanation?.Trim().Length ?? 0) > ExplanationMaxLength)
        {
            Add(errors, "explanation", $"İzah ən çox {ExplanationMaxLength} simvol ola bilər.");
        }

        ValidateImage(input, correct, options, errors);
        return errors;
    }

    private static void ValidateImage(AdminQuestionInput input, string correct, (string Field, string Value)[] options,
        Dictionary<string, List<string>> errors)
    {
        var url = Blank(input.ImageUrl);
        var alt = Blank(input.ImageAltText);
        var source = Blank(input.ImageSource);
        var licence = Blank(input.ImageLicense);

        if (url is null)
        {
            // Everything about a picture belongs to the picture: left behind, those fields would credit nothing.
            if (alt is not null || source is not null || licence is not null)
            {
                Add(errors, "imageUrl", "Şəkil seçilməyib, amma şəklə aid sahələr doludur. Ya şəkli seçin, ya həmin sahələri boşaldın.");
            }
            return;
        }

        if (!LocalImagePath.IsQuestionImage(url))
        {
            Add(errors, "imageUrl", "Şəkli Şəkillər bölməsindən seçin: kənar ünvan qəbul olunmur.");
        }

        if (alt is null)
        {
            Add(errors, "imageAltText", "Şəklin izahını (alt mətn) yazın: ekran oxuyucusu bunu oxuyur.");
        }
        else if (alt.Length > AltTextMaxLength)
        {
            Add(errors, "imageAltText", $"Alt mətn ən çox {AltTextMaxLength} simvol ola bilər.");
        }
        else if (options.FirstOrDefault(o => o.Field == "option" + correct).Value is { Length: > 0 } answer
                 && alt.Contains(answer, StringComparison.CurrentCultureIgnoreCase))
        {
            // The alt text is read out before the options, so an answer inside it hands the question away
            // to the one player who most needs it to be fair.
            Add(errors, "imageAltText", "Alt mətn düzgün cavabı deməməlidir: onu ekran oxuyucusu suala qədər oxuyur.");
        }

        if (source is null) Add(errors, "imageSource", "Şəklin mənbəyini yazın (məsələn: öz arxivimiz).");
        else if (source.Length > CreditMaxLength) Add(errors, "imageSource", $"Mənbə ən çox {CreditMaxLength} simvol ola bilər.");

        if (licence is null) Add(errors, "imageLicense", "Şəklin lisenziyasını yazın (məsələn: şirkətin öz şəkli, CC BY 4.0).");
        else if (licence.Length > CreditMaxLength) Add(errors, "imageLicense", $"Lisenziya ən çox {CreditMaxLength} simvol ola bilər.");
    }

    /// <summary>Copies a validated input onto a row, leaving Id and CreatedAt alone.</summary>
    public static void Apply(Question question, AdminQuestionInput input)
    {
        question.Text = input.Text!.Trim();
        question.OptionA = input.OptionA!.Trim();
        question.OptionB = input.OptionB!.Trim();
        question.OptionC = input.OptionC!.Trim();
        question.OptionD = input.OptionD!.Trim();
        question.CorrectOption = input.CorrectOption!.Trim().ToUpperInvariant()[0];
        question.Difficulty = input.Difficulty!.Value;
        question.Category = input.Category!.Trim();
        question.Explanation = Blank(input.Explanation);
        question.BookId = input.BookId;
        question.QuizModeId = input.QuizModeId;
        question.ImageUrl = Blank(input.ImageUrl);
        question.ImageAltText = Blank(input.ImageAltText);
        question.ImageSource = Blank(input.ImageSource);
        question.ImageLicense = Blank(input.ImageLicense);
    }

    public static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static void Add(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var list)) errors[field] = list = [];
        list.Add(message);
    }
}
