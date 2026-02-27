using Hotel_Manager.Areas.Identity.Pages.Account.Manage;
using Hotel_Manager.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace HotelManager.Tests.Areas.Identity.Pages.Account.Manage
{
    [TestFixture]
    public class IndexModelTests
    {
        private Mock<UserManager<ApplicationUser>> _userManagerMock = null!;
        private Mock<SignInManager<ApplicationUser>> _signInManagerMock = null!;
        private IndexModel _pageModel = null!;

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

            _pageModel = new IndexModel(
                _userManagerMock.Object,
                _signInManagerMock.Object
            );

            // Setup authenticated user
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

        private ApplicationUser CreateTestUser()
        {
            return new ApplicationUser
            {
                Id = "user-id-123",
                UserName = "testuser",
                Email = "test@example.com",
                FirstName = "John",
                LastName = "Doe",
                Age = 30,
                CreatedAt = DateTime.UtcNow.AddDays(-100),
                IsActive = true,
                PhoneNumber = "123456789"
            };
        }

        [Test]
        public async Task OnGetAsync_UserNotFound_ReturnsNotFound()
        {
            _userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync((ApplicationUser?)null);

            var result = await _pageModel.OnGetAsync() as NotFoundObjectResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.StatusCode, Is.EqualTo(404));
        }

        [Test]
        public async Task OnGetAsync_ValidUser_LoadsDataAndReturnsPage()
        {
            var user = CreateTestUser();

            _userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            _userManagerMock.Setup(m => m.GetUserNameAsync(user))
                .ReturnsAsync(user.UserName);

            _userManagerMock.Setup(m => m.GetEmailAsync(user))
                .ReturnsAsync(user.Email);

            _userManagerMock.Setup(m => m.GetPhoneNumberAsync(user))
                .ReturnsAsync(user.PhoneNumber);

            _userManagerMock.Setup(m => m.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "Guest", "User" });

            var result = await _pageModel.OnGetAsync() as PageResult;

            Assert.That(result, Is.Not.Null);

            Assert.That(_pageModel.Username, Is.EqualTo("testuser"));
            Assert.That(_pageModel.Email, Is.EqualTo("test@example.com"));
            Assert.That(_pageModel.FirstName, Is.EqualTo("John"));
            Assert.That(_pageModel.LastName, Is.EqualTo("Doe"));
            Assert.That(_pageModel.Age, Is.EqualTo(30));
            Assert.That(_pageModel.IsActive, Is.True);
            Assert.That(_pageModel.Roles, Is.EqualTo("Guest, User"));
            Assert.That(_pageModel.Input.PhoneNumber, Is.EqualTo("123456789"));
        }

        [Test]
        public async Task OnPostAsync_UserNotFound_ReturnsNotFound()
        {
            _userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync((ApplicationUser?)null);

            var result = await _pageModel.OnPostAsync() as NotFoundObjectResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.StatusCode, Is.EqualTo(404));
        }

        [Test]
        public async Task OnPostAsync_InvalidModelState_ReloadsDataAndReturnsPage()
        {
            var user = CreateTestUser();

            _userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            _userManagerMock.Setup(m => m.GetUserNameAsync(user))
                .ReturnsAsync(user.UserName);

            _userManagerMock.Setup(m => m.GetEmailAsync(user))
                .ReturnsAsync(user.Email);

            _userManagerMock.Setup(m => m.GetPhoneNumberAsync(user))
                .ReturnsAsync(user.PhoneNumber);

            _userManagerMock.Setup(m => m.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "Guest" });

            _pageModel.Input = new IndexModel.InputModel { PhoneNumber = "invalid" };
            _pageModel.ModelState.AddModelError("PhoneNumber", "Invalid phone");

            var result = await _pageModel.OnPostAsync() as PageResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(_pageModel.Username, Is.EqualTo("testuser"));
            Assert.That(_pageModel.Roles, Is.EqualTo("Guest"));
        }

        [Test]
        public async Task OnPostAsync_PhoneNumberUnchanged_RefreshesSignInAndRedirects()
        {
            var user = CreateTestUser();

            _userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            _userManagerMock.Setup(m => m.GetPhoneNumberAsync(user))
                .ReturnsAsync("123456789");

            _pageModel.Input = new IndexModel.InputModel { PhoneNumber = "123456789" };

            _signInManagerMock.Setup(m => m.RefreshSignInAsync(user))
                .Returns(Task.CompletedTask);

            var result = await _pageModel.OnPostAsync() as RedirectToPageResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(_pageModel.StatusMessage, Is.EqualTo("Your profile has been updated"));

            _signInManagerMock.Verify(m => m.RefreshSignInAsync(user), Times.Once);
            _userManagerMock.Verify(m => m.SetPhoneNumberAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task OnPostAsync_PhoneNumberChanged_SetsPhoneAndRefreshesSignIn()
        {
            var user = CreateTestUser();

            _userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            _userManagerMock.Setup(m => m.GetPhoneNumberAsync(user))
                .ReturnsAsync("old-number");

            _userManagerMock.Setup(m => m.SetPhoneNumberAsync(user, "new-number"))
                .ReturnsAsync(IdentityResult.Success);

            _signInManagerMock.Setup(m => m.RefreshSignInAsync(user))
                .Returns(Task.CompletedTask);

            _pageModel.Input = new IndexModel.InputModel { PhoneNumber = "new-number" };

            var result = await _pageModel.OnPostAsync() as RedirectToPageResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(_pageModel.StatusMessage, Is.EqualTo("Your profile has been updated"));

            _userManagerMock.Verify(m => m.SetPhoneNumberAsync(user, "new-number"), Times.Once);
            _signInManagerMock.Verify(m => m.RefreshSignInAsync(user), Times.Once);
        }

        [Test]
        public async Task OnPostAsync_SetPhoneNumberFails_SetsErrorMessageAndRedirects()
        {
            var user = CreateTestUser();

            _userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            _userManagerMock.Setup(m => m.GetPhoneNumberAsync(user))
                .ReturnsAsync("old-number");

            _userManagerMock.Setup(m => m.SetPhoneNumberAsync(user, "new-number"))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Phone update error" }));

            _pageModel.Input = new IndexModel.InputModel { PhoneNumber = "new-number" };

            var result = await _pageModel.OnPostAsync() as RedirectToPageResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(_pageModel.StatusMessage, Is.EqualTo("Unexpected error when trying to set phone number."));
        }
    }
}