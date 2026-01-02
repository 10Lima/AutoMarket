namespace AutoMarket.Services.Email;

public class GmailSmtpOptions
{
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;

    public string User { get; set; } = null!;
    public string AppPassword { get; set; } = null!;

    public string FromEmail { get; set; } = null!;
    public string FromName { get; set; } = "AutoMarket";
}