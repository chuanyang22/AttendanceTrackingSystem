using System.ComponentModel.DataAnnotations;

namespace AttendanceTrackingSystem.Models.ViewModels
{
    public class VerifyPinViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter the 6-digit PIN code.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "The PIN must be exactly 6 digits long.")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "The PIN must contain only numbers.")]
        [Display(Name = "6-Digit PIN")]
        public string PinCode { get; set; } = string.Empty;
    }
}
