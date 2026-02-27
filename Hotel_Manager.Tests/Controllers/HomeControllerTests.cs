using Hotel_Manager.Controllers;
using Hotel_Manager.Data;
using Hotel_Manager.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace HotelManager.Tests.Controllers
{
    [TestFixture]
    public class HomeControllerTests
    {
        private ApplicationDbContext _context = null!;
        private Mock<ILogger<HomeController>> _loggerMock = null!;
        private HomeController _controller = null!;

        [SetUp]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"HomeControllerTest_{Guid.NewGuid()}")
                .Options;

            _context = new ApplicationDbContext(options);
            _loggerMock = new Mock<ILogger<HomeController>>();

            _controller = new HomeController(_loggerMock.Object, _context);
        }

        [TearDown]
        public void TearDown()
        {
            _controller?.Dispose();
            _context?.Database.EnsureDeleted();
            _context?.Dispose();
        }

        private void SetupUser(bool isAuthenticated, params string[] roles)
        {
            var claims = new List<Claim>();

            if (isAuthenticated)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, "test-user-id"));
                claims.Add(new Claim(ClaimTypes.Name, "testuser"));

                foreach (var role in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            }

            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }

        private ApplicationUser CreateUser(string id, string email)
        {
            return new ApplicationUser
            {
                Id = id,
                UserName = email,
                Email = email,
                FirstName = "Test",
                LastName = "User",
                Age = 30,
                IsActive = true
            };
        }

        [Test]
        public async Task Index_NotAuthenticated_NoViewBagPropertiesSet()
        {
            SetupUser(isAuthenticated: false);

            var result = await _controller.Index() as ViewResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.ViewName, Is.Null);

            Assert.That(_controller.ViewBag.UpcomingReservations, Is.Null);
            Assert.That(_controller.ViewBag.InProgressReservations, Is.Null);
            Assert.That(_controller.ViewBag.FreeRooms, Is.Null);
        }

        [Test]
        public async Task Index_AuthenticatedGuest_NoViewBagPropertiesSet()
        {
            SetupUser(isAuthenticated: true, "Guest");

            var result = await _controller.Index() as ViewResult;

            Assert.That(result, Is.Not.Null);

            Assert.That(_controller.ViewBag.UpcomingReservations, Is.Null);
            Assert.That(_controller.ViewBag.InProgressReservations, Is.Null);
            Assert.That(_controller.ViewBag.FreeRooms, Is.Null);
        }

        [Test]
        public async Task Index_Receptionist_SetsViewBagWithCorrectCounts()
        {
            SetupUser(isAuthenticated: true, "Receptionist");

            var user = CreateUser("user1", "recep@test.com");
            _context.Users.Add(user);

            _context.Reservations.AddRange(
                new Reservation { Status = "Upcoming", UserId = user.Id },
                new Reservation { Status = "Upcoming", UserId = user.Id },
                new Reservation { Status = "In progress", UserId = user.Id },
                new Reservation { Status = "Completed", UserId = user.Id },
                new Reservation { Status = "Cancelled", UserId = user.Id }
            );

            _context.Rooms.AddRange(
                new Room { RoomNumber = "101", IsAvailable = true },
                new Room { RoomNumber = "102", IsAvailable = true },
                new Room { RoomNumber = "103", IsAvailable = true },
                new Room { RoomNumber = "104", IsAvailable = false }
            );

            await _context.SaveChangesAsync();

            var result = await _controller.Index() as ViewResult;

            Assert.That(result, Is.Not.Null);

            Assert.That(_controller.ViewBag.UpcomingReservations, Is.EqualTo(2));
            Assert.That(_controller.ViewBag.InProgressReservations, Is.EqualTo(1));
            Assert.That(_controller.ViewBag.FreeRooms, Is.EqualTo(3));
        }

        [Test]
        public async Task Index_Admin_SetsViewBagWithCorrectCounts()
        {
            SetupUser(isAuthenticated: true, "Admin");

            var user = CreateUser("user2", "admin@test.com");
            _context.Users.Add(user);

            _context.Reservations.AddRange(
                new Reservation { Status = "Upcoming", UserId = user.Id },
                new Reservation { Status = "In progress", UserId = user.Id },
                new Reservation { Status = "In progress", UserId = user.Id }
            );

            _context.Rooms.AddRange(
                new Room { RoomNumber = "201", IsAvailable = true },
                new Room { RoomNumber = "202", IsAvailable = false }
            );

            await _context.SaveChangesAsync();

            var result = await _controller.Index() as ViewResult;

            Assert.That(result, Is.Not.Null);

            Assert.That(_controller.ViewBag.UpcomingReservations, Is.EqualTo(1));
            Assert.That(_controller.ViewBag.InProgressReservations, Is.EqualTo(2));
            Assert.That(_controller.ViewBag.FreeRooms, Is.EqualTo(1));
        }

        [Test]
        public async Task Index_ReceptionistAndAdminRoles_SetsViewBag()
        {
            SetupUser(isAuthenticated: true, "Receptionist", "Admin");

            var user = CreateUser("user3", "multi@test.com");
            _context.Users.Add(user);

            _context.Reservations.Add(new Reservation { Status = "Upcoming", UserId = user.Id });
            _context.Rooms.Add(new Room { RoomNumber = "301", IsAvailable = true });

            await _context.SaveChangesAsync();

            var result = await _controller.Index() as ViewResult;

            Assert.That(result, Is.Not.Null);

            Assert.That(_controller.ViewBag.UpcomingReservations, Is.EqualTo(1));
            Assert.That(_controller.ViewBag.FreeRooms, Is.EqualTo(1));
        }

        [Test]
        public void Privacy_ReturnsDefaultPrivacyView()
        {
            var result = _controller.Privacy() as ViewResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.ViewName, Is.Null);
        }

        [Test]
        public void Error_WithTraceIdentifier_SetsRequestIdInModel()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.TraceIdentifier = "trace-abc-12345";

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var result = _controller.Error() as ViewResult;

            Assert.That(result, Is.Not.Null);

            var model = result!.Model as ErrorViewModel;
            Assert.That(model, Is.Not.Null);
            Assert.That(model!.RequestId, Is.EqualTo("trace-abc-12345"));
            Assert.That(model.ShowRequestId, Is.True);
        }

        [Test]
        public async Task Pricing_SetsCorrectViewDataAndOrderedCollections()
        {
            var roomTypes = new List<RoomType>
            {
                new RoomType { Id = 1, Name = "Basic",   PricePerNight = 60m },
                new RoomType { Id = 2, Name = "Deluxe",  PricePerNight = 140m },
                new RoomType { Id = 3, Name = "Suite",   PricePerNight = 220m },
                new RoomType { Id = 4, Name = "Economy", PricePerNight = 45m }
            };

            var services = new List<HotelService>
            {
                new HotelService { Id = 1, Name = "Breakfast", Price = 15m },
                new HotelService { Id = 2, Name = "Parking",   Price = 10m },
                new HotelService { Id = 3, Name = "Spa",       Price = 50m },
                new HotelService { Id = 4, Name = "WiFi",      Price = 5m }
            };

            _context.RoomTypes.AddRange(roomTypes);
            _context.HotelServices.AddRange(services);
            await _context.SaveChangesAsync();

            var result = await _controller.Pricing() as ViewResult;

            Assert.That(result, Is.Not.Null);

            Assert.That(_controller.ViewData["Title"], Is.EqualTo("Цени"));
            Assert.That(_controller.ViewData["HideNavbar"], Is.True);

            var returnedRooms = _controller.ViewBag.RoomTypes as List<RoomType>;
            Assert.That(returnedRooms, Is.Not.Null);
            Assert.That(returnedRooms!.Count, Is.EqualTo(4));
            Assert.That(returnedRooms[0].PricePerNight, Is.EqualTo(45m));
            Assert.That(returnedRooms[3].PricePerNight, Is.EqualTo(220m));

            var returnedServices = _controller.ViewBag.Services as List<HotelService>;
            Assert.That(returnedServices, Is.Not.Null);
            Assert.That(returnedServices!.Count, Is.EqualTo(4));
            Assert.That(returnedServices[0].Price, Is.EqualTo(5m));
            Assert.That(returnedServices[3].Price, Is.EqualTo(50m));
        }

        [Test]
        public async Task Pricing_EmptyDatabase_ReturnsEmptyCollections()
        {
            var result = await _controller.Pricing() as ViewResult;

            Assert.That(result, Is.Not.Null);

            Assert.That(_controller.ViewData["Title"], Is.EqualTo("Цени"));
            Assert.That(_controller.ViewData["HideNavbar"], Is.True);

            var rooms = _controller.ViewBag.RoomTypes as List<RoomType>;
            Assert.That(rooms, Is.Empty);

            var services = _controller.ViewBag.Services as List<HotelService>;
            Assert.That(services, Is.Empty);
        }
    }
}