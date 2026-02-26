using Hotel_Manager.Data;
using Hotel_Manager.Models;
using Hotel_Manager.Services;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace HotelManager.Tests.Services
{
    [TestFixture]
    public class RoomAvailabilityServiceTests
    {
        private ApplicationDbContext _context;
        private RoomAvailabilityService _service;

        [SetUp]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_" + Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _service = new RoomAvailabilityService(_context);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Test]
        public async Task IsRoomLockedAsync_ActiveFutureReservation_ReturnsTrue()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user1",
                UserName = "testuser1",
                Email = "test1@example.com",
                FirstName = "Test",      
                LastName = "User1"       
            };

            var room = new Room { Id = 101, RoomNumber = "101", IsAvailable = false };
            var reservation = new Reservation
            {
                Id = 1,
                UserId = user.Id,
                User = user,
                CheckInDate = DateTime.Today.AddDays(-1),
                CheckOutDate = DateTime.Today.AddDays(10),
                Status = "Upcoming"
            };
            var rr = new ReservationRoom { RoomId = 101, ReservationId = 1 };

            _context.Users.Add(user);
            _context.Rooms.Add(room);
            _context.Reservations.Add(reservation);
            _context.ReservationRooms.Add(rr);
            await _context.SaveChangesAsync();

            
            var locked = await _service.IsRoomLockedAsync(101);

            Assert.That(locked, Is.True);
        }

        [Test]
        public async Task IsRoomLockedAsync_CompletedReservation_ReturnsFalse()
        {
            var user = new ApplicationUser
            {
                Id = "user2",
                UserName = "testuser2",
                Email = "test2@example.com",
                FirstName = "Test",
                LastName = "User2"
            };

            var room = new Room { Id = 102, RoomNumber = "102" };
            var reservation = new Reservation
            {
                Id = 2,
                UserId = user.Id,
                User = user,
                CheckInDate = DateTime.Today.AddDays(-10),
                CheckOutDate = DateTime.Today.AddDays(-5),
                Status = "Completed"
            };
            var rr = new ReservationRoom { RoomId = 102, ReservationId = 2 };

            _context.Users.Add(user);
            _context.Rooms.Add(room);
            _context.Reservations.Add(reservation);
            _context.ReservationRooms.Add(rr);
            await _context.SaveChangesAsync();

            var locked = await _service.IsRoomLockedAsync(102);

            Assert.That(locked, Is.False);
        }

        [Test]
        public async Task AutoCompleteExpiredReservationsAsync_CompletesOldReservations()
        {
            var user = new ApplicationUser
            {
                Id = "user3",
                UserName = "testuser3",
                Email = "test3@example.com",
                FirstName = "Test",
                LastName = "User3"
            };

            var reservation = new Reservation
            {
                Id = 99,
                UserId = user.Id,
                User = user,
                CheckInDate = DateTime.Today.AddDays(-10),
                CheckOutDate = DateTime.Today.AddDays(-2),
                Status = "Upcoming"
            };

            _context.Users.Add(user);
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            await _service.AutoCompleteExpiredReservationsAsync();

            var updated = await _context.Reservations.FindAsync(99);
            Assert.That(updated?.Status, Is.EqualTo("Completed"));
        }
    }
}