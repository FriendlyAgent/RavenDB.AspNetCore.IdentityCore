using System.ComponentModel.DataAnnotations;

namespace Identity.MVC.Sample.Models.AccountViewModels
{
    public class ForgotPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}
