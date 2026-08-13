using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceTrackingSystem.Models
{
    public class AttendanceRecord
    {
        [Key]
        public int AttendanceRecordId { get; set; }

        [Required]
        public int SessionId { get; set; }

        [ForeignKey("SessionId")]
        public AttendanceSession? AttendanceSession { get; set; }

        [Required]
        public int StudentId { get; set; }

        [ForeignKey("StudentId")]
        public Student? Student { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Present"; // Present, Absent, Late, Excused

        [StringLength(255)]
        public string? Remarks { get; set; }

        public DateTime MarkedAt { get; set; } = DateTime.Now;
    }
}