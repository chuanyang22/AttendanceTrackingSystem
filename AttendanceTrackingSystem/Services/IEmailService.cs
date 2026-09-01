namespace AttendanceTrackingSystem.Services
{
	public interface IEmailService
	{
		Task SendAbsenceNotificationAsync(string studentEmail, string studentName, string className, DateTime sessionDate);
		Task SendPasswordResetPinAsync(string toEmail, string userName, string pinCode);
		Task SendPasswordResetLinkAsync(string toEmail, string userName, string resetLink);
	}
}
