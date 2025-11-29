using System.Net;
using System.Net.Mail;

namespace API_ThiTracNghiem.Services.AuthService.Services;

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
        try
        {
            if (string.IsNullOrWhiteSpace(to))
                throw new ArgumentException("Email address cannot be null or empty", nameof(to));

            var smtp = _config.GetSection("Smtp");
            var senderEmail = smtp["SenderEmail"];
            var senderPassword = smtp["SenderPassword"];

            if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(senderPassword))
            {
                throw new InvalidOperationException("SMTP configuration is missing. Please configure Smtp:SenderEmail and Smtp:SenderPassword in appsettings.json");
            }

            using var client = new SmtpClient(smtp["Host"], int.Parse(smtp["Port"] ?? "587"))
            {
                EnableSsl = bool.Parse(smtp["EnableSsl"] ?? "true"),
                Credentials = new NetworkCredential(senderEmail, senderPassword)
            };

            using var mail = new MailMessage(senderEmail, to)
            {
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            await client.SendMailAsync(mail);
        }
        catch (Exception ex)
        {
            // Log và throw lại để caller có thể xử lý
            throw new InvalidOperationException($"Failed to send email to {to}: {ex.Message}", ex);
        }
    }
}


