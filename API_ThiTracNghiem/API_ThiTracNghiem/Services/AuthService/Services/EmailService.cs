using System.Net;
using System.Net.Mail;

namespace AuthService.Services;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var smtp = _config.GetSection("Smtp");
        using var client = new SmtpClient(smtp["Host"], int.Parse(smtp["Port"] ?? "587"))
        {
            EnableSsl = bool.Parse(smtp["EnableSsl"] ?? "true"),
            Credentials = new NetworkCredential(smtp["SenderEmail"], smtp["SenderPassword"])
        };

        using var mail = new MailMessage(smtp["SenderEmail"], to)
        {
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        await client.SendMailAsync(mail);
    }
}


