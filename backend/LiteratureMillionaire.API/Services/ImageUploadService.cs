using System.Globalization;
using System.Security.Cryptography;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Entities;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace LiteratureMillionaire.API.Services;

/// <summary>
/// Pictures uploaded in the admin panel (docs/admin-panel-plan.md, phase 4).
///
/// Nothing an administrator sends is ever stored as it arrives. The file is decoded, turned the right way up
/// (phone photographs carry their rotation in EXIF), shrunk to the size the game actually shows and written out
/// as WebP. That is what makes the upload safe as well as small: anything that is not really a picture fails to
/// decode and is refused, and re-encoding leaves nothing of the original file behind - no EXIF, no location, no
/// data hidden after the image - because the bytes are written from the decoded pixels.
///
/// The file is named by the hash of those bytes, so the same picture uploaded twice is one file, and a URL
/// already used by a question always shows the same picture.
/// </summary>
public interface IImageUploadService
{
    /// <exception cref="ImageUploadException">When the file is not a picture this can use.</exception>
    Task<StoredImage> StoreAsync(Stream content, string kind, string originalFileName, AdminActor actor, CancellationToken ct = default);

    Task<IReadOnlyList<UploadedImage>> ListAsync(string? kind, int take, CancellationToken ct = default);
}

/// <summary>What was stored, and whether this picture was already there.</summary>
public sealed record StoredImage(UploadedImage Image, bool AlreadyExisted);

public sealed class ImageUploadException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ImageUploadService : IImageUploadService
{
    /// <summary>Largest file accepted, before decoding. Nginx and the request pipeline cut off above this too.</summary>
    public const int MaxBytes = 10 * 1024 * 1024;

    /// <summary>A picture must be at least this wide and tall: below it, it looks broken on a phone.</summary>
    public const int MinSide = 200;

    /// <summary>Refuses a "decompression bomb" - a tiny file that decodes to a picture too large to hold in memory.</summary>
    public const long MaxPixels = 50_000_000;

    /// <summary>Longest edge kept, per kind. A question picture fills a card; a cover is shown smaller.</summary>
    private const int QuestionMaxEdge = 1600;
    private const int CoverMaxEdge = 1200;

    private const int WebpQuality = 82;

    private static readonly SKEncodedImageFormat[] Accepted = [SKEncodedImageFormat.Jpeg, SKEncodedImageFormat.Png, SKEncodedImageFormat.Webp];

    private readonly ApplicationDbContext _db;
    private readonly IAdminAuditLog _audit;
    private readonly UploadStorage _storage;
    private readonly ILogger<ImageUploadService> _logger;

    public ImageUploadService(ApplicationDbContext db, IAdminAuditLog audit, UploadStorage storage, ILogger<ImageUploadService> logger)
    {
        _db = db;
        _audit = audit;
        _storage = storage;
        _logger = logger;
    }

    public async Task<IReadOnlyList<UploadedImage>> ListAsync(string? kind, int take, CancellationToken ct = default)
    {
        var query = _db.UploadedImages.AsNoTracking();
        if (kind is not null)
        {
            query = query.Where(i => i.Kind == kind);
        }

        return await query.OrderByDescending(i => i.Id).Take(Math.Clamp(take, 1, 200)).ToListAsync(ct);
    }

    public async Task<StoredImage> StoreAsync(Stream content, string kind, string originalFileName, AdminActor actor, CancellationToken ct = default)
    {
        if (kind is not (UploadedImage.KindQuestion or UploadedImage.KindCover))
        {
            throw new ImageUploadException("UNKNOWN_KIND", "Şəklin növü seçilməyib.");
        }

        var source = await ReadAsync(content, ct);
        var (webp, width, height) = ToWebp(source, kind == UploadedImage.KindCover ? CoverMaxEdge : QuestionMaxEdge);

        var hash = Convert.ToHexString(SHA256.HashData(webp))[..32].ToLowerInvariant();
        var url = _storage.Save(kind, hash, webp);

        // The same picture twice is the same row: it is the same file on disk.
        if (await _db.UploadedImages.FirstOrDefaultAsync(i => i.Kind == kind && i.Hash == hash, ct) is { } existing)
        {
            return new StoredImage(existing, AlreadyExisted: true);
        }

        var image = new UploadedImage
        {
            Kind = kind,
            Hash = hash,
            Url = url,
            Width = width,
            Height = height,
            Bytes = webp.Length,
            OriginalFileName = CleanName(originalFileName),
            UploadedAtUtc = DateTime.UtcNow,
            UploadedBy = actor.FullName,
        };

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        _db.UploadedImages.Add(image);
        await _db.SaveChangesAsync(ct);
        await _audit.RecordAsync(actor, "image-uploaded", "image", image.Id.ToString(CultureInfo.InvariantCulture),
            $"{image.Url} · {width}×{height} · {image.Bytes / 1024} KB · {image.OriginalFileName}", ct);
        await transaction.CommitAsync(ct);

        _logger.LogInformation("Image {Url} stored ({Width}x{Height}, {Bytes} bytes).", url, width, height, webp.Length);
        return new StoredImage(image, AlreadyExisted: false);
    }

    // --- conversion -------------------------------------------------------------------------------------------

    private static async Task<byte[]> ReadAsync(Stream content, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        // Never more than one byte past the limit: a file that is too big is refused, not held in memory.
        var chunk = new byte[64 * 1024];
        int read;
        while (buffer.Length <= MaxBytes && (read = await content.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
        }

        if (buffer.Length == 0)
        {
            throw new ImageUploadException("EMPTY_FILE", "Fayl boşdur.");
        }
        if (buffer.Length > MaxBytes)
        {
            throw new ImageUploadException("FILE_TOO_LARGE", $"Fayl {MaxBytes / (1024 * 1024)} MB-dan böyükdür.");
        }
        return buffer.ToArray();
    }

    /// <summary>Decodes, turns upright, shrinks to <paramref name="maxEdge"/> and encodes as WebP.</summary>
    private static (byte[] Webp, int Width, int Height) ToWebp(byte[] source, int maxEdge)
    {
        using var data = SKData.CreateCopy(source);
        using var codec = SKCodec.Create(data)
            ?? throw new ImageUploadException("NOT_AN_IMAGE", "Bu fayl şəkil deyil və ya formatı dəstəklənmir. JPG, PNG və ya WebP göndərin.");

        if (!Accepted.Contains(codec.EncodedFormat))
        {
            throw new ImageUploadException("UNSUPPORTED_FORMAT",
                "Yalnız JPG, PNG və WebP qəbul olunur. iPhone şəkli (HEIC) olsa, telefonun ayarlarından \"Ən uyğun\" formatı seçin və ya şəkli JPG kimi göndərin.");
        }

        var info = codec.Info;
        if ((long)info.Width * info.Height > MaxPixels)
        {
            throw new ImageUploadException("IMAGE_TOO_LARGE", "Şəkil həddindən artıq böyükdür.");
        }

        using var decoded = SKBitmap.Decode(codec)
            ?? throw new ImageUploadException("NOT_AN_IMAGE", "Şəkli oxumaq alınmadı. Başqa fayl sınayın.");
        using var upright = Upright(decoded, codec.EncodedOrigin);

        if (Math.Min(upright.Width, upright.Height) < MinSide)
        {
            throw new ImageUploadException("IMAGE_TOO_SMALL", $"Şəklin hər tərəfi ən azı {MinSide} piksel olmalıdır.");
        }

        using var sized = Shrink(upright, maxEdge);
        using var image = SKImage.FromBitmap(sized);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, WebpQuality)
            ?? throw new ImageUploadException("ENCODE_FAILED", "Şəkli çevirmək alınmadı. Başqa fayl sınayın.");

        return (encoded.ToArray(), sized.Width, sized.Height);
    }

    /// <summary>Applies the rotation a camera recorded in EXIF, so a phone photograph is not stored on its side.</summary>
    private static SKBitmap Upright(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.Default or SKEncodedOrigin.TopLeft)
        {
            return bitmap.Copy();
        }

        var swapsSides = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var width = swapsSides ? bitmap.Height : bitmap.Width;
        var height = swapsSides ? bitmap.Width : bitmap.Height;

        var rotated = new SKBitmap(width, height, bitmap.ColorType, bitmap.AlphaType);
        using var canvas = new SKCanvas(rotated);
        canvas.SetMatrix(origin switch
        {
            SKEncodedOrigin.TopRight => SKMatrix.CreateScale(-1, 1).PostConcat(SKMatrix.CreateTranslation(width, 0)),
            SKEncodedOrigin.BottomRight => SKMatrix.CreateRotationDegrees(180, 0, 0).PostConcat(SKMatrix.CreateTranslation(width, height)),
            SKEncodedOrigin.BottomLeft => SKMatrix.CreateScale(1, -1).PostConcat(SKMatrix.CreateTranslation(0, height)),
            SKEncodedOrigin.LeftTop => SKMatrix.CreateRotationDegrees(90, 0, 0).PostConcat(SKMatrix.CreateScale(-1, 1)).PostConcat(SKMatrix.CreateTranslation(width, 0)),
            SKEncodedOrigin.RightTop => SKMatrix.CreateRotationDegrees(90, 0, 0).PostConcat(SKMatrix.CreateTranslation(width, 0)),
            SKEncodedOrigin.RightBottom => SKMatrix.CreateRotationDegrees(90, 0, 0).PostConcat(SKMatrix.CreateScale(1, -1)).PostConcat(SKMatrix.CreateTranslation(width, height)),
            SKEncodedOrigin.LeftBottom => SKMatrix.CreateRotationDegrees(270, 0, 0).PostConcat(SKMatrix.CreateTranslation(0, height)),
            _ => SKMatrix.Identity,
        });
        canvas.DrawBitmap(bitmap, 0, 0);
        canvas.Flush();
        return rotated;
    }

    /// <summary>Shrinks so the longest edge is at most <paramref name="maxEdge"/>; never enlarges.</summary>
    private static SKBitmap Shrink(SKBitmap bitmap, int maxEdge)
    {
        var longest = Math.Max(bitmap.Width, bitmap.Height);
        if (longest <= maxEdge)
        {
            return bitmap.Copy();
        }

        var scale = (double)maxEdge / longest;
        var width = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
        var height = Math.Max(1, (int)Math.Round(bitmap.Height * scale));
        var target = new SKImageInfo(width, height, bitmap.ColorType, bitmap.AlphaType);
        return bitmap.Resize(target, new SKSamplingOptions(SKCubicResampler.Mitchell))
            ?? throw new ImageUploadException("RESIZE_FAILED", "Şəklin ölçüsünü dəyişmək alınmadı.");
    }

    /// <summary>The uploader's file name, kept only to recognise the picture in a list. Never used as a path.</summary>
    private static string CleanName(string name)
    {
        var trimmed = Path.GetFileName(name ?? string.Empty).Trim();
        var clean = new string(trimmed.Where(c => !char.IsControl(c)).ToArray());
        if (clean.Length == 0)
        {
            return "şəkil";
        }
        return clean.Length <= 200 ? clean : clean[..200];
    }
}

/// <summary>
/// Where uploaded pictures live: one folder per kind on a volume that survives deployments (Uploads:Root, the
/// "uploads" volume in production). Nginx serves it read-only at /uploads/; the API only ever writes here.
/// </summary>
public sealed class UploadStorage
{
    private readonly string _root;

    public UploadStorage(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configured = configuration["Uploads:Root"];
        _root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "uploads")
            : configured);
    }

    public string Root => _root;

    public static string FolderFor(string kind) => kind == UploadedImage.KindCover ? "covers" : "questions";

    /// <summary>Writes the file unless it is already there (same content, same name), and returns its site path.</summary>
    public string Save(string kind, string hash, byte[] webp)
    {
        var folder = FolderFor(kind);
        var directory = Path.Combine(_root, folder);
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, hash + ".webp");
        if (!File.Exists(path))
        {
            // Written under a temporary name and moved into place, so nginx never serves a half-written file.
            var temporary = Path.Combine(directory, $".{hash}.{Guid.NewGuid():N}.tmp");
            File.WriteAllBytes(temporary, webp);
            try
            {
                File.Move(temporary, path);
            }
            catch (IOException) when (File.Exists(path))
            {
                File.Delete(temporary); // another upload of the same picture won the race
            }
        }

        return $"/uploads/{folder}/{hash}.webp";
    }
}
