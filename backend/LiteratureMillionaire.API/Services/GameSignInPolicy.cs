namespace LiteratureMillionaire.API.Services;

/// <summary>
/// Whether a quiz can only be started with a QRLog sign-in ticket (configuration: Game:RequireQrLogin).
///
/// Required in production. Before it was, the start endpoint took any name and phone it was sent, so the
/// QRLog sign-in on the page was a door in a wall with a gap beside it: one HTTP request played as anyone,
/// and a list of phone numbers bought as many attempts - with a prize at the end. Development and the test
/// suite may start with a bare name and phone, which is the only reason the switch exists.
/// </summary>
public sealed record GameSignInPolicy(bool RequireQrLogin);
