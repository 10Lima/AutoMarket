using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AutoMarket.Services.Email;

public class GmailEmailSender : IEmailSender
{
    private readonly GmailSmtpOptions _opt;

    public GmailEmailSender(IOptions<GmailSmtpOptions> opt)
    {
        _opt = opt.Value;
    }

    public async Task SendAsync(string toEmail, string subject, string bodyText, CancellationToken ct = default)
    {
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(_opt.FromName, _opt.FromEmail));
        msg.To.Add(MailboxAddress.Parse(toEmail));
        msg.Subject = subject;

        msg.Body = new TextPart("plain")
        {
            Text = bodyText ?? string.Empty
        };

        using var client = new SmtpClient();

        // STARTTLS na porta 587
        var secure = _opt.UseStartTls 
            ? SecureSocketOptions.StartTls 
            : SecureSocketOptions.SslOnConnect;

        await client.ConnectAsync(_opt.Host, _opt.Port, secure, ct);
        await client.AuthenticateAsync(_opt.User, _opt.AppPassword, ct);
        await client.SendAsync(msg, ct);
        await client.DisconnectAsync(true, ct);
    }
}