namespace AttendanceTrackingSystem.Models.ViewModels
{
    public class StudentAttendanceSummaryViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StudentEmail { get; set; } = string.Empty;
        public int TotalSessions { get; set; }
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public int ExcusedCount { get; set; }

        // Additional Feature: Attendance Percentage
        public double AttendancePercentage => TotalSessions == 0
            ? 0
            : Math.Round(((double)(PresentCount + ExcusedCount) / TotalSessions) * 100, 1);

        public List<AttendanceRecordDetail> Records { get; set; } = new List<AttendanceRecordDetail>();
    }

    public class AttendanceRecordDetail
    {
        public DateTime Date { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Remarks { get; set; }
    }

    public class ClassAttendanceReportViewModel
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public int Month { get; set; }
        public int Year { get; set; }
        public int TotalSessions { get; set; }

        public List<StudentAttendanceSummaryViewModel> StudentSummaries { get; set; }
            = new List<StudentAttendanceSummaryViewModel>();
    }
}