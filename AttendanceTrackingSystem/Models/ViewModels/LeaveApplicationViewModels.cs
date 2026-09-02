using System;
 using System.Collections.Generic;
 using System.ComponentModel.DataAnnotations;
 using Microsoft.AspNetCore.Http;

 namespace AttendanceTrackingSystem.Models.ViewModels
 {
     public class LeaveApplyViewModel
     {
         [Required(ErrorMessage = "From date is required.")]
         [DataType(DataType.Date)]
         [Display(Name = "From Date")]
         public DateTime StartDate { get; set; } = DateTime.Today;

         [Required(ErrorMessage = "To date is required.")]
         [DataType(DataType.Date)]
         [Display(Name = "To Date")]
         public DateTime EndDate { get; set; } = DateTime.Today;

         [Required(ErrorMessage = "Please select a reason for your leave.")]
         [Display(Name = "Reason for Leave")]
         public string Reason { get; set; } = "";

         [StringLength(1000, ErrorMessage = "Explanation cannot exceed 1000 characters.")]
         [Display(Name = "Detailed Explanation / Description")]
         public string? Description { get; set; }

         [Display(Name = "Supporting Proof Document (PDF, JPG, PNG)")]
         public IFormFile? ProofFile { get; set; }
     }

     public class LeaveIndexViewModel
     {
         public bool IsAdmin { get; set; }
         public bool IsApplicant { get; set; } // True for Student & Teacher
         public string UserRole { get; set; } = "Student"; // Student, Teacher, Admin
         public LeaveApplyViewModel ApplyForm { get; set; } = new LeaveApplyViewModel();

         // For Student & Teacher applicants
         public List<LeaveApplication> MyApplications { get; set; } = new List<LeaveApplication>();

         // For Admin reviewer
         public List<LeaveApplication> Applications { get; set; } = new List<LeaveApplication>();

         public int TotalCount { get; set; }
         public int PendingCount { get; set; }
         public int ApprovedCount { get; set; }
         public int RejectedCount { get; set; }

         public string StatusFilter { get; set; } = "All";
         public string RoleFilter { get; set; } = "All";
         public string SearchString { get; set; } = "";
     }
 }
