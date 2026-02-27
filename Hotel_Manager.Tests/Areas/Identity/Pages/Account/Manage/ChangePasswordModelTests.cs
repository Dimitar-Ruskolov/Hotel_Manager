using Hotel_Manager.Areas.Identity.Pages.Account.Manage;
using Hotel_Manager.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace HotelManager.Tests.Areas.Identity.Pages.Account.Manage
{
    [TestFixture]
    public class ChangePasswordModelTests
    {
        private Mock<UserManager<ApplicationUser>> _userManagerMock = null!;
        private Mock<SignInManager<ApplicationUser>> _signInManagerMock = null!;
        private Mock<ILogger<ChangePasswordModel>> _loggerMock = null!;
        private ChangePasswordModel _pageModel = null!;

        [SetUp]
        public void Setup()
        {
            var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
            _userManagerMock = new Mock<UserManager<ApplicationUser>>(
                userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!
            );

            var contextAccessorMock = new Mock<IHttpContextAccessor>();
            var claimsFactoryMock = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
            _signInManagerMock = new Mock<SignInManager<ApplicationUser>>(
                _userManagerMock.Object,
                contextAccessorMock.Object,
                claimsFactoryMock.Object,
                null!, null!, null!, null!
            );

            _loggerMock = new Mock<ILogger<ChangePasswordModel>>();

            _pageModel = new ChangePasswordModel(
                _userManagerMock.Object,
                _signInManagerMock.Object,
                _loggerMock.Object
            );

            // Authenticated user
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "user-id-123")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            _pageModel.PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }

        [Test]
        public async Task OnGetAsync_UserNotFound_ReturnsNotFound()
        {
            _userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync((ApplicationUser?)null);

            var result = await _pageModel.OnGetAsync();

            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task OnGetAsync_NoPasswordSet_RedirectsToSetPassword()
        {
            var user = new ApplicationUser { Id = "user-id-123" };

            _userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            _userManagerMock.Setup(m => m.HasPasswordAsync(user))
                .ReturnsAsync(false);

            var result = await _pageModel.OnGetAsync() as RedirectToPageResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.PageName, Is.EqualTo("./SetPassword"));
        }

        [Test]
        public async Task OnGetAsync_HasPassword_ReturnsPage()
        {
            var user = new ApplicationUser { Id = "user-id-123" };

            _userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            _userManagerMock.Setup(m => m.HasPasswordAsync(user))
                .ReturnsAsync(true);

            var result = await _pageModel.OnGetAsync() as PageResult;

            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public async Task OnPostAsync_InvalidModelState_ReturnsPage()
        {
            var input = new ChangePasswordModel.InputModel
            {
                OldPassword = "old",
                NewPassword = "new",
                ConfirmPassword = "mismatch"
            };

            _pageModel.Input = input;
            _pageModel.ModelState.AddModelError("ConfirmPassword", "Passwords do not match");

            var result = await _pageModel.OnPostAsync() as PageResult;

            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public async Task OnPostAsync_UserNotFound_ReturnsNotFound()
        {
            var input = new ChangePasswordModel.InputModel
            {
                OldPassword = "oldpass",
                NewPassword = "NewPass123!",
                ConfirmPassword = "NewPass123!"
            };

            _pageModel.Input = input;

            _userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync((ApplicationUser?)null);

            var result = await _pageModel.OnPostAsync() as NotFoundObjectResult;

            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public async Task OnPostAsync_ChangePasswordFails_AddsErrorsAndReturnsPage()
        {
            var user = new ApplicationUser { Id = "user-id-123" };

            var input = new ChangePasswordModel.InputModel
            {
                OldPassword = "wrong",
                NewPassword = "NewPass123!",
                ConfirmPassword = "NewPass123!"
            };

            _pageModel.Input = input;

            _userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            _userManagerMock.Setup(m => m.ChangePasswordAsync(user, "wrong", "NewPass123!"))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Incorrect password" }));

            var result = await _pageModel.OnPostAsync() as PageResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(_pageModel.ModelState[""].Errors.Any(e => e.ErrorMessage == "Incorrect password"), Is.True);
        }

        [Test]
        public async Task OnPostAsync_ChangePasswordSucceeds_RefreshesSignInLogsAndRedirects()
        {
            var user = new ApplicationUser { Id = "user-id-123" };

            var input = new ChangePasswordModel.InputModel
            {
                OldPassword = "oldpass",
                NewPassword = "NewPass123!",
                ConfirmPassword = "NewPass123!"
            };

            _pageModel.Input = input;

            _userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            _userManagerMock.Setup(m => m.ChangePasswordAsync(user, "oldpass", "NewPass123!"))
                .ReturnsAsync(IdentityResult.Success);

            _signInManagerMock.Setup(m => m.RefreshSignInAsync(user))
                .Returns(Task.CompletedTask);

            var result = await _pageModel.OnPostAsync() as RedirectToPageResult;

            Assert.That(result, Is.Not.Null);

            Assert.That(_pageModel.StatusMessage, Is.EqualTo("Your password has been changed."));

            _signInManagerMock.Verify(m => m.RefreshSignInAsync(user), Times.Once);

            _loggerMock.VerifyLog(LogLevel.Information, "User changed their password successfully.", Times.Once());
        }
    }
}