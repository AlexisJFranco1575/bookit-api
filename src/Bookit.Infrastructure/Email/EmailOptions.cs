using MailKit.Security;

namespace Bookit.Infrastructure.Email;

public enum EmailMode
{
    /// <summary>Emails are only written to the application log. Safe default for development and tests.</summary>
    Log,

    /// <summary>Emails are delivered through an SMTP server.</summary>
    Smtp
}

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public EmailMode Mode { get; set; } = EmailMode.Log;

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    /// <summary>None for Mailpit in development; StartTls (port 587) or SslOnConnect (port 465) in production.</summary>
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.Auto;

    public string? Username { get; set; }

    /// <summary>Comes from a secret or an environment variable, never from a committed file.</summary>
    public string? Password { get; set; }

    public string FromAddress { get; set; } = "no-reply@bookit.local";

    public string FromName { get; set; } = "Bookit";

    public int TimeoutSeconds { get; set; } = 30;
}