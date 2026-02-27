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

namespace HotelManager.Tests.Areas.Identity.Pages.Account
{
    [TestFixture]
    public class ResetPasswordModelTests
    {
        private Mock<UserManager<ApplicationUser>> _userManagerMock = null!;
        private ResetPasswordModel _pageModel = null!;

        [SetUp]
        public void Setup()
        {
            _userManagerMock = new Mock<UserManager<ApplicationUser>>(
                new Mock<IUserStore<ApplicationUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!
            );

            _pageModel = new ResetPasswordModel(_userManagerMock.Object);

            // Anonymous context (ResetPassword page is public)
            _pageModel.PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        [Test]
        public void OnGet_CodeIsNull_ReturnsBadRequest()
        {
            var result = _pageModel.OnGet(null) as BadRequestObjectResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.StatusCode, Is.EqualTo(400));
            Assert.That(result.Value, Is.EqualTo("A code must be supplied for password reset."));
        }

        [Test]
        public void OnGet_ValidCode_DecodesAndSetsInput_ReturnsPage()
        {
            var code = "encoded-code";
            var decodedBytes = Encoding.UTF8.GetBytes("real-reset-code");
            var base64UrlEncoded = WebEncoders.Base64UrlEncode(decodedBytes);

            var result = _pageModel.OnGet(base64UrlEncoded) as PageResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(_pageModel.Input, Is.Not.Null);
            Assert.That(_pageModel.Input.Code, Is.EqualTo("real-reset-code"));
        }

        [Test]
        public async Task OnPostAsync_InvalidModelState_ReturnsPage()
        {
            var input = new ResetPasswordModel.InputModel
            {
                Email = "invalid",
                Password = "short",
                ConfirmPassword = "mismatch",
                Code = "code123"
            };

            _pageModel.Input = input;
            _pageModel.ModelState.AddModelError("Password", "Password too short");

            var result = await _pageModel.OnPostAsync() as PageResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(_pageModel.ModelState.IsValid, Is.False);
        }

        [Test]
        public async Task OnPostAsync_UserNotFound_RedirectsToConfirmation()
        {
            _pageModel.Input = new ResetPasswordModel.InputModel
            {
                Email = "notfound@example.com",
                Password = "NewPass123!",
                ConfirmPassword = "NewPass123!",
                Code = "valid-code"
            };

            _userManagerMock.Setup(m => m.FindByEmailAsync("notfound@example.com"))
                .ReturnsAsync((ApplicationUser?)null);

            var result = await _pageModel.OnPostAsync() as RedirectToPageResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.PageName, Is.EqualTo("./ResetPasswordConfirmation"));
        }

        [Test]
        public async Task OnPostAsync_ResetSucceeds_RedirectsToConfirmation()
        {
            var user = new ApplicationUser { Id = "user-id-123", Email = "user@example.com" };

            _pageModel.Input = new ResetPasswordModel.InputModel
            {
                Email = "user@example.com",
                Password = "NewPass123!",
                ConfirmPassword = "NewPass123!",
                Code = "valid-code"
            };

            _userManagerMock.Setup(m => m.FindByEmailAsync("user@example.com"))
                .ReturnsAsync(user);

            _userManagerMock.Setup(m => m.ResetPasswordAsync(user, "valid-code", "NewPass123!"))
                .ReturnsAsync(IdentityResult.Success);

            var result = await _pageModel.OnPostAsync() as RedirectToPageResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.PageName, Is.EqualTo("./ResetPasswordConfirmation"));
        }

        [Test]
        public async Task OnPostAsync_ResetFails_AddsErrorsAndReturnsPage()
        {
            var user = new ApplicationUser { Id = "user-id-123", Email = "user@example.com" };

            _pageModel.Input = new ResetPasswordModel.InputModel
            {
                Email = "user@example.com",
                Password = "NewPass123!",
                ConfirmPassword = "NewPass123!",
                Code = "invalid-code"
            };

            _userManagerMock.Setup(m => m.FindByEmailAsync("user@example.com"))
                .ReturnsAsync(user);

            _userManagerMock.Setup(m => m.ResetPasswordAsync(user, "invalid-code", "NewPass123!"))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError { Description = "Invalid token" },
                    new IdentityError { Description = "Password too weak" }
                ));

            var result = await _pageModel.OnPostAsync() as PageResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(_pageModel.ModelState[""].Errors.Count, Is.EqualTo(2));
            Assert.That(_pageModel.ModelState[""].Errors[0].ErrorMessage, Is.EqualTo("Invalid token"));
        }
    }
}