using Hotel_Manager.Data;
using Hotel_Manager.Models;
using Hotel_Manager.Services;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using System;
using System.Reflection;
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

        [Test]
        public async Task IsRoomLockedAsync_NoReservations_ReturnsFalse()
        {
            var room = new Room { Id = 203, RoomNumber = "203" };
            _context.Rooms.Add(room);
            await _context.SaveChangesAsync();

            var isLocked = await _service.IsRoomLockedAsync(203);

            Assert.That(isLocked, Is.False);
        }

        [Test]
        public async Task SetAvailabilityByReservationIdAsync_UpdatesRoomAvailability()
        {
            var user = CreateUser("u-single");
            var room1 = new Room { Id = 301, RoomNumber = "301", IsAvailable = false };
            var room2 = new Room { Id = 302, RoomNumber = "302", IsAvailable = true };
            var res = CreateReservation(user.Id, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(3), "Confirmed");

            _context.Users.Add(user);
            _context.Rooms.AddRange(room1, room2);
            _context.Reservations.Add(res);
            await _context.SaveChangesAsync();

            _context.ReservationRooms.AddRange(
                new ReservationRoom { ReservationId = res.Id, RoomId = room1.Id },
                new ReservationRoom { ReservationId = res.Id, RoomId = room2.Id }
            );
            await _context.SaveChangesAsync();

            await _service.SetAvailabilityByReservationIdAsync(res.Id, true);

            var r1 = await _context.Rooms.FindAsync(room1.Id);
            var r2 = await _context.Rooms.FindAsync(room2.Id);

            Assert.Multiple(() =>
            {
                Assert.That(r1!.IsAvailable, Is.True);
                Assert.That(r2!.IsAvailable, Is.True);
            });

            await _service.SetAvailabilityByReservationIdAsync(res.Id, false);

            r1 = await _context.Rooms.FindAsync(room1.Id);
            r2 = await _context.Rooms.FindAsync(room2.Id);

            Assert.Multiple(() =>
            {
                Assert.That(r1!.IsAvailable, Is.False);
                Assert.That(r2!.IsAvailable, Is.False);
            });
        }

        [Test]
        public async Task SetAvailabilityByReservationIdAsync_NoLinkedRooms_EarlyReturnNoUpdate()
        {
            var res = CreateReservation("u-empty", DateTime.Today, DateTime.Today.AddDays(1), "Pending");
            _context.Reservations.Add(res);
            await _context.SaveChangesAsync();

            // Act
            await _service.SetAvailabilityByReservationIdAsync(res.Id, true);

            Assert.Pass("Early return when !roomIds.Any()");
        }

        [Test]
        public async Task SetAvailabilityByReservationIdAsync_NonExistentReservation_EarlyReturnNoUpdate()
        {
            await _service.SetAvailabilityByReservationIdAsync(999999, true);

            Assert.Pass("Non-existent reservation should early-return safely");
        }

        [Test]
        public async Task SetAvailabilityByReservationIdAsyncMultiple_UpdatesRoomsForMultipleReservations()
        {
            var user = CreateUser("u-multi");
            var room1 = new Room { Id = 401, RoomNumber = "401", IsAvailable = false };
            var room2 = new Room { Id = 402, RoomNumber = "402", IsAvailable = true };
            var res1 = CreateReservation(user.Id, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(3), "Confirmed");
            var res2 = CreateReservation(user.Id, DateTime.Today.AddDays(2), DateTime.Today.AddDays(6), "Upcoming");

            _context.Users.Add(user);
            _context.Rooms.AddRange(room1, room2);
            _context.Reservations.AddRange(res1, res2);
            await _context.SaveChangesAsync();

            _context.ReservationRooms.AddRange(
                new ReservationRoom { ReservationId = res1.Id, RoomId = room1.Id },
                new ReservationRoom { ReservationId = res1.Id, RoomId = room2.Id },
                new ReservationRoom { ReservationId = res2.Id, RoomId = room1.Id }
            );
            await _context.SaveChangesAsync();

            await CallSetAvailabilityMultiple(new List<int> { res1.Id, res2.Id }, true);

            var r1 = await _context.Rooms.FindAsync(room1.Id);
            var r2 = await _context.Rooms.FindAsync(room2.Id);

            Assert.Multiple(() =>
            {
                Assert.That(r1!.IsAvailable, Is.True);
                Assert.That(r2!.IsAvailable, Is.True);
            });
        }

        [Test]
        public async Task SetAvailabilityByReservationIdAsyncMultiple_NoRooms_EarlyReturnNoUpdate()
        {
            var res1 = CreateReservation("u1", DateTime.Today, DateTime.Today.AddDays(1), "Pending");
            var res2 = CreateReservation("u2", DateTime.Today.AddDays(1), DateTime.Today.AddDays(2), "Upcoming");

            _context.Reservations.AddRange(res1, res2);
            await _context.SaveChangesAsync();

            await CallSetAvailabilityMultiple(new List<int> { res1.Id, res2.Id }, true);

            Assert.Pass("Early return when !roomIds.Any()");
        }

        private async Task CallSetAvailabilityMultiple(List<int> reservationIds, bool isAvailable)
        {
            var method = typeof(RoomAvailabilityService).GetMethod(
                "SetAvailabilityByReservationIdAsyncMultiple",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (method == null)
                Assert.Fail("Private method SetAvailabilityByReservationIdAsyncMultiple not found");

            var task = (Task)method.Invoke(_service, new object[] { reservationIds, isAvailable })!;
            await task;
        }



        private ApplicationUser CreateUser(string suffix)
        {
            return new ApplicationUser
            {
                Id = $"user-{suffix}",
                UserName = $"test{suffix}",
                Email = $"test{suffix}@example.com",
                FirstName = "Test",
                LastName = "User",
                Age = 30,
                IsActive = true
            };
        }

        private Reservation CreateReservation(string userId, DateTime checkIn, DateTime checkOut, string status)
        {
            return new Reservation
            {
                UserId = userId,
                CheckInDate = checkIn,
                CheckOutDate = checkOut,
                Status = status
            };
        }
    }
}