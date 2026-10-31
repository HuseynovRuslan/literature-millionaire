using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using static LiteratureMillionaire.Tests.AdminTestClient;

namespace LiteratureMillionaire.Tests;

/// <summary>
/// Uploading pictures in the admin panel (phase 4). What matters here is that nothing is stored as it arrives:
/// every file is decoded and written out again as WebP, which is what keeps a "picture" that is really something
/// else - or carries a camera's location in its metadata - from ever reaching the uploads volume.
/// </summary>
public class AdminUploadsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kitabxana-uploads-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Uploading_needs_an_admin_session_and_the_admin_header()
    {
        await using var factory = NewFactory(_root);
        await factory.CreateDatabaseAsync();
        using var anonymous = Https(factory);

        using var list = await anonymous.GetAsync("/api/admin/uploads");
        // With the header (what a real client sends) but no session: refused as not signed in.
        using var upload = await UploadAsync(anonymous, Png(800, 600), "a.png", "question");
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, upload.StatusCode);

        using var admin = await SignedInAsync(factory);
        using var headerless = await admin.PostAsync("/api/admin/uploads", Multipart(Png(800, 600), "a.png", "question"));
        Assert.Equal(HttpStatusCode.BadRequest, headerless.StatusCode);
        Assert.Equal("ADMIN_HEADER_REQUIRED", (await headerless.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.False(Directory.Exists(Path.Combine(_root, "questions")));
    }

    [Fact]
    public async Task A_picture_is_stored_as_webp_at_the_size_the_game_shows_and_is_recorded()
    {
        await using var factory = NewFactory(_root);
        await factory.CreateDatabaseAsync();
        using var admin = await SignedInAsync(factory);

        using var response = await UploadAsync(admin, Png(2400, 1800), "şəkil 1.png", "question");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var url = body.GetProperty("url").GetString()!;
        // Shrunk to the long edge a question card uses, and named by the hash of what was stored.
        Assert.Matches(@"^/uploads/questions/[0-9a-f]{32}\.webp$", url);
        Assert.Equal(1600, body.GetProperty("width").GetInt32());
        Assert.Equal(1200, body.GetProperty("height").GetInt32());
        Assert.False(body.GetProperty("alreadyExisted").GetBoolean());
        Assert.Equal("şəkil 1.png", body.GetProperty("originalFileName").GetString());

        var file = Path.Combine(_root, "questions", url.Split('/')[^1]);
        Assert.True(File.Exists(file));
        var stored = await File.ReadAllBytesAsync(file);
        using var codec = SKCodec.Create(new MemoryStream(stored))!;
        Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
        Assert.Equal(1600, codec.Info.Width);

        var listed = Assert.Single((await admin.GetFromJsonAsync<JsonElement>("/api/admin/uploads?kind=question")).EnumerateArray());
        Assert.Equal(url, listed.GetProperty("url").GetString());
        Assert.Equal("Admin İşçi", listed.GetProperty("uploadedBy").GetString());

        var entry = await LastAuditAsync(factory);
        Assert.Equal("image-uploaded", entry.Action);
        Assert.Contains("1600×1200", entry.Details);
    }

    [Fact]
    public async Task The_same_picture_twice_is_one_file_and_covers_are_kept_smaller()
    {
        await using var factory = NewFactory(_root);
        await factory.CreateDatabaseAsync();
        using var admin = await SignedInAsync(factory);
        var png = Png(2000, 2000);

        using var first = await UploadAsync(admin, png, "cover.png", "cover");
        using var second = await UploadAsync(admin, png, "cover-copy.png", "cover");

        var one = await first.Content.ReadFromJsonAsync<JsonElement>();
        var two = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(one.GetProperty("url").GetString(), two.GetProperty("url").GetString());
        Assert.False(one.GetProperty("alreadyExisted").GetBoolean());
        Assert.True(two.GetProperty("alreadyExisted").GetBoolean());
        // A cover is shown smaller than a question picture.
        Assert.Equal(1200, one.GetProperty("width").GetInt32());
        Assert.Single(Directory.GetFiles(Path.Combine(_root, "covers")));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.UploadedImages.CountAsync());
        Assert.Equal(1, await db.AdminAuditEntries.CountAsync(e => e.Action == "image-uploaded"));
    }

    [Fact]
    public async Task Anything_that_is_not_a_usable_picture_is_refused_and_nothing_is_written()
    {
        await using var factory = NewFactory(_root);
        await factory.CreateDatabaseAsync();
        using var admin = await SignedInAsync(factory);

        // A file that is not an image at all, whatever its name says.
        using var script = await UploadAsync(admin, Encoding.UTF8.GetBytes("<?php system($_GET['c']); ?>"), "photo.png", "question");
        // Too small to show.
        using var tiny = await UploadAsync(admin, Png(150, 150), "tiny.png", "question");
        // Bigger than the limit, refused before anything tries to decode it.
        using var huge = await UploadAsync(admin, new byte[(11 * 1024 * 1024)], "huge.png", "question");
        // No kind, or one this does not know.
        using var unknownKind = await UploadAsync(admin, Png(800, 600), "a.png", "banner");
        using var empty = await UploadAsync(admin, [], "a.png", "question");

        Assert.Equal("NOT_AN_IMAGE", await CodeAsync(script));
        Assert.Equal("IMAGE_TOO_SMALL", await CodeAsync(tiny));
        Assert.Equal("FILE_TOO_LARGE", await CodeAsync(huge));
        Assert.Equal("UNKNOWN_KIND", await CodeAsync(unknownKind));
        Assert.Equal("EMPTY_FILE", await CodeAsync(empty));

        Assert.False(Directory.Exists(Path.Combine(_root, "questions")));
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().UploadedImages.CountAsync());
    }

    [Fact]
    public async Task What_is_stored_carries_nothing_of_the_original_file()
    {
        await using var factory = NewFactory(_root);
        await factory.CreateDatabaseAsync();
        using var admin = await SignedInAsync(factory);
        // A real PNG with a text chunk in it - what a camera's location data or an injected payload would look like.
        var withMetadata = PngWithText(900, 700, "GPSLatitude", "40.409264, 49.867092 SECRET-PAYLOAD");

        using var response = await UploadAsync(admin, withMetadata, "photo.png", "question");

        var url = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!;
        var stored = await File.ReadAllBytesAsync(Path.Combine(_root, "questions", url.Split('/')[^1]));
        Assert.DoesNotContain("SECRET-PAYLOAD", Encoding.Latin1.GetString(stored));
        Assert.DoesNotContain("GPSLatitude", Encoding.Latin1.GetString(stored));
    }

    // --- helpers ---------------------------------------------------------------

    private static LeaderboardApiFactory NewFactory(string root) =>
        new(qrLogSecret: Secret, adminPhones: AdminPhone, uploadsRoot: root);

    private static Task<HttpResponseMessage> UploadAsync(HttpClient admin, byte[] content, string fileName, string kind)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/uploads") { Content = Multipart(content, fileName, kind) };
        request.Headers.Add(AdminAuth.CsrfHeader, "1");
        return admin.SendAsync(request);
    }

    private static MultipartFormDataContent Multipart(byte[] content, string fileName, string kind)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return new MultipartFormDataContent { { file, "file", fileName }, { new StringContent(kind), "kind" } };
    }

    /// <summary>A real PNG of the given size: a gradient, so it does not compress to nothing.</summary>
    private static byte[] Png(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            using var paint = new SKPaint { Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(width, height),
                [SKColors.Navy, SKColors.Gold, SKColors.Crimson], null, SKShaderTileMode.Clamp) };
            canvas.DrawRect(0, 0, width, height, paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    /// <summary>The same PNG with a tEXt chunk added, the way a camera or an attacker would leave data behind.</summary>
    private static byte[] PngWithText(int width, int height, string keyword, string text)
    {
        var png = Png(width, height);
        var payload = Encoding.Latin1.GetBytes($"{keyword}\0{text}");
        var chunk = new List<byte>();
        chunk.AddRange(BitConverter.GetBytes(payload.Length).Reverse());
        chunk.AddRange("tEXt"u8.ToArray());
        chunk.AddRange(payload);
        chunk.AddRange(BitConverter.GetBytes(Crc32("tEXt"u8.ToArray().Concat(payload).ToArray())).Reverse());

        // Right after the 8-byte signature and the IHDR chunk (25 bytes).
        var result = new List<byte>(png[..33]);
        result.AddRange(chunk);
        result.AddRange(png[33..]);
        return result.ToArray();
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }
        return crc ^ 0xFFFFFFFFu;
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();
    }

    private static async Task<LiteratureMillionaire.API.Entities.AdminAuditEntry> LastAuditAsync(LeaderboardApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.AdminAuditEntries.AsNoTracking().OrderByDescending(e => e.Id).FirstAsync();
    }
}
