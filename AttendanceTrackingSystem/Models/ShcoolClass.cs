using System.ComponentModel.DataAnnotations;

namespace AttendanceTrackingSystem.Models
{
    public class SchoolClass
    {
        [Key]
        public int ClassId { get; set; }

        [Required]
        [StringLength(100)]
        public string ClassName { get; set; } = string.Empty;

        [StringLength(50)]
        public string? ClassType { get; set; } // e.g. Lecture, Practical, Tutorial

        [StringLength(100)]
        public string? Schedule { get; set; }

        [Required(ErrorMessage = "Please assign a teacher to this class.")]
        public int TeacherId { get; set; }
        public User? Teacher { get; set; }

        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
        public ICollection<AttendanceSession> AttendanceSessions { get; set; } = new List<AttendanceSession>();
        
    }
}
