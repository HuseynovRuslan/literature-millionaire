using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Cookies;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Seed;
using LiteratureMillionaire.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "Frontend";

// --- Services ---------------------------------------------------------------

builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        // Serialize enums (Difficulty) as strings: "Easy" | "Medium" | "Hard"
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options
        .UseNpgsql(
            builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured."))
        // A failed SaveChanges is handled (or rethrown) by the caller. EF's own error log for it
        // repeats the SQL error text, which for a unique-index violation contains the duplicate key
        // value - the participant's phone number - so that event must not be written to the log.
        .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.SaveChangesFailed)));

builder.Services.AddScoped<IQuestionService, QuestionService>();

// Game sessions live in memory only (MVP). See GameService for the expiry policy.
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IGameService, GameService>();

builder.Services.AddScoped<ICampaignService, CampaignService>();
builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();
builder.Services.AddScoped<IBookService, BookService>();

// "QRLog ilə davam et". Pending logins live in the same memory cache as game sessions and expire on
// their own; the shared signing secret comes from configuration (QrLog__VouchSecret in production).
builder.Services.AddSingleton<IQrLoginService, QrLoginService>();

// Whether a quiz may only be started with a QRLog sign-in ticket. Fail-closed: it is required everywhere
// except Development and Testing unless configuration says otherwise, so a production deployment that
// forgets the setting still refuses a bare name and phone rather than accepting them.
// Read when first needed rather than while the builder runs, so configuration layered on afterwards (a test
// host, an environment variable provider) is what decides.
builder.Services.AddSingleton(sp =>
{
    var environment = sp.GetRequiredService<IWebHostEnvironment>();
    return new GameSignInPolicy(sp.GetRequiredService<IConfiguration>().GetValue(
        "Game:RequireQrLogin",
        defaultValue: !environment.IsDevelopment() && !environment.IsEnvironment("Testing")));
});

// --- Admin panel access (docs/admin-panel-plan.md, phase 1b) ---------------------------------------------
//
// An admin signs in with the same QRLog QR players use, and gets a session only if their phone is on the
// admin list (Admin__Phones). The session is a cookie: HttpOnly so no script can read it, Secure, SameSite=
// Strict so no other site can ride it, eight hours absolute with no sliding renewal. An API answers 401/403
// rather than redirecting to a login page. The list is re-checked on every request by the policy, so removing
// somebody takes effect immediately.
builder.Services.AddSingleton<IAdminDirectory, AdminDirectory>();
builder.Services.AddScoped<IAdminAuditLog, AdminAuditLog>();
builder.Services.AddScoped<IAdminLoginLinks, AdminLoginLinks>();
builder.Services.AddScoped<IAdminCampaignService, AdminCampaignService>();
builder.Services.AddScoped<IAdminResultsService, AdminResultsService>();
// Where uploaded pictures are written (Uploads__Root; the "uploads" volume in production).
builder.Services.AddSingleton<UploadStorage>();
builder.Services.AddScoped<IImageUploadService, ImageUploadService>();
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, AdminRequirementHandler>();
builder.Services
    .AddAuthentication(AdminAuth.Scheme)
    .AddCookie(AdminAuth.Scheme, options =>
    {
        options.Cookie.Name = "kitabxana_admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.Path = "/";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = false;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization(options =>
    options.AddPolicy(AdminAuth.Policy, policy => policy
        .AddAuthenticationSchemes(AdminAuth.Scheme)
        .RequireAuthenticatedUser()
        .AddRequirements(new AdminRequirement())));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Literature Millionaire API",
        Version = "v1",
        Description = "Quiz game API focused on Azerbaijani literature."
    });

    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// --- Production one-shot migration mode --------------------------------------
//
// `dotnet LiteratureMillionaire.API.dll --migrate-only` runs the migration/seed step and
// exits without starting Kestrel, so a Compose `migrate` service can complete (or fail)
// before the `api` service is allowed to start - the production HTTP server never comes up
// against a database that hasn't been migrated yet.
if (args.Contains("--migrate-only"))
{
    var migrated = await MigrateAndSeedAsync(app.Services);
    return migrated ? 0 : 1;
}

// --- Break-glass admin sign-in link ---------------------------------------------------------------------------
//
// `docker exec literature-millionaire-api-1 dotnet LiteratureMillionaire.API.dll --admin-link <phone> [name]`
// prints a single-use link valid for 15 minutes, for when QRLog is down. Running it needs a shell on the
// server, which is the whole of its protection - and the phone must still be on the admin list.
var adminLinkAt = Array.IndexOf(args, "--admin-link");
if (adminLinkAt >= 0)
{
    return await CreateAdminLinkAsync(app.Services, args.Skip(adminLinkAt + 1).ToArray());
}

// --- Database: apply migrations + seed (development convenience) ------------
//
// Production uses the explicit `--migrate-only` step above instead; this block only ever
// runs in Development, where a failure is logged (not fatal) so the dev server still starts.

if (app.Environment.IsDevelopment())
{
    await MigrateAndSeedAsync(app.Services);
}

// --- Pipeline ---------------------------------------------------------------

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Literature Millionaire API v1"));
}

app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);

// In development the API also serves the uploads folder at /uploads, because there is no Nginx in front of it
// (the Vite dev server proxies /uploads here). In production Nginx serves the same volume, read-only, and this
// never runs: the API's job is to write those files, not to hand them out.
if (app.Environment.IsDevelopment())
{
    var uploads = app.Services.GetRequiredService<UploadStorage>();
    Directory.CreateDirectory(uploads.Root);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploads.Root),
        RequestPath = "/uploads",
        ServeUnknownFileTypes = false,
    });
}

// Every state-changing admin request must carry the admin header (see AdminAuth.CsrfHeader). Checked before
// authentication, so a forged cross-site request is turned away without ever reaching a controller.
app.Use(async (context, next) =>
{
    var method = context.Request.Method;
    if (context.Request.Path.StartsWithSegments("/api/admin")
        && !HttpMethods.IsGet(method) && !HttpMethods.IsHead(method) && !HttpMethods.IsOptions(method)
        && context.Request.Headers[AdminAuth.CsrfHeader] != "1")
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { code = "ADMIN_HEADER_REQUIRED" });
        return;
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Liveness/readiness probe for the container orchestrator: DB reachability only, nothing else.
// No exception, connection string, host or credential ever reaches the response - only a
// stable {"status": "..."} body, so this is safe to expose without authentication.
app.MapGet("/health", async (ApplicationDbContext db, CancellationToken ct) =>
{
    try
    {
        return await db.Database.CanConnectAsync(ct)
            ? Results.Ok(new { status = "healthy" })
            : Results.Json(new { status = "unhealthy" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception)
    {
        return Results.Json(new { status = "unhealthy" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();
return 0;

// Shared by the Development auto-migrate convenience and the production `--migrate-only`
// mode, so the two never drift. Both callers only observe the returned success flag - one
// logs nothing further (dev server still starts either way), the other maps it to the
// process exit code.
static async Task<bool> MigrateAndSeedAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(db);
        return true;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        // Sanitized, same as GameService's DatabaseFailure: PostgreSQL's own exception
        // message/Detail/Where can quote row values, and this runs against the very
        // database participant/phone data lives in, so the exception object itself is
        // never passed to the logger - only its type and the safe, schema-level fields
        // PostgresErrors extracts (SQLSTATE, and a constraint/index name we chose
        // ourselves in the migration, never derived from row data).
        var sqlState = PostgresErrors.GetSqlState(ex);
        var constraintName = PostgresErrors.GetConstraintName(ex);
        logger.LogError(
            "Database failure during {Operation} (sql state {SqlState}, constraint {ConstraintName}, {ExceptionType} / {InnerExceptionType}).",
            "migrate-and-seed", sqlState, constraintName, ex.GetType().Name, ex.GetBaseException().GetType().Name);
        return false;
    }
}

static async Task<int> CreateAdminLinkAsync(IServiceProvider services, string[] rest)
{
    using var scope = services.CreateScope();
    var admins = scope.ServiceProvider.GetRequiredService<IAdminDirectory>();
    var links = scope.ServiceProvider.GetRequiredService<IAdminLoginLinks>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    if (rest.Length == 0 || !PhoneNumber.TryNormalize(rest[0], out var phone))
    {
        Console.Error.WriteLine("İstifadə: --admin-link <telefon> [ad]  (məs. --admin-link 0501234567 \"Ad Soyad\")");
        return 2;
    }

    if (!admins.IsAdmin(phone))
    {
        // Said without echoing the number back, so a mistyped command leaves nothing useful in a shell history.
        Console.Error.WriteLine("Bu nömrə admin siyahısında (Admin__Phones) deyil. Link yaradılmadı.");
        return 3;
    }

    var name = rest.Length > 1 && !string.IsNullOrWhiteSpace(rest[1]) ? rest[1].Trim() : "Təcili giriş";
    var token = await links.CreateAsync(phone, name[..Math.Min(name.Length, 120)]);
    var baseUrl = (configuration["Admin:PublicBaseUrl"] ?? string.Empty).TrimEnd('/');
    // The token travels in the fragment: browsers never send it to a server, so it stays out of access logs.
    Console.WriteLine($"{baseUrl}/admin/link#{token}");
    Console.WriteLine($"Bir dəfəlikdir və {(int)AdminLoginLinks.Lifetime.TotalMinutes} dəqiqə etibarlıdır.");
    return 0;
}

public partial class Program;
