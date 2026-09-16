# Backend development setup

The API's only supported database is **PostgreSQL**. There is no SQL Server / LocalDB
support left in this project - `Microsoft.EntityFrameworkCore.SqlServer` and
`Microsoft.Data.SqlClient` have been removed. The only other provider still present is
SQLite, and only as a `PackageReference` of `LiteratureMillionaire.Tests`, for fast,
fully isolated unit tests; it is never used to run the API.

## Prerequisites

- .NET 10 SDK
- A PostgreSQL 16+ server reachable from your machine (a local install, or a disposable
  container - either is fine, this project does not care how you run it)
- The `dotnet-ef` global tool (`dotnet tool install --global dotnet-ef`) if you need to
  add or apply migrations yourself

## Configuring the connection string

The API reads its connection string from the standard ASP.NET Core configuration key
`ConnectionStrings:DefaultConnection`, which resolves (in increasing priority) from:

1. `appsettings.json` / `appsettings.Development.json` - committed, so these only ever
   hold a non-secret placeholder or a throwaway local-dev value, never a real password.
2. The environment variable `ConnectionStrings__DefaultConnection` (double underscore -
   that is how ASP.NET Core config binds nested keys from the environment). This is the
   supported way to point the API at a specific database, including any deployment
   target - **no VPS host, deployment username or production password is ever
   committed to this repository**; Task 11B will document how those are provided in the
   actual deployment environment.

Example connection string shape (`Npgsql` keyword=value format):

```
Host=localhost;Port=5432;Database=literature_millionaire_dev;Username=YOUR_USER;Password=YOUR_PASSWORD
```

`appsettings.Development.json` ships with a throwaway local convenience value
(`literature_millionaire_dev` / `postgres` / `postgres`) for a fresh local PostgreSQL
install with no other users on it. If your local instance uses a different user or
password, either edit that file locally (do not commit real changes to it beyond the
placeholder) or override it for one run:

```bash
# bash
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=literature_millionaire_dev;Username=postgres;Password=your_local_password"
```

```powershell
# PowerShell
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5432;Database=literature_millionaire_dev;Username=postgres;Password=your_local_password"
```

## Applying migrations

From `backend/LiteratureMillionaire.API`:

```bash
dotnet ef database update
```

This applies `Data/Migrations/*_InitialPostgreSql.cs` - the single migration that creates
the full current schema (Books, MonthlyCampaigns, Questions, Participants, QuizAttempts,
all indexes, unique constraints and CHECK constraints) on an empty PostgreSQL database.

In `Development`, `Program.cs` also calls `Database.MigrateAsync()` automatically at
startup, followed by the demo/approved-content seed (`DbSeeder`), so running the API
against a brand-new database "just works" without a manual `dotnet ef database update`
first.

## Running the API

```bash
cd backend/LiteratureMillionaire.API
dotnet run --launch-profile http
```

Swagger UI is available at `/swagger` in `Development`.

## Adding a migration after a model change

```bash
cd backend/LiteratureMillionaire.API
dotnet ef migrations add <Name>
```

Check the generated migration for PostgreSQL-appropriate SQL (quoted `"ColumnName"`
identifiers, `character varying`/`timestamp with time zone`/`date`, no `SqlServer:`
annotations) - EF Core's Npgsql provider gets this right automatically as long as no
provider-specific raw SQL is written by hand.
