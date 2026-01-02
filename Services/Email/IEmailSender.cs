namespace AutoMarket.Services.Email;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string bodyText, CancellationToken ct = default);
}