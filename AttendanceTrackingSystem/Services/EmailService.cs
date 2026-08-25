using Microsoft.Extensions.Logging;

namespace AttendanceTrackingSystem.Services
{
    public class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> _logger;

        public EmailService(ILogger<EmailService> logger)
        {
            _logger = logger;
        }

        public Task SendAbsenceNotificationAsync(string studentEmail, string studentName, string className, DateTime sessionDate)
        {
            // Simulates background email transmission and logs the notice
            string subject = $"Absence Notice: {className} on {sessionDate:dd MMM yyyy}";
            string body = $"Dear {studentName},\n\nYou were marked ABSENT for {className} on {sessionDate:dd MMM yyyy}.\nIf this was a mistake, please contact your instructor.";

            _logger.LogInformation("================ EMAIL SENT ================");
            _logger.LogInformation("TO: {Email}", studentEmail);
            _logger.LogInformation("SUBJECT: {Subject}", subject);
            _logger.LogInformation("BODY: {Body}", body);
            _logger.LogInformation("============================================");

            return Task.CompletedTask;
        }
        public Task SendPasswordResetPinAsync(string toEmail, string userName, string pinCode)
        {
            string subject = "Your Password Reset PIN Code";
            string body = $"Dear {userName},\n\nYou requested a password reset. Your 6-digit PIN code is:\n\n{pinCode}\n\nThis code expires in 15 minutes.\nIf you did not request this, please ignore this email.";

            _logger.LogInformation("================ EMAIL SENT ================");
            _logger.LogInformation("TO: {Email}", toEmail);
            _logger.LogInformation("SUBJECT: {Subject}", subject);
            _logger.LogInformation("BODY: {Body}", body);
            _logger.LogInformation("============================================");

            return Task.CompletedTask;
        }
    }
}
