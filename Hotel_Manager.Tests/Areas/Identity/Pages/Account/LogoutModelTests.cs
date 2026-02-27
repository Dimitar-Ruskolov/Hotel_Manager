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
using System.Threading.Tasks;

namespace HotelManager.Tests.Areas.Identity.Pages.Account
{
    [TestFixture]
    public class LogoutModelTests
    {
        private LogoutModel _model = null!;
        private Mock<SignInManager<ApplicationUser>> _signInManagerMock = null!;
        private Mock<ILogger<LogoutModel>> _loggerMock = null!;

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

            _loggerMock = new Mock<ILogger<LogoutModel>>();

            _model = new LogoutModel(_signInManagerMock.Object, _loggerMock.Object);
        }

        private void SetupHttpContext(string returnUrl = null)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("localhost");
            httpContext.Request.PathBase = new PathString("");

            _model.PageContext = new PageContext { HttpContext = httpContext };
        }

        [Test]
        public async Task OnPost_WithReturnUrl_SignsOutAndRedirectsToGivenUrl()
        {
            SetupHttpContext();

            var returnUrl = "/dashboard";

            var result = await _model.OnPost(returnUrl);

            Assert.That(result, Is.InstanceOf<LocalRedirectResult>());
            var redirect = (LocalRedirectResult)result;
            Assert.That(redirect.Url, Is.EqualTo("/dashboard"));

            _signInManagerMock.Verify(m => m.SignOutAsync(), Times.Once);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("User logged out.")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
                Times.Once()
            );
        }

        [Test]
        public async Task OnPost_NoReturnUrl_SignsOutAndRedirectsToCurrentPage()
        {
            SetupHttpContext();

            var result = await _model.OnPost(null);

            Assert.That(result, Is.InstanceOf<RedirectToPageResult>());
            var redirect = (RedirectToPageResult)result;
            Assert.That(redirect.PageName, Is.Null);

            _signInManagerMock.Verify(m => m.SignOutAsync(), Times.Once);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("User logged out.")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
                Times.Once()
            );
        }

        [Test]
        public async Task OnPost_SignOutCalledEvenIfLoggerFails()
        {
            SetupHttpContext();

            var result = await _model.OnPost("/home");

            _signInManagerMock.Verify(m => m.SignOutAsync(), Times.Once);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("User logged out.")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
                Times.Once()
            );

            Assert.That(result, Is.InstanceOf<LocalRedirectResult>());
        }
    }
}