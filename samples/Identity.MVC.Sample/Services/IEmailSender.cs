using System.Threading.Tasks;

namespace Identity.MVC.Sample.Services
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string email, string subject, string message);
    }
}
