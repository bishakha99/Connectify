using System.ComponentModel.DataAnnotations;

namespace Connectify.ViewModels
{
    public class VerifyOtpViewModel
    {
        [Required]
        [EmailAddress]
        // This will be in a hidden field, but is needed to identify the user
        public string Email { get; set; }

        [Required]
        [Display(Name = "Verification Code")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "The code must be 6 digits.")]
        public string OtpCode { get; set; }
    }
}