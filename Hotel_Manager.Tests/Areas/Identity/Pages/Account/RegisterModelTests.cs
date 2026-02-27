using Hotel_Manager.Areas.Identity.Pages.Account;
using Hotel_Manager.Models;
using Microsoft.AspNetCore.Authentication;
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

namespace HotelManager.Tests.Areas.Identity.Pages.Account
{
    [TestFixture]
    public class RegisterModelTests
    {
        private Mock<UserManager<ApplicationUser>> _userManagerMock = null!;
        private Mock<SignInManager<ApplicationUser>> _signInManagerMock = null!;
        private Mock<IUserStore<ApplicationUser>> _userStoreMock = null!;
        private Mock<ILogger<RegisterModel>> _loggerMock = null!;
        private RegisterModel _pageModel = null!;

        [SetUp]
        public void Setup()
        {
            _userStoreMock = new Mock<IUserStore<ApplicationUser>>();
            _userStoreMock.As<IUserEmailStore<ApplicationUser>>();

            _userManagerMock = new Mock<UserManager<ApplicationUser>>(
                _userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!
            );

            _userManagerMock.Setup(m => m.SupportsUserEmail).Returns(true);

            var contextAccessorMock = new Mock<IHttpContextAccessor>();
            var claimsFactoryMock = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
            _signInManagerMock = new Mock<SignInManager<ApplicationUser>>(
                _userManagerMock.Object,
                contextAccessorMock.Object,
                claimsFactoryMock.Object,
                null!, null!, null!, null!
            );

            _loggerMock = new Mock<ILogger<RegisterModel>>();

            _pageModel = new RegisterModel(
                _userManagerMock.Object,
                _userStoreMock.Object,
                _signInManagerMock.Object,
                _loggerMock.Object
            );

            _pageModel.PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }



        [Test]
        public async Task OnPostAsync_InvalidModelState_ReturnsPage()
        {
            var input = new RegisterModel.InputModel
            {
                Email = "invalid",
                FirstName = "",
                LastName = "Doe",
                Age = 25,
                Password = "Pass123!",
                ConfirmPassword = "Pass123!"
            };

            _pageModel.Input = input;
            _pageModel.ModelState.AddModelError("Email", "Invalid email");

            var result = await _pageModel.OnPostAsync("/return-url") as PageResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(_pageModel.ModelState.IsValid, Is.False);
        }

        [Test]
        public async Task OnPostAsync_ValidInput_CreatesUserAddsGuestRoleSignsInAndRedirects()
        {
            var input = new RegisterModel.InputModel
            {
                Email = "newuser@example.com",
                FirstName = "New",
                LastName = "User",
                Age = 28,
                Password = "StrongPass123!",
                ConfirmPassword = "StrongPass123!"
            };

            var user = new ApplicationUser();

            _pageModel.Input = input;
            _pageModel.ReturnUrl = "/welcome";

            _userManagerMock.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), "StrongPass123!"))
                .ReturnsAsync(IdentityResult.Success)
                .Callback<ApplicationUser, string>((u, p) => user = u);

            _userManagerMock.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Guest"))
                .ReturnsAsync(IdentityResult.Success);

            _signInManagerMock.Setup(m => m.SignInAsync(It.IsAny<ApplicationUser>(), false, null))
                .Returns(Task.CompletedTask);

            var result = await _pageModel.OnPostAsync("/welcome") as LocalRedirectResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Url, Is.EqualTo("/welcome"));

            _userManagerMock.Verify(m => m.CreateAsync(It.Is<ApplicationUser>(u =>
                u.Email == "newuser@example.com" &&
                u.FirstName == "New" &&
                u.LastName == "User" &&
                u.Age == 28 &&
                u.IsActive == true
            ), "StrongPass123!"), Times.Once);

            _userManagerMock.Verify(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Guest"), Times.Once);

            _signInManagerMock.Verify(m => m.SignInAsync(It.IsAny<ApplicationUser>(), false, null), Times.Once);

            _loggerMock.VerifyLog(LogLevel.Information, "User registered.", Times.Once());
        }

    }
}