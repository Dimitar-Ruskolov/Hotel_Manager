using Hotel_Manager.Areas.Identity.Pages.Account.Manage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NUnit.Framework;
using System;
using System.IO;

namespace HotelManager.Tests.Areas.Identity.Pages.Account.Manage
{
    [TestFixture]
    public class ManageNavPagesTests
    {
        [Test]
        public void StaticProperties_ReturnCorrectPageNames()
        {
            Assert.That(ManageNavPages.Index, Is.EqualTo("Index"));
            Assert.That(ManageNavPages.Email, Is.EqualTo("Email"));
            Assert.That(ManageNavPages.ChangePassword, Is.EqualTo("ChangePassword"));
            Assert.That(ManageNavPages.DownloadPersonalData, Is.EqualTo("DownloadPersonalData"));
            Assert.That(ManageNavPages.DeletePersonalData, Is.EqualTo("DeletePersonalData"));
            Assert.That(ManageNavPages.ExternalLogins, Is.EqualTo("ExternalLogins"));
            Assert.That(ManageNavPages.PersonalData, Is.EqualTo("PersonalData"));
            Assert.That(ManageNavPages.TwoFactorAuthentication, Is.EqualTo("TwoFactorAuthentication"));
        }

        [TestCase(null, "Index", "active")] 
        [TestCase("Index", "Index", "active")]
        [TestCase("Email", "Index", null)]
        [TestCase("index", "Index", "active")] 
        public void PageNavClass_ReturnsActiveOrNull(string? activePage, string currentPage, string? expected)
        {
            var viewContext = CreateViewContext(activePage, currentPage);

            var result = ManageNavPages.PageNavClass(viewContext, currentPage);

            Assert.That(result, Is.EqualTo(expected));
        }

        [TestCase("Index", "Index", "active")]
        [TestCase("Email", "Email", null)]
        [TestCase("index", "Index", "active")]
        [TestCase("INDEX", "Index", "active")]
        [TestCase("OtherPage", "Index", null)]
        public void IndexNavClass_ReturnsCorrectActiveClass(string? activePage, string expectedPage, string? expectedClass)
        {
            var viewContext = CreateViewContext(activePage, expectedPage);

            var result = ManageNavPages.IndexNavClass(viewContext);

            Assert.That(result, Is.EqualTo(expectedClass));
        }

        [TestCase("Email", "Email", "active")]
        [TestCase("Index", "Email", null)]
        public void EmailNavClass_ReturnsCorrectActiveClass(string? activePage, string expectedPage, string? expectedClass)
        {
            var viewContext = CreateViewContext(activePage, expectedPage);

            var result = ManageNavPages.EmailNavClass(viewContext);

            Assert.That(result, Is.EqualTo(expectedClass));
        }

        [TestCase("ChangePassword", "ChangePassword", "active")]
        [TestCase("Index", "ChangePassword", null)]
        public void ChangePasswordNavClass_ReturnsCorrectActiveClass(string? activePage, string expectedPage, string? expectedClass)
        {
            var viewContext = CreateViewContext(activePage, expectedPage);

            var result = ManageNavPages.ChangePasswordNavClass(viewContext);

            Assert.That(result, Is.EqualTo(expectedClass));
        }

        [TestCase("DownloadPersonalData", "DownloadPersonalData", "active")]
        [TestCase("Index", "DownloadPersonalData", null)]
        public void DownloadPersonalDataNavClass_ReturnsCorrectActiveClass(string? activePage, string expectedPage, string? expectedClass)
        {
            var viewContext = CreateViewContext(activePage, expectedPage);

            var result = ManageNavPages.DownloadPersonalDataNavClass(viewContext);

            Assert.That(result, Is.EqualTo(expectedClass));
        }

        [TestCase("DeletePersonalData", "DeletePersonalData", "active")]
        [TestCase("Index", "DeletePersonalData", null)]
        public void DeletePersonalDataNavClass_ReturnsCorrectActiveClass(string? activePage, string expectedPage, string? expectedClass)
        {
            var viewContext = CreateViewContext(activePage, expectedPage);

            var result = ManageNavPages.DeletePersonalDataNavClass(viewContext);

            Assert.That(result, Is.EqualTo(expectedClass));
        }

        [TestCase("ExternalLogins", "ExternalLogins", "active")]
        [TestCase("Index", "ExternalLogins", null)]
        public void ExternalLoginsNavClass_ReturnsCorrectActiveClass(string? activePage, string expectedPage, string? expectedClass)
        {
            var viewContext = CreateViewContext(activePage, expectedPage);

            var result = ManageNavPages.ExternalLoginsNavClass(viewContext);

            Assert.That(result, Is.EqualTo(expectedClass));
        }

        [TestCase("PersonalData", "PersonalData", "active")]
        [TestCase("Index", "PersonalData", null)]
        public void PersonalDataNavClass_ReturnsCorrectActiveClass(string? activePage, string expectedPage, string? expectedClass)
        {
            var viewContext = CreateViewContext(activePage, expectedPage);

            var result = ManageNavPages.PersonalDataNavClass(viewContext);

            Assert.That(result, Is.EqualTo(expectedClass));
        }

        [TestCase("TwoFactorAuthentication", "TwoFactorAuthentication", "active")]
        [TestCase("Index", "TwoFactorAuthentication", null)]
        public void TwoFactorAuthenticationNavClass_ReturnsCorrectActiveClass(string? activePage, string expectedPage, string? expectedClass)
        {
            var viewContext = CreateViewContext(activePage, expectedPage);

            var result = ManageNavPages.TwoFactorAuthenticationNavClass(viewContext);

            Assert.That(result, Is.EqualTo(expectedClass));
        }

        private ViewContext CreateViewContext(string? activePage, string currentPage)
        {
            var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
            if (activePage != null)
            {
                viewData["ActivePage"] = activePage;
            }

            var actionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor
            {
                DisplayName = $"Pages/Account/Manage/{currentPage}"
            };

            return new ViewContext
            {
                ViewData = viewData,
                ActionDescriptor = actionDescriptor
            };
        }
    }
}