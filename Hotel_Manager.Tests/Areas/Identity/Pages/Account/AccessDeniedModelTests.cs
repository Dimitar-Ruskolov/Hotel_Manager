using Hotel_Manager.Areas.Identity.Pages.Account;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NUnit.Framework;

namespace HotelManager.Tests.Areas.Identity.Pages.Account
{
    [TestFixture]
    public class AccessDeniedModelTests
    {
        private AccessDeniedModel _model;

        [SetUp]
        public void SetUp()
        {
            _model = new AccessDeniedModel();
        }

        [Test]
        public void OnGet_DoesNotThrowException()
        {
            Assert.DoesNotThrow(() => _model.OnGet());
        }

        [Test]
        public void OnGet_PageModelRemainsInValidState()
        {
            _model.OnGet();

            Assert.That(_model, Is.Not.Null);
            Assert.That(_model.ModelState.IsValid, Is.True); 
        }
    }
}