using LiteratureMillionaire.API.Data;
using Microsoft.EntityFrameworkCore;

namespace LiteratureMillionaire.API.Seed;

/// <summary>
/// Who owns a question bank once it exists: the admin panel, not this code.
///
/// These seeders used to reconcile their bank on every deployment - correcting rows, restoring wording, deleting
/// what they did not recognise. That was right while the banks could only be changed by editing the seed and
/// deploying. It stopped being right when the panel got a question editor (docs/admin-panel-plan.md, phase 6):
/// a question corrected there would come back wrong at the next deployment, and one deleted there would return -
/// silently, with nobody able to tell why.
///
/// So a seeder now fills its bank once, when it is empty, and never touches it again.
/// </summary>
public static class BankOwnership
{
    /// <summary>True when this book already has questions, meaning the panel owns them from here on.</summary>
    public static Task<bool> AlreadyFilledAsync(ApplicationDbContext db, int bookId, CancellationToken ct = default) =>
        db.Questions.AnyAsync(q => q.BookId == bookId, ct);
}
