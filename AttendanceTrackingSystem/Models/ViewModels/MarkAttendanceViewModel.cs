using System.ComponentModel.DataAnnotations;

namespace AttendanceTrackingSystem.Models.ViewModels
{
    public class MarkAttendanceViewModel
    {
        public int SessionId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public string? Topic { get; set; }

        public List<StudentAttendanceItem> Students { get; set; } = new List<StudentAttendanceItem>();
    }

    public class StudentAttendanceItem
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StudentEmail { get; set; } = string.Empty;
        public string Status { get; set; } = "Present"; // Present, Absent, Late, Excused
        public string? Remarks { get; set; }
    }
}