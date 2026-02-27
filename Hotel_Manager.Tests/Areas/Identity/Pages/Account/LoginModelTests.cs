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
using System.Threading.Tasks;

namespace HotelManager.Tests.Areas.Identity.Pages.Account
{
    [TestFixture]
    public class LoginModelTests
    {
        private LoginModel _model = null!;
        private Mock<SignInManager<ApplicationUser>> _signInManagerMock = null!;
        private Mock<ILogger<LoginModel>> _loggerMock = null!;
        private Mock<IUrlHelper> _urlHelperMock = null!;

        [SetUp]
        public void Setup()
        {
            var userManagerMock = new Mock<UserManager<ApplicationUser>>(
                new Mock<IUserStore<ApplicationUser>>().Object,
                null!, null!, null!, null!, null!, null!, null!, null!
            );

            var contextAccessorMock = new Mock<IHttpContextAccessor>();
            var claimsFactoryMock = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();

            _signInManagerMock = new Mock<SignInManager<ApplicationUser>>(
                userManagerMock.Object,
                contextAccessorMock.Object,
                claimsFactoryMock.Object,
                null!, null!, null!, null!
            );

            _loggerMock = new Mock<ILogger<LoginModel>>();

            _urlHelperMock = new Mock<IUrlHelper>();
            _urlHelperMock.Setup(u => u.Content("~/")).Returns("/");

            _model = new LoginModel(_signInManagerMock.Object, _loggerMock.Object)
            {
                Input = new LoginModel.InputModel()
            };

            _model.Url = _urlHelperMock.Object;
        }

        private void SetupHttpContextWithAuthService(string returnUrl = null)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("localhost");
            httpContext.Request.PathBase = new PathString("");

            var authServiceMock = new Mock<IAuthenticationService>();
            authServiceMock.Setup(s => s.SignOutAsync(It.IsAny<HttpContext>(), IdentityConstants.ExternalScheme, null))
                .Returns(Task.CompletedTask);

            httpContext.RequestServices = new MockServiceProvider(authServiceMock.Object);

            _model.PageContext = new PageContext { HttpContext = httpContext };
            _model.ReturnUrl = returnUrl ?? "~/";
        }

        [Test]
        public async Task OnGetAsync_WithErrorMessage_AddsToModelState()
        {
            SetupHttpContextWithAuthService();
            _model.ErrorMessage = "Test error";

            await _model.OnGetAsync();

            Assert.That(_model.ModelState.ContainsKey(string.Empty), Is.True);
            Assert.That(_model.ModelState[string.Empty].Errors.Any(e => e.ErrorMessage == "Test error"), Is.True);
        }

        [Test]
        public async Task OnGetAsync_ClearsExternalCookie()
        {
            SetupHttpContextWithAuthService();

            var authServiceMock = new Mock<IAuthenticationService>();
            authServiceMock.Setup(s => s.SignOutAsync(It.IsAny<HttpContext>(), IdentityConstants.ExternalScheme, null))
                .Returns(Task.CompletedTask);

            _model.PageContext.HttpContext.RequestServices = new MockServiceProvider(authServiceMock.Object);

            await _model.OnGetAsync();

            authServiceMock.Verify(s => s.SignOutAsync(
                It.IsAny<HttpContext>(),
                IdentityConstants.ExternalScheme,
                null
            ), Times.Once);
        }

        [Test]
        public async Task OnGetAsync_LoadsExternalLogins()
        {
            SetupHttpContextWithAuthService();

            var schemes = new List<AuthenticationScheme>
            {
                new AuthenticationScheme("Google", "Google", typeof(Microsoft.AspNetCore.Authentication.IAuthenticationHandler)),
                new AuthenticationScheme("Facebook", "Facebook", typeof(Microsoft.AspNetCore.Authentication.IAuthenticationHandler))
            };

            _signInManagerMock.Setup(m => m.GetExternalAuthenticationSchemesAsync())
                .ReturnsAsync(schemes);

            await _model.OnGetAsync();

            Assert.That(_model.ExternalLogins, Has.Count.EqualTo(2));
            Assert.That(_model.ExternalLogins.Any(s => s.Name == "Google"), Is.True);
        }

        [Test]
        public async Task OnPostAsync_InvalidModelState_ReturnsPage()
        {
            SetupHttpContextWithAuthService();
            _model.Input = new LoginModel.InputModel();
            _model.ModelState.AddModelError("Input.Email", "Required");

            var result = await _model.OnPostAsync();

            Assert.That(result, Is.InstanceOf<PageResult>());
        }

        [Test]
        public async Task OnPostAsync_SuccessfulLogin_RedirectsToReturnUrl()
        {
            SetupHttpContextWithAuthService("/dashboard");

            _model.Input = new LoginModel.InputModel
            {
                Email = "user@example.com",
                Password = "Pass123!",
                RememberMe = true
            };

            _signInManagerMock.Setup(m => m.PasswordSignInAsync(
                _model.Input.Email,
                _model.Input.Password,
                _model.Input.RememberMe,
                false
            )).ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

            var result = await _model.OnPostAsync("/dashboard");

            Assert.That(result, Is.InstanceOf<LocalRedirectResult>());
            var redirect = (LocalRedirectResult)result;
            Assert.That(redirect.Url, Is.EqualTo("/dashboard"));
        }

        [Test]
        public async Task OnPostAsync_RequiresTwoFactor_RedirectsTo2faPage()
        {
            SetupHttpContextWithAuthService("/dashboard");

            _model.Input = new LoginModel.InputModel
            {
                Email = "2fa@example.com",
                Password = "Pass123!",
                RememberMe = false
            };

            _signInManagerMock.Setup(m => m.PasswordSignInAsync(
                _model.Input.Email,
                _model.Input.Password,
                _model.Input.RememberMe,
                false
            )).ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.TwoFactorRequired);

            var result = await _model.OnPostAsync("/dashboard");

            Assert.That(result, Is.InstanceOf<RedirectToPageResult>());
            var redirect = (RedirectToPageResult)result;
            Assert.That(redirect.PageName, Is.EqualTo("./LoginWith2fa"));
            Assert.That(redirect.RouteValues["ReturnUrl"], Is.EqualTo("/dashboard"));
            Assert.That(redirect.RouteValues["RememberMe"], Is.EqualTo(false));
        }

        [Test]
        public async Task OnPostAsync_AccountLockedOut_RedirectsToLockoutPage()
        {
            SetupHttpContextWithAuthService();

            _model.Input = new LoginModel.InputModel
            {
                Email = "locked@example.com",
                Password = "Pass123!",
                RememberMe = false
            };

            _signInManagerMock.Setup(m => m.PasswordSignInAsync(
                _model.Input.Email,
                _model.Input.Password,
                _model.Input.RememberMe,
                false
            )).ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.LockedOut);

            var result = await _model.OnPostAsync();

            Assert.That(result, Is.InstanceOf<RedirectToPageResult>());
            var redirect = (RedirectToPageResult)result;
            Assert.That(redirect.PageName, Is.EqualTo("./Lockout"));
        }

        [Test]
        public async Task OnPostAsync_InvalidCredentials_AddsModelErrorAndReturnsPage()
        {
            SetupHttpContextWithAuthService();

            _model.Input = new LoginModel.InputModel
            {
                Email = "wrong@example.com",
                Password = "wrong",
                RememberMe = false
            };

            _signInManagerMock.Setup(m => m.PasswordSignInAsync(
                _model.Input.Email,
                _model.Input.Password,
                _model.Input.RememberMe,
                false
            )).ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

            var result = await _model.OnPostAsync();

            Assert.That(result, Is.InstanceOf<PageResult>());
            Assert.That(_model.ModelState.ContainsKey(string.Empty), Is.True);
            Assert.That(_model.ModelState[string.Empty].Errors.Any(e => e.ErrorMessage == "Invalid login attempt."), Is.True);
        }
    }

    internal class MockServiceProvider : IServiceProvider
    {
        private readonly IAuthenticationService _authService;

        public MockServiceProvider(IAuthenticationService authService)
        {
            _authService = authService;
        }

        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(IAuthenticationService))
                return _authService;

            return null;
        }
    }
}