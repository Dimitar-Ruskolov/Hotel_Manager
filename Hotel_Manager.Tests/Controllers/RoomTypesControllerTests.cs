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
    public class RoomTypesControllerTests
    {
        private ApplicationDbContext _context = null!;
        private RoomTypesController _controller = null!;

        [SetUp]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"RoomTypesTest_{Guid.NewGuid()}")
                .Options;

            _context = new ApplicationDbContext(options);
            _controller = new RoomTypesController(_context);

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
            _controller?.Dispose();
            _context?.Database.EnsureDeleted();
            _context?.Dispose();
        }

        [Test]
        public async Task Index_ReturnsViewWithAllRoomTypes()
        {
            var roomTypes = new List<RoomType>
            {
                new RoomType { Id = 1, Name = "Single", Capacity = 1, PricePerNight = 80m },
                new RoomType { Id = 2, Name = "Double", Capacity = 2, PricePerNight = 120m },
                new RoomType { Id = 3, Name = "Suite", Capacity = 4, PricePerNight = 250m }
            };

            _context.RoomTypes.AddRange(roomTypes);
            await _context.SaveChangesAsync();

            var result = await _controller.Index() as ViewResult;

            Assert.That(result, Is.Not.Null);

            var model = result!.Model as List<RoomType>;
            Assert.That(model, Is.Not.Null);
            Assert.That(model!.Count(), Is.EqualTo(3));
            Assert.That(model[0].Name, Is.EqualTo("Single"));
        }

        [Test]
        public async Task Index_EmptyDatabase_ReturnsEmptyList()
        {
            var result = await _controller.Index() as ViewResult;

            Assert.That(result, Is.Not.Null);

            var model = result!.Model as List<RoomType>;
            Assert.That(model, Is.Not.Null);
            Assert.That(model!.Count(), Is.EqualTo(0));
        }

        [Test]
        public void Create_Get_ReturnsDefaultView()
        {
            var result = _controller.Create() as ViewResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.ViewName, Is.Null);
        }

        [Test]
        public async Task Create_Post_ValidModel_AddsRoomTypeAndRedirects()
        {
            var roomType = new RoomType
            {
                Name = "Family Room",
                Capacity = 4,
                PricePerNight = 180m
            };

            var result = await _controller.Create(roomType) as RedirectToActionResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.ActionName, Is.EqualTo(nameof(RoomTypesController.Index)));

            var saved = await _context.RoomTypes.FirstOrDefaultAsync(r => r.Name == "Family Room");
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved!.Capacity, Is.EqualTo(4));
            Assert.That(saved.PricePerNight, Is.EqualTo(180m));
        }

        [Test]
        public async Task Create_Post_InvalidModel_ReturnsViewWithModel()
        {
            var roomType = new RoomType { Name = "" };

            _controller.ModelState.AddModelError("Name", "Name is required");

            var result = await _controller.Create(roomType) as ViewResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Model, Is.SameAs(roomType));
        }

        [Test]
        public async Task Edit_Get_ExistingId_ReturnsViewWithRoomType()
        {
            var roomType = new RoomType { Id = 10, Name = "Deluxe", Capacity = 2, PricePerNight = 150m };
            _context.RoomTypes.Add(roomType);
            await _context.SaveChangesAsync();

            var result = await _controller.Edit(10) as ViewResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Model, Is.TypeOf<RoomType>());

            var model = result.Model as RoomType;
            Assert.That(model!.Id, Is.EqualTo(10));
            Assert.That(model.Name, Is.EqualTo("Deluxe"));
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
            var original = new RoomType { Id = 20, Name = "Standard", Capacity = 2, PricePerNight = 100m };
            _context.RoomTypes.Add(original);
            await _context.SaveChangesAsync();

            _context.Entry(original).State = EntityState.Detached;

            var updated = new RoomType
            {
                Id = 20,
                Name = "Superior",
                Capacity = 3,
                PricePerNight = 130m
            };

            var result = await _controller.Edit(20, updated) as RedirectToActionResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.ActionName, Is.EqualTo(nameof(RoomTypesController.Index)));

            var saved = await _context.RoomTypes.FindAsync(20);
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved!.Name, Is.EqualTo("Superior"));
            Assert.That(saved.Capacity, Is.EqualTo(3));
            Assert.That(saved.PricePerNight, Is.EqualTo(130m));
        }

        [Test]
        public async Task Edit_Post_IdMismatch_ReturnsNotFound()
        {
            var roomType = new RoomType { Id = 30, Name = "Test", Capacity = 1, PricePerNight = 50m };

            var result = await _controller.Edit(40, roomType) as NotFoundResult;

            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public async Task Edit_Post_InvalidModel_ReturnsViewWithModel()
        {
            var roomType = new RoomType { Id = 50, Name = "", Capacity = 2, PricePerNight = 90m };

            _controller.ModelState.AddModelError("Name", "Name is required");

            var result = await _controller.Edit(50, roomType) as ViewResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Model, Is.SameAs(roomType));
        }

        [Test]
        public async Task Details_ValidId_ReturnsViewWithRoomType()
        {
            var roomType = new RoomType { Id = 60, Name = "Executive", Capacity = 2, PricePerNight = 160m };
            _context.RoomTypes.Add(roomType);
            await _context.SaveChangesAsync();

            var result = await _controller.Details(60) as ViewResult;

            Assert.That(result, Is.Not.Null);

            var model = result!.Model as RoomType;
            Assert.That(model, Is.Not.Null);
            Assert.That(model!.Id, Is.EqualTo(60));
            Assert.That(model.Name, Is.EqualTo("Executive"));
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
        public async Task Delete_Get_ExistingId_ReturnsViewWithRoomType()
        {
            var roomType = new RoomType { Id = 70, Name = "Penthouse", Capacity = 6, PricePerNight = 400m };
            _context.RoomTypes.Add(roomType);
            await _context.SaveChangesAsync();

            var result = await _controller.Delete(70) as ViewResult;

            Assert.That(result, Is.Not.Null);

            var model = result!.Model as RoomType;
            Assert.That(model, Is.Not.Null);
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
            var roomType = new RoomType { Id = 80, Name = "Junior Suite", Capacity = 3, PricePerNight = 200m };
            _context.RoomTypes.Add(roomType);
            await _context.SaveChangesAsync();

            var result = await _controller.DeleteConfirmed(80) as RedirectToActionResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.ActionName, Is.EqualTo(nameof(RoomTypesController.Index)));

            var deleted = await _context.RoomTypes.FindAsync(80);
            Assert.That(deleted, Is.Null);
        }

        [Test]
        public async Task DeleteConfirmed_Post_NonExistingId_RedirectsWithoutDelete()
        {
            var result = await _controller.DeleteConfirmed(999) as RedirectToActionResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.ActionName, Is.EqualTo(nameof(RoomTypesController.Index)));
        }
    }
}