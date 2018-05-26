using System.ComponentModel.DataAnnotations;

namespace Identity.MVC.Sample.Models.AccountViewModels
{
    public class ExternalLoginViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}
