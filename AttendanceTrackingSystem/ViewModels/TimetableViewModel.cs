using System.ComponentModel.DataAnnotations;
using AttendanceTrackingSystem.Models;

namespace AttendanceTrackingSystem.ViewModels
{
    public class TimetableViewModel
    {
        public DateTime WeekStart { get; set; }
        public DateTime WeekEnd { get; set; }
        public List<PublicHoliday> Holidays { get; set; } = new List<PublicHoliday>();
        
        // List of all timetable slots to render
        public List<TimetableSlot> Slots { get; set; } = new List<TimetableSlot>();
    }

    public class TimetableSlot
    {
        public string ClassName { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public string Status { get; set; } = "Regular"; // Regular, Replacement, Cancelled
        public DateTime Date { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string SessionType { get; set; } = "L"; // L, T, P
    }
}

