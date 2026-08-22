namespace AttendanceTrackingSystem.Models.ViewModels
{
    public class DashboardViewModel
    {
        public string Role { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;

        // Metric Card Counts
        public int TotalStudents { get; set; }
        public int TotalClasses { get; set; }
        public int TotalSessions { get; set; }
        public int TotalUsers { get; set; }

        // Chart Data (Present vs Absent vs Late)
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public int LateCount { get; set; }
        public double OverallAttendanceRate { get; set; }
    }
}