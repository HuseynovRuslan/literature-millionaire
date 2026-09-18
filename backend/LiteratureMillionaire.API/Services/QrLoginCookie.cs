namespace LiteratureMillionaire.API.Services;

/// <summary>
/// The cookie that says which sign-in this browser is in the middle of.
///
/// It exists because a phone hands people between windows: QRLog is opened to approve a code and gives the
/// browser back to an installed app or a fresh tab, which has none of the original page's storage. Cookies are
/// the one thing every window shares, so the code and its poll secret live here for as long as the code does.
///
/// The other half of that is just as important and was missing: the cookie has to be *cleared* the moment the
/// sign-in it points at has been used or ended. Left behind, it makes every later screen resume a sign-in that
/// is already spent - which is how signing out of the panel locked it in a retry loop, and how the next person
/// on a shared phone arrived already signed in as the last one.
/// </summary>
public static class QrLoginCookie
{
    public const string Name = "kitabxana_qr";

    /// <summary>Scoped to the sign-in endpoints: nothing else on the site ever needs to send it.</summary>
    private const string Path = "/api/qrlog-login";

    public static void Set(HttpResponse response, string code, string pollSecret, DateTime expiresAtUtc) =>
        response.Cookies.Append(Name, $"{code}.{pollSecret}", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = Path,
            Expires = expiresAtUtc,
        });

    /// <summary>Deletes it with the same attributes it was written with - otherwise the browser keeps its own.</summary>
    public static void Clear(HttpResponse response) =>
        response.Cookies.Delete(Name, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = Path,
        });

    /// <summary>The sign-in this browser is carrying, if any.</summary>
    public static bool TryRead(HttpRequest request, out string code, out string pollSecret)
    {
        code = string.Empty;
        pollSecret = string.Empty;
        if (!request.Cookies.TryGetValue(Name, out var value) || value.Split('.', 2) is not [var c, var secret])
        {
            return false;
        }

        code = c;
        pollSecret = secret;
        return true;
    }
}
