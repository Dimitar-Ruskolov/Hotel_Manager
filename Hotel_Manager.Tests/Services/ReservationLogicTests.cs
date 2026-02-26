using Hotel_Manager.Data;
using Hotel_Manager.Models;
using Hotel_Manager.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HotelManager.Tests.Services
{
    [TestFixture]
    public class ReservationLogicTests
    {
        private ApplicationDbContext _context = null!;
        private Mock<UserManager<ApplicationUser>> _userManagerMock = null!;
        private ReservationLogic _logic = null!;

        [SetUp]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"LogicTest_{Guid.NewGuid()}")
                .Options;

            _context = new ApplicationDbContext(options);

            var store = new Mock<IUserStore<ApplicationUser>>();
            _userManagerMock = new Mock<UserManager<ApplicationUser>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!
            );

            _logic = new ReservationLogic(
                _context,
                _userManagerMock.Object,
                new ReservationTotalPriceService(),
                new RoomAvailabilityService(_context)
            );
        }

        [TearDown]
        public void TearDown()
        {
            _context?.Database.EnsureDeleted();
            _context?.Dispose();
        }

        [Test]
        public async Task CreateAsync_NewGuest_CreatesUserAndReservation()
        {
        
            var roomType = new RoomType
            {
                Id = 1,
                Name = "Standard",
                Capacity = 2,
                PricePerNight = 100m
            };

            var room = new Room
            {
                Id = 1001,
                RoomNumber = "101",
                RoomTypeId = 1,
                IsAvailable = true
            };

            _context.RoomTypes.Add(roomType);
            _context.Rooms.Add(room);
            await _context.SaveChangesAsync();

    
            _userManagerMock.Setup(m => m.FindByEmailAsync("newguest@example.com"))
                .ReturnsAsync((ApplicationUser?)null);

            _userManagerMock.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);

            _userManagerMock.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Guest"))
                .ReturnsAsync(IdentityResult.Success);

 
            var reservationInput = new Reservation
            {
                CheckInDate = DateTime.Today.AddDays(1),
                CheckOutDate = DateTime.Today.AddDays(4)
            };

            var result = await _logic.CreateAsync(
                reservationInput,
                "newguest@example.com",
                "John",
                "Doe",
                30,
                1,   
                null,
                null
            );

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True, "Create should succeed");
                Assert.That(result.NewGuestEmail, Is.EqualTo("newguest@example.com"));
                Assert.That(result.NewGuestPassword, Is.Not.Null.Or.Empty);
                Assert.That(_context.Reservations.Count(), Is.EqualTo(1), "Reservation not saved");
                Assert.That(_context.ReservationRooms.Any(), Is.True, "No room assigned to reservation");
            });
        }

        [Test]
        public void DetermineStatus_PastReservation_ReturnsCompleted()
        {
            var status = _logic.DetermineStatus(
                DateTime.Today.AddDays(-10),
                DateTime.Today.AddDays(-3)
            );

            Assert.That(status, Is.EqualTo("Completed"));
        }

        [Test]
        public void DetermineStatus_CurrentReservation_ReturnsInProgress()
        {
            var checkIn = DateTime.Today.AddDays(-2);
            var checkOut = DateTime.Today.AddDays(5);

            var status = _logic.DetermineStatus(checkIn, checkOut);

            Assert.Multiple(() =>
            {
                Assert.That(checkIn <= DateTime.Today, "Check-in should be <= today");
                Assert.That(checkOut >= DateTime.Today, "Check-out should be >= today");
                Assert.That(status, Is.EqualTo("In Progress"));
            });
        }

        [Test]
        public void DetermineStatus_FutureReservation_ReturnsUpcoming()
        {
            var status = _logic.DetermineStatus(
                DateTime.Today.AddDays(5),
                DateTime.Today.AddDays(10)
            );

            Assert.That(status, Is.EqualTo("Upcoming"));
        }

        [Test]
        public async Task UpdateReservationAsync_ValidUpdate_RecalculatesPriceAndSaves()
        {
   
            var user = new ApplicationUser
            {
                Id = "u-upd",
                UserName = "upd@test.com",
                Email = "upd@test.com",
                FirstName = "Update",
                LastName = "Test",
                Age = 35,
                IsActive = true
            };

            var roomType = new RoomType
            {
                Id = 1,
                Name = "Standard",
                PricePerNight = 100m
            };

            var room = new Room
            {
                Id = 1001,
                RoomNumber = "101",
                RoomTypeId = 1,
                IsAvailable = true
            };

            var reservation = new Reservation
            {
                Id = 100,
                UserId = user.Id,
                User = user,
                CheckInDate = DateTime.Today.AddDays(-1),
                CheckOutDate = DateTime.Today.AddDays(2),
                TotalPrice = 300m
            };

            var reservationRoom = new ReservationRoom
            {
                ReservationId = 100,
                RoomId = 1001
            };

            _context.Users.Add(user);
            _context.RoomTypes.Add(roomType);
            _context.Rooms.Add(room);
            _context.Reservations.Add(reservation);
            _context.ReservationRooms.Add(reservationRoom);
            await _context.SaveChangesAsync();


            var result = await _logic.UpdateReservationAsync(
                100,
                DateTime.Today.AddDays(1),
                DateTime.Today.AddDays(5),
                1,
                null
            );


            var updated = await _context.Reservations
                .Include(r => r.ReservationRooms)
                .ThenInclude(rr => rr.Room)
                .ThenInclude(r => r.RoomType)
                .FirstOrDefaultAsync(r => r.Id == 100);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True, "Update should succeed");
                Assert.That(updated, Is.Not.Null, "Reservation not found after update");
                Assert.That(updated!.TotalPrice, Is.EqualTo(400m), "Price should be recalculated for 4 nights");
            });
        }
    }
}