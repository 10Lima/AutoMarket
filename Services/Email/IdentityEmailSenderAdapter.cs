namespace AutoMarket.Services.Email;

/// <summary>
/// Adaptador que liga o IEmailSender do Identity (Microsoft.AspNetCore.Identity.UI.Services)
/// ao teu GmailEmailSender (AutoMarket.Services.Email.IEmailSender).
/// </summary>
public class IdentityEmailSenderAdapter : Microsoft.AspNetCore.Identity.UI.Services.IEmailSender
{
    private readonly IEmailSender _sender;

    public IdentityEmailSenderAdapter(IEmailSender sender)
    {
        _sender = sender;
    }

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
        => _sender.SendAsync(email, subject, htmlMessage);
}