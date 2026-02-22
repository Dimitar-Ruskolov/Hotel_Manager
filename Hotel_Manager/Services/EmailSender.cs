using Microsoft.AspNetCore.Identity.UI.Services;
using System.Threading.Tasks;

namespace Hotel_Manager.Services
{
    public class EmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // Fake email sender – не праща реален имейл
            return Task.CompletedTask;
        }
    }
}