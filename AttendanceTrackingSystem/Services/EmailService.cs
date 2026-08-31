using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AttendanceTrackingSystem.Services
{
    public class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> _logger;
        private readonly IConfiguration _config;

        public EmailService(ILogger<EmailService> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        private async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                var smtpServer = _config["SmtpSettings:Server"];
                var smtpPort = int.Parse(_config["SmtpSettings:Port"] ?? "587");
                var senderEmail = _config["SmtpSettings:SenderEmail"];
                var senderName = _config["SmtpSettings:SenderName"];
                var password = _config["SmtpSettings:Password"];

                if (senderEmail == "your.email@gmail.com")
                {
                    _logger.LogWarning("SMTP not configured. Please update appsettings.json with your real email and app password.");
                    return;
                }

                using var client = new SmtpClient(smtpServer, smtpPort)
                {
                    Credentials = new NetworkCredential(senderEmail, password),
                    EnableSsl = true
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail!, senderName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = false,
                };
                mailMessage.To.Add(toEmail);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("Real email successfully sent to {Email}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send real email to {Email}", toEmail);
            }
        }

        public async Task SendAbsenceNotificationAsync(string studentEmail, string studentName, string className, DateTime sessionDate)
        {
            string subject = $"Absence Notice: {className} on {sessionDate:dd MMM yyyy}";
            string body = $"Dear {studentName},\n\nYou were marked ABSENT for {className} on {sessionDate:dd MMM yyyy}.\nIf this was a mistake, please contact your instructor.";

            await SendEmailAsync(studentEmail, subject, body);
        }
        
        public async Task SendPasswordResetPinAsync(string toEmail, string userName, string pinCode)
        {
            string subject = "Your Password Reset PIN Code";
            string body = $"Dear {userName},\n\nYou requested a password reset. Your 6-digit PIN code is:\n\n{pinCode}\n\nThis code expires in 15 minutes.\nIf you did not request this, please ignore this email.";

            await SendEmailAsync(toEmail, subject, body);
        }
    }
}
