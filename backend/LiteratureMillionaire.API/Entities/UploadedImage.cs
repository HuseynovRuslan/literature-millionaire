namespace LiteratureMillionaire.API.Entities;

/// <summary>
/// An image an administrator uploaded, after the server re-encoded it (see ImageUploadService). The file lives on the
/// uploads volume under <see cref="Url"/>; this row is how the panel lists what exists.
///
/// Files are named by the hash of their content, so uploading the same picture twice yields one file and one row,
/// and a URL, once handed out, always shows the same picture.
/// </summary>
public class UploadedImage
{
    public const string KindQuestion = "question";
    public const string KindCover = "cover";

    public int Id { get; set; }

    /// <summary><see cref="KindQuestion"/> or <see cref="KindCover"/>: decides the size it is stored at and its folder.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>First 32 hex characters of the SHA-256 of the stored WebP. Unique per kind.</summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>Site path, e.g. "/uploads/questions/0123….webp".</summary>
    public string Url { get; set; } = string.Empty;

    public int Width { get; set; }
    public int Height { get; set; }
    public long Bytes { get; set; }

    /// <summary>The name the file had on the uploader's computer, for recognising it in the list. Display only.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    public DateTime UploadedAtUtc { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
}
