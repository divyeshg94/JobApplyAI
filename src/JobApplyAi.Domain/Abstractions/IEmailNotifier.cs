namespace JobApplyAi.Domain.Abstractions;

/// <summary>Generic send — digest composition is Api-layer concern (NotificationService).</summary>
public interface IEmailNotifier
{
    Task SendAsync(string toAddress, string subject, string bodyHtml, CancellationToken ct);
}
