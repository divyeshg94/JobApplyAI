using JobApplyAi.Domain.Abstractions;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;

namespace JobApplyAi.Infrastructure.Email;

/// <summary>Gmail SMTP + app password (2FA required on the Google account) — see README prereqs.</summary>
public class MailKitEmailNotifier(IOptions<SmtpOptions> options) : IEmailNotifier
{
    private const string GmailHost = "smtp.gmail.com";
    private const int GmailStartTlsPort = 587;

    public async Task SendAsync(string toAddress, string subject, string bodyHtml, CancellationToken ct)
    {
        var settings = options.Value;
        if (string.IsNullOrEmpty(settings.Username) || string.IsNullOrEmpty(settings.AppPassword))
        {
            throw new InvalidOperationException("Smtp:Username / Smtp:AppPassword are not configured.");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName ?? "JobApplyAi", settings.Username));
        message.To.Add(MailboxAddress.Parse(toAddress));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = bodyHtml }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(GmailHost, GmailStartTlsPort, SecureSocketOptions.StartTls, ct);
        await client.AuthenticateAsync(settings.Username, settings.AppPassword, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}

public class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string? Username { get; set; }
    public string? AppPassword { get; set; }
    public string? FromName { get; set; }
}
