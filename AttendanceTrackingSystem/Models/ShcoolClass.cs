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

        [StringLength(100)]
        public string? Schedule { get; set; }

        [Required(ErrorMessage = "Please assign a teacher to this class.")]
        [StringLength(100)]
        public string TeacherName { get; set; } = string.Empty;

        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    }
}