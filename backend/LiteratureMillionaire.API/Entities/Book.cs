namespace LiteratureMillionaire.API.Entities;

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Relative, locally hosted path such as "/covers/oluler.webp", or an absolute URL.</summary>
    public string CoverImageUrl { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<MonthlyCampaign> Campaigns { get; set; } = new List<MonthlyCampaign>();
}
