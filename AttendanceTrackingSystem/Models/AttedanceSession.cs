using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceTrackingSystem.Models
{
    public class AttendanceSession
    {
        [Key]
        public int SessionId { get; set; }

        [Required(ErrorMessage = "Please select a class.")]
        public int ClassId { get; set; }

        [ForeignKey("ClassId")]
        public SchoolClass? SchoolClass { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Session Date")]
        public DateTime SessionDate { get; set; } = DateTime.Today;

        [StringLength(100)]
        [Display(Name = "Topic / Remarks")]
        public string? Topic { get; set; }

        [StringLength(50)]
        public string QRCodeToken { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 10);

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
    }
}