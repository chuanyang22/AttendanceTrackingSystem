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

        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    }
}