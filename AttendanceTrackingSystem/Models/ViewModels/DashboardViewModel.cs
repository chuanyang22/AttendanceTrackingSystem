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

        // Chart Data (Present vs Absent vs Excused)
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public int ExcusedCount { get; set; }
        public double OverallAttendanceRate { get; set; }
        
        public List<StudentClassAttendanceStat> ClassStats { get; set; } = new List<StudentClassAttendanceStat>();
        public List<TeacherSessionItem> TeacherTodaySessions { get; set; } = new List<TeacherSessionItem>();
    }

    public class TeacherSessionItem
    {
        public int SessionId { get; set; }
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string SessionType { get; set; } = "Lecture";
        public string PinCode { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public int PresentCount { get; set; }
        public int TotalEnrolled { get; set; }
    }

    public class StudentClassAttendanceStat
    {
        public string ClassName { get; set; } = string.Empty;
        public int Present { get; set; }
        public int Absent { get; set; }
        public int Excused { get; set; }
        
        public double AttendancePercentage 
        {
            get 
            {
                int total = Present + Absent + Excused;
                return total == 0 ? 0 : Math.Round(((double)(Present + Excused) / total) * 100, 1);
            }
        }
    }
}