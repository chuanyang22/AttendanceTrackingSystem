using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceTrackingSystem.Models
{
    public class LeaveApplication
    {
        [Key]
        public int LeaveApplicationId { get; set; }

        public int? StudentId { get; set; }

        [ForeignKey("StudentId")]
        public virtual Student? Student { get; set; }

        public int? UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User? ApplicantUser { get; set; }

        [StringLength(100)]
        public string ApplicantName { get; set; } = string.Empty;

        [StringLength(50)]
        public string ApplicantRole { get; set; } = "Student"; // "Student" or "Teacher"

        [Required(ErrorMessage = "Start date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "From Date")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "End date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "To Date")]
        public DateTime EndDate { get; set; }

        [Required(ErrorMessage = "Please select a reason for leave.")]
        [StringLength(100)]
        public string Reason { get; set; } = string.Empty;

        [StringLength(1000)]
        [Display(Name = "Explanation / Description")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Proof document is required.")]
        [StringLength(500)]
        public string ProofDocumentUrl { get; set; } = string.Empty;

        [StringLength(255)]
        public string? ProofDocumentFileName { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Pending";

        public DateTime SubmittedAt { get; set; } = DateTime.Now;

        public int? ReviewedByUserId { get; set; }

        [ForeignKey("ReviewedByUserId")]
        public virtual User? ReviewedByUser { get; set; }

        public DateTime? ReviewedAt { get; set; }

        [StringLength(500)]
        [Display(Name = "Reviewer Remarks")]
        public string? ReviewerRemarks { get; set; }
    }
}
