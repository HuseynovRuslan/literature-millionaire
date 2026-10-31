using System.Net;
using System.Net.Mail;
using System.Text;

namespace LiteratureMillionaire.API.Services;

/// <summary>A message to one person, as plain text and as HTML, so every mail client shows something readable.</summary>
public sealed record EmailMessage(string To, string Subject, string TextBody, string HtmlBody);

/// <summary>Sends the sign-in codes of <see cref="IEmailLoginService"/>.</summary>
public interface IEmailSender
{
    /// <summary>
    /// False when no mailbox is configured. Email sign-in is then switched off and the registration screen offers
    /// only QRLog - better than a form that takes someone's address and then fails to send them anything.
    /// </summary>
    bool IsConfigured { get; }

    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

/// <summary>
/// The mailbox codes are sent from (Email:* in configuration, Email__* on the server). Null unless both a host
/// and a sender address are known; the sender defaults to the user name, which for Gmail and most hosted
/// mailboxes is the address itself.
/// </summary>
public sealed record SmtpSettings(string Host, int Port, string? Username, string? Password, string FromAddress, string FromName)
{
    public const int DefaultPort = 587;
    public const string DefaultFromName = "Kitabxana 2.0";

    public static SmtpSettings? Read(IConfiguration configuration)
    {
        var host = configuration["Email:SmtpHost"]?.Trim();
        var username = configuration["Email:Username"]?.Trim();
        var from = configuration["Email:FromAddress"]?.Trim();
        if (string.IsNullOrEmpty(from)) from = username;
        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(from))
        {
            return null;
        }

        var port = configuration.GetValue("Email:SmtpPort", DefaultPort);
        var name = configuration["Email:FromName"]?.Trim();
        return new SmtpSettings(host, port, string.IsNullOrEmpty(username) ? null : username, configuration["Email:Password"],
            from, string.IsNullOrEmpty(name) ? DefaultFromName : name);
    }
}

/// <summary>
/// SMTP with STARTTLS (port 587 - Gmail, Yandex, Outlook and most hosted mail). System.Net.Mail rather than a
/// package: it is enough for one short message at a time, and it cannot speak implicit TLS on port 465, which is
/// why 587 is the default. The connection is always encrypted; a password is never sent in the clear.
/// </summary>
public sealed class SmtpEmailSender(SmtpSettings settings) : IEmailSender
{
    public bool IsConfigured => true;

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        using var mail = new MailMessage
        {
            From = new MailAddress(settings.FromAddress, settings.FromName, Encoding.UTF8),
            Subject = message.Subject,
            SubjectEncoding = Encoding.UTF8,
            Body = message.TextBody,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false,
        };
        mail.To.Add(message.To);
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.HtmlBody, Encoding.UTF8, "text/html"));

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 15_000,
        };
        if (settings.Username is not null)
        {
            client.Credentials = new NetworkCredential(settings.Username, settings.Password);
        }

        await client.SendMailAsync(mail, ct);
    }
}

/// <summary>
/// Development without a mailbox: the message is written to the log instead, so the whole flow can be tried
/// locally. Never registered outside Development - the log is no place for somebody's sign-in code.
/// </summary>
public sealed class DevelopmentEmailSender(ILogger<DevelopmentEmailSender> logger) : IEmailSender
{
    public bool IsConfigured => true;

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        logger.LogInformation("Development email to {To}: {Subject}\n{Body}", message.To, message.Subject, message.TextBody);
        return Task.CompletedTask;
    }
}

/// <summary>No mailbox configured outside Development: email sign-in is off.</summary>
public sealed class UnconfiguredEmailSender : IEmailSender
{
    public bool IsConfigured => false;

    public Task SendAsync(EmailMessage message, CancellationToken ct = default) =>
        throw new InvalidOperationException("No mailbox is configured (Email:SmtpHost, Email:Username).");
}
