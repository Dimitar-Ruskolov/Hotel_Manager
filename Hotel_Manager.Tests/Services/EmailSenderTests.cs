using Hotel_Manager.Services;
using NUnit.Framework;
using System.Threading.Tasks;

namespace HotelManager.Tests.Services
{
    [TestFixture]
    public class EmailSenderTests
    {
        private EmailSender _emailSender = null!;

        [SetUp]
        public void Setup()
        {
            _emailSender = new EmailSender();
        }

        [Test]
        public async Task SendEmailAsync_AlwaysReturnsCompletedTask()
        {
            var email = "test@example.com";
            var subject = "Test Subject";
            var htmlMessage = "<p>Hello from test</p>";

            var task = _emailSender.SendEmailAsync(email, subject, htmlMessage);

            Assert.That(task.IsCompleted, Is.True, "Task should be already completed");
            Assert.That(task.IsFaulted, Is.False, "Task should not be faulted");
            Assert.That(task.IsCanceled, Is.False, "Task should not be canceled");

            await task;

            Assert.Pass("Fake email sender completed without exception");
        }      
    }
}