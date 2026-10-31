namespace LiteratureMillionaire.API.Entities;

/// <summary>Slugs of the quiz modes the application knows by name.</summary>
public static class QuizModeSlugs
{
    public const string BilikDunyasi = "bilik-dunyasi";
    public const string AyinKitabi = "ayin-kitabi";
    public const string EdebiyyatDunyasi = "edebiyyat-dunyasi";
    public const string YasilBaki = "yasil-baki";
    public const string GreenGarden = "green-garden";

    /// <summary>
    /// Mode used by GET /api/campaigns/current and by start requests that name no campaign (clients written
    /// before quiz modes existed).
    /// </summary>
    public const string Default = BilikDunyasi;

    /// <summary>Modes played per book: their campaigns must name a book and questions are drawn from it.</summary>
    public static bool RequiresBook(string slug) => slug == AyinKitabi;
}
