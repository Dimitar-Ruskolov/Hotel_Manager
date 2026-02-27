using Hotel_Manager.Areas.Identity.Pages.Account;
using Hotel_Manager.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Moq;
using NUnit.Framework;
using System;
using System.Text;
using System.Threading.Tasks;
using IdentityUIEmailSender = Microsoft.AspNetCore.Identity.UI.Services.IEmailSender;

namespace HotelManager.Tests.Areas.Identity.Pages.Account
{
    [TestFixture]
    public class ForgotPasswordModelTests
    {
        private ForgotPasswordModel _model = null!;
        private Mock<UserManager<ApplicationUser>> _userManagerMock = null!;
        private Mock<IdentityUIEmailSender> _emailSenderMock = null!;

        [SetUp]
        public void Setup()
        {
            _userManagerMock = new Mock<UserManager<ApplicationUser>>(
                new Mock<IUserStore<ApplicationUser>>().Object,
                null!, null!, null!, null!, null!, null!, null!, null!
            );

            _emailSenderMock = new Mock<IdentityUIEmailSender>();

            _model = new ForgotPasswordModel(_userManagerMock.Object, _emailSenderMock.Object)
            {
                Input = new ForgotPasswordModel.InputModel()
            };
        }

        [Test]
        public async Task OnPostAsync_InvalidModelState_ReturnsPage()
        {
            _model.ModelState.AddModelError("Input.Email", "Required");

            var result = await _model.OnPostAsync();

            Assert.That(result, Is.InstanceOf<PageResult>());
        }

        [Test]
        public async Task OnPostAsync_UserNotFound_RedirectsToConfirmation()
        {
            _userManagerMock.Setup(m => m.FindByEmailAsync("unknown@example.com"))
                .ReturnsAsync((ApplicationUser?)null);

            _model.Input.Email = "unknown@example.com";

            var result = await _model.OnPostAsync();

            Assert.That(result, Is.InstanceOf<RedirectToPageResult>());
            var redirect = (RedirectToPageResult)result;
            Assert.That(redirect.PageName, Is.EqualTo("./ForgotPasswordConfirmation"));
        }

        [Test]
        public async Task OnPostAsync_UserEmailNotConfirmed_RedirectsToConfirmation()
        {
            var user = new ApplicationUser { Email = "notconfirmed@example.com" };

            _userManagerMock.Setup(m => m.FindByEmailAsync("notconfirmed@example.com"))
                .ReturnsAsync(user);

            _userManagerMock.Setup(m => m.IsEmailConfirmedAsync(user))
                .ReturnsAsync(false);

            _model.Input.Email = "notconfirmed@example.com";

            var result = await _model.OnPostAsync();

            Assert.That(result, Is.InstanceOf<RedirectToPageResult>());
            var redirect = (RedirectToPageResult)result;
            Assert.That(redirect.PageName, Is.EqualTo("./ForgotPasswordConfirmation"));
        }
    }
}