using System.ComponentModel.DataAnnotations;

namespace AttendanceTrackingSystem.Models
{
    public class Student
    {
        [Key]
        public int StudentId { get; set; }

        [Required(ErrorMessage = "Name is required.")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress(ErrorMessage = "Enter a valid email.")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Phone(ErrorMessage = "Enter a valid phone number.")]
        public string Phone { get; set; } = string.Empty;

        [Required]
        public string Status { get; set; } = "Active"; // Active / Inactive / Graduated

        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    }
}