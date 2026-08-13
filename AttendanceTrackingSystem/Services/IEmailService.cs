namespace AttendanceTrackingSystem.Services
{
	public interface IEmailService
	{
		Task SendAbsenceNotificationAsync(string studentEmail, string studentName, string className, DateTime sessionDate);
	}
}