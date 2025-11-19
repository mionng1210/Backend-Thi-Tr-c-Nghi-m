using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace API_ThiTracNghiem.Services
{
    public interface IEmailService
    {
        Task SendAsync(string toEmail, string subject, string htmlBody);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(toEmail))
                    throw new ArgumentException("Email address cannot be null or empty", nameof(toEmail));

                var senderEmail = _configuration["Smtp:SenderEmail"];
                var senderPassword = _configuration["Smtp:SenderPassword"];

                if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(senderPassword))
                {
                    throw new InvalidOperationException("SMTP configuration is missing. Please configure Smtp:SenderEmail and Smtp:SenderPassword in appsettings.json");
                }

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("Thi Trắc Nghiệm", senderEmail));
                message.To.Add(MailboxAddress.Parse(toEmail));
                message.Subject = subject;
                message.Body = new TextPart("html") { Text = htmlBody };

                using var client = new SmtpClient();
                var host = _configuration["Smtp:Host"] ?? "smtp.gmail.com";
                var port = int.TryParse(_configuration["Smtp:Port"], out var p) ? p : 587;
                
                await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(senderEmail, senderPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                // Log và throw lại để caller có thể xử lý
                throw new InvalidOperationException($"Failed to send email to {toEmail}: {ex.Message}", ex);
            }
        }
    }
}


