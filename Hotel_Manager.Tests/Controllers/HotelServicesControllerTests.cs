using Hotel_Manager.Controllers;
using Hotel_Manager.Data;
using Hotel_Manager.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace HotelManager.Tests.Controllers
{
    [TestFixture]
    public class HotelServicesControllerTests
    {
        private ApplicationDbContext _context = null!;
        private HotelServicesController _controller = null!;

        [SetUp]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"HotelServicesTest_{Guid.NewGuid()}")
                .Options;

            _context = new ApplicationDbContext(options);
            _controller = new HotelServicesController(_context);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Role, "Admin")
            };

            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }

        [TearDown]
        public void TearDown()
        {
            _context?.Database.EnsureDeleted();
            _context?.Dispose();
        }

        [Test]
        public async Task Index_ReturnsViewWithAllServices()
        {
            var services = new List<HotelService>
            {
                new HotelService { Id = 1, Name = "Breakfast", Price = 15m },
                new HotelService { Id = 2, Name = "Parking", Price = 10m },
                new HotelService { Id = 3, Name = "Spa", Price = 50m }
            };

            _context.HotelServices.AddRange(services);
            await _context.SaveChangesAsync();

            var result = await _controller.Index() as ViewResult;

            Assert.That(result, Is.Not.Null);

            var model = result!.Model as List<HotelService>;
            Assert.That(model, Is.Not.Null);
            Assert.That(model!.Count, Is.EqualTo(3));
            Assert.That(model[0].Name, Is.EqualTo("Breakfast"));
        }

        [Test]
        public async Task Index_EmptyDatabase_ReturnsEmptyList()
        {
            var result = await _controller.Index() as ViewResult;

            Assert.That(result, Is.Not.Null);

            var model = result!.Model as List<HotelService>;
            Assert.That(model, Is.Not.Null);
            Assert.That(model, Is.Empty);
        }

        [Test]
        public void Create_Get_ReturnsDefaultView()
        {
            var result = _controller.Create() as ViewResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.ViewName, Is.Null);
        }

        [Test]
        public async Task Create_Post_ValidModel_AddsServiceAndRedirects()
        {
            var service = new HotelService
            {
                Name = "Gym Access",
                Price = 25m
            };

            var result = await _controller.Create(service) as RedirectToActionResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.ActionName, Is.EqualTo(nameof(HotelServicesController.Index)));

            var saved = await _context.HotelServices.FirstOrDefaultAsync(s => s.Name == "Gym Access");
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved!.Price, Is.EqualTo(25m));
        }

        [Test]
        public async Task Create_Post_InvalidModel_ReturnsViewWithModel()
        {
            var service = new HotelService { Name = "" };

            _controller.ModelState.AddModelError("Name", "Name is required");

            var result = await _controller.Create(service) as ViewResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Model, Is.SameAs(service));
        }

        [Test]
        public async Task Edit_Get_ExistingId_ReturnsViewWithService()
        {
            var service = new HotelService { Id = 10, Name = "Laundry", Price = 8m };
            _context.HotelServices.Add(service);
            await _context.SaveChangesAsync();

            var result = await _controller.Edit(10) as ViewResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Model, Is.TypeOf<HotelService>());

            var model = result.Model as HotelService;
            Assert.That(model!.Id, Is.EqualTo(10));
            Assert.That(model.Name, Is.EqualTo("Laundry"));
        }

        [Test]
        public async Task Edit_Get_NonExistingId_ReturnsNotFound()
        {
            var result = await _controller.Edit(999) as NotFoundResult;
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public async Task Edit_Post_ValidModel_UpdatesAndRedirects()
        {
            var original = new HotelService { Id = 20, Name = "Old Service", Price = 30m };
            _context.HotelServices.Add(original);
            await _context.SaveChangesAsync();

            _context.Entry(original).State = EntityState.Detached;

            var updated = new HotelService
            {
                Id = 20,
                Name = "Updated Service",
                Price = 35m
            };

            var result = await _controller.Edit(20, updated) as RedirectToActionResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.ActionName, Is.EqualTo(nameof(HotelServicesController.Index)));

            var saved = await _context.HotelServices.FindAsync(20);
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved!.Name, Is.EqualTo("Updated Service"));
            Assert.That(saved.Price, Is.EqualTo(35m));
        }

        [Test]
        public async Task Edit_Post_IdMismatch_ReturnsNotFound()
        {
            var service = new HotelService { Id = 30, Name = "Test", Price = 10m };

            var result = await _controller.Edit(40, service) as NotFoundResult;

            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public async Task Edit_Post_InvalidModel_ReturnsViewWithModel()
        {
            var service = new HotelService { Id = 50, Name = "", Price = 20m };

            _controller.ModelState.AddModelError("Name", "Name is required");

            var result = await _controller.Edit(50, service) as ViewResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Model, Is.SameAs(service));
        }

        [Test]
        public async Task Details_ValidId_ReturnsViewWithService()
        {
            var service = new HotelService { Id = 60, Name = "Pool", Price = 12m };
            _context.HotelServices.Add(service);
            await _context.SaveChangesAsync();

            var result = await _controller.Details(60) as ViewResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Model, Is.TypeOf<HotelService>());

            var model = result.Model as HotelService;
            Assert.That(model!.Id, Is.EqualTo(60));
            Assert.That(model.Name, Is.EqualTo("Pool"));
        }

        [Test]
        public async Task Details_NullId_ReturnsNotFound()
        {
            var result = await _controller.Details(null) as NotFoundResult;
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public async Task Details_NonExistingId_ReturnsNotFound()
        {
            var result = await _controller.Details(999) as NotFoundResult;
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public async Task Delete_Get_ExistingId_ReturnsViewWithService()
        {
            var service = new HotelService { Id = 70, Name = "Massage", Price = 45m };
            _context.HotelServices.Add(service);
            await _context.SaveChangesAsync();

            var result = await _controller.Delete(70) as ViewResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Model, Is.TypeOf<HotelService>());

            var model = result.Model as HotelService;
            Assert.That(model!.Id, Is.EqualTo(70));
        }

        [Test]
        public async Task Delete_Get_NonExistingId_ReturnsNotFound()
        {
            var result = await _controller.Delete(999) as NotFoundResult;
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public async Task DeleteConfirmed_Post_ExistingId_DeletesAndRedirects()
        {
            var service = new HotelService { Id = 80, Name = "Sauna", Price = 18m };
            _context.HotelServices.Add(service);
            await _context.SaveChangesAsync();

            var result = await _controller.DeleteConfirmed(80) as RedirectToActionResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.ActionName, Is.EqualTo(nameof(HotelServicesController.Index)));

            var deleted = await _context.HotelServices.FindAsync(80);
            Assert.That(deleted, Is.Null);
        }

        [Test]
        public async Task DeleteConfirmed_Post_NonExistingId_RedirectsWithoutDelete()
        {
            var result = await _controller.DeleteConfirmed(999) as RedirectToActionResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.ActionName, Is.EqualTo(nameof(HotelServicesController.Index)));
        }
    }
}