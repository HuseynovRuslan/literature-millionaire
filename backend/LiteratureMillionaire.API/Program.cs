using System.Text.Json.Serialization;
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
        .UseSqlServer(
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

// --- Database: apply migrations + seed (development convenience) ------------

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(db);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database migration/seeding failed. Check the 'DefaultConnection' connection string.");
    }
}

// --- Pipeline ---------------------------------------------------------------

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Literature Millionaire API v1"));
}

app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
