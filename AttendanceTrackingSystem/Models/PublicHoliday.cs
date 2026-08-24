using System.ComponentModel.DataAnnotations;

namespace AttendanceTrackingSystem.Models
{
    public class PublicHoliday
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        [Required]
        [StringLength(100)]
        public string HolidayName { get; set; } = string.Empty;
    }
}
