using Hotel_Manager.Data;
using Hotel_Manager.Models;
using Hotel_Manager.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
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

        private ApplicationUser CreateMinimalUser(string id, string email)
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
                Assert.That(result.Success, Is.True);
                Assert.That(result.NewGuestEmail, Is.EqualTo("newguest@example.com"));
                Assert.That(result.NewGuestPassword, Is.Not.Null.Or.Empty);
                Assert.That(_context.Reservations.Count(), Is.EqualTo(1));
                Assert.That(_context.ReservationRooms.Any(), Is.True);
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
            var status = _logic.DetermineStatus(
                DateTime.Today.AddDays(-2),
                DateTime.Today.AddDays(5)
            );
            Assert.That(status, Is.EqualTo("In Progress"));
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
            var user = CreateMinimalUser("u-upd", "upd@test.com");
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
                Assert.That(result.Success, Is.True);
                Assert.That(updated, Is.Not.Null);
                Assert.That(updated!.TotalPrice, Is.EqualTo(400m));
            });
        }

        [Test]
        public async Task CreateAsync_EmptyGuestEmail_ReturnsError()
        {
            var result = await _logic.CreateAsync(
                new Reservation(),
                "",
                "John",
                "Doe",
                30,
                1,
                null,
                null
            );

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Errors.Any(e => e.Contains("Guest email is required")), Is.True);
            });
        }

        [Test]
        public async Task CreateAsync_UserCreationFails_ReturnsErrors()
        {
            _userManagerMock.Setup(m => m.FindByEmailAsync("fail@example.com"))
                .ReturnsAsync((ApplicationUser?)null);
            _userManagerMock.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too weak" }));

            var result = await _logic.CreateAsync(
                new Reservation(),
                "fail@example.com",
                "John",
                "Doe",
                30,
                1,
                null,
                null
            );

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Errors.Any(e => e.Contains("Password too weak")), Is.True);
            });
        }

        [Test]
        public async Task CreateAsync_ValidServices_AssignsThemCorrectly()
        {
            var user = CreateMinimalUser("u-valid", "valid@example.com");
            var roomType = new RoomType { Id = 1, Name = "Standard", Capacity = 2, PricePerNight = 100m };
            var room = new Room { Id = 2002, RoomNumber = "202", RoomTypeId = 1, IsAvailable = true };
            var service1 = new HotelService { Id = 10, Name = "Breakfast", Price = 15m };
            var service2 = new HotelService { Id = 20, Name = "Parking", Price = 10m };

            _context.Users.Add(user);
            _context.RoomTypes.Add(roomType);
            _context.Rooms.Add(room);
            _context.HotelServices.AddRange(service1, service2);
            await _context.SaveChangesAsync();

            _userManagerMock.Setup(m => m.FindByEmailAsync("valid@example.com"))
                .ReturnsAsync(user);

            var reservationInput = new Reservation
            {
                CheckInDate = DateTime.Today.AddDays(1),
                CheckOutDate = DateTime.Today.AddDays(4)
            };
            var serviceIds = new List<int> { 10, 20 };

            var result = await _logic.CreateAsync(
                reservationInput,
                "valid@example.com",
                "John",
                "Doe",
                30,
                1,
                serviceIds,
                null
            );

            var savedReservation = await _context.Reservations
                .Include(r => r.ReservationServices)
                .ThenInclude(rs => rs.HotelService)
                .FirstOrDefaultAsync(r => r.Id == result.ReservationId);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(savedReservation!.ReservationServices.Count, Is.EqualTo(2));
                Assert.That(savedReservation.ReservationServices.Any(rs => rs.ServiceId == 10), Is.True);
                Assert.That(savedReservation.ReservationServices.Any(rs => rs.ServiceId == 20), Is.True);
                Assert.That(savedReservation.ReservationServices.All(rs => rs.HotelService != null), Is.True);
            });
        }

        [Test]
        public async Task CreateAsync_InvalidServiceIds_ReturnsError()
        {
            var user = CreateMinimalUser("u-serv", "serv@example.com");
            var roomType = new RoomType { Id = 1, Name = "Standard", Capacity = 2, PricePerNight = 100m };
            var room = new Room { Id = 2003, RoomNumber = "203", RoomTypeId = 1, IsAvailable = true };

            _context.Users.Add(user);
            _context.RoomTypes.Add(roomType);
            _context.Rooms.Add(room);
            await _context.SaveChangesAsync();

            _userManagerMock.Setup(m => m.FindByEmailAsync("serv@example.com"))
                .ReturnsAsync(user);

            var reservationInput = new Reservation
            {
                CheckInDate = DateTime.Today.AddDays(1),
                CheckOutDate = DateTime.Today.AddDays(4)
            };
            var invalidServiceIds = new List<int> { 999 };

            var result = await _logic.CreateAsync(
                reservationInput,
                "serv@example.com",
                "John",
                "Doe",
                30,
                1,
                invalidServiceIds,
                null
            );

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Errors.Any(e => e.Contains("One or more selected services no longer exist")), Is.True);
                Assert.That(_context.ReservationServices.Count(), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task UpdateReservationAsync_ChangeRoomType_UpdatesAvailabilityAndAddsNewRoomLink()
        {
            var user = CreateMinimalUser("u-change", "change@test.com");
            var oldRoomType = new RoomType { Id = 1, Name = "Old", PricePerNight = 100m };
            var newRoomType = new RoomType { Id = 2, Name = "New", PricePerNight = 120m };
            var oldRoom = new Room { Id = 3001, RoomNumber = "301", RoomTypeId = 1, IsAvailable = false };
            var newRoom = new Room { Id = 3002, RoomNumber = "302", RoomTypeId = 2, IsAvailable = true };
            var reservation = new Reservation
            {
                Id = 500,
                UserId = user.Id,
                User = user,
                CheckInDate = DateTime.Today,
                CheckOutDate = DateTime.Today.AddDays(3),
                TotalPrice = 300m
            };
            var oldRoomLink = new ReservationRoom { ReservationId = 500, RoomId = 3001 };

            _context.Users.Add(user);
            _context.RoomTypes.AddRange(oldRoomType, newRoomType);
            _context.Rooms.AddRange(oldRoom, newRoom);
            _context.Reservations.Add(reservation);
            _context.ReservationRooms.Add(oldRoomLink);
            await _context.SaveChangesAsync();

            var result = await _logic.UpdateReservationAsync(
                500,
                DateTime.Today,
                DateTime.Today.AddDays(3),
                2,
                null
            );

            var updatedReservation = await _context.Reservations
                .Include(r => r.ReservationRooms)
                .FirstOrDefaultAsync(r => r.Id == 500);
            var updatedNewRoom = await _context.Rooms.FindAsync(3002);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(updatedReservation!.ReservationRooms.Count, Is.EqualTo(1));
                Assert.That(updatedReservation.ReservationRooms.First().RoomId, Is.EqualTo(3002));
                Assert.That(updatedNewRoom!.IsAvailable, Is.False);
            });
        }

        [Test]
        public async Task UpdateReservationAsync_ServiceIdsInvalid_ReturnsError()
        {
            var user = CreateMinimalUser("u-servupd", "servupd@test.com");
            var roomType = new RoomType { Id = 1, Name = "Standard", PricePerNight = 100m };
            var room = new Room { Id = 3001, RoomNumber = "301", RoomTypeId = 1, IsAvailable = true };
            var reservation = new Reservation
            {
                Id = 400,
                UserId = user.Id,
                User = user,
                CheckInDate = DateTime.Today,
                CheckOutDate = DateTime.Today.AddDays(3),
                TotalPrice = 300m
            };
            var reservationRoom = new ReservationRoom
            {
                ReservationId = 400,
                RoomId = 3001
            };
            var existingService = new HotelService { Id = 10, Name = "Breakfast", Price = 15m };

            _context.HotelServices.Add(existingService);
            _context.Users.Add(user);
            _context.RoomTypes.Add(roomType);
            _context.Rooms.Add(room);
            _context.Reservations.Add(reservation);
            _context.ReservationRooms.Add(reservationRoom);
            await _context.SaveChangesAsync();

            var invalidServiceIds = new List<int> { 10, 999 };

            var result = await _logic.UpdateReservationAsync(
                400,
                DateTime.Today,
                DateTime.Today.AddDays(3),
                1,
                invalidServiceIds
            );

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Errors.Any(e => e.Contains("One or more selected services no longer exist")), Is.True);
            });
        }

        [Test]
        public async Task UpdateReservationAsync_NoAvailableRoom_WhenChangingType_ReturnsError()
        {
            var user = CreateMinimalUser("u-err", "err@test.com");
            var roomType1 = new RoomType { Id = 1, Name = "Old", PricePerNight = 100m };
            var roomType2 = new RoomType { Id = 2, Name = "New", PricePerNight = 120m };
            var room = new Room { Id = 2001, RoomNumber = "201", RoomTypeId = 1, IsAvailable = false };
            var reservation = new Reservation
            {
                Id = 200,
                UserId = user.Id,
                User = user
            };

            _context.Users.Add(user);
            _context.RoomTypes.AddRange(roomType1, roomType2);
            _context.Rooms.Add(room);
            _context.Reservations.Add(reservation);
            _context.ReservationRooms.Add(new ReservationRoom { ReservationId = 200, RoomId = 2001 });
            await _context.SaveChangesAsync();

            var result = await _logic.UpdateReservationAsync(
                200,
                DateTime.Today,
                DateTime.Today.AddDays(3),
                2,
                null
            );

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Errors.Any(e => e.Contains("Няма свободна стая")), Is.True);
            });
        }

        [Test]
        public async Task UpdateReservationAsync_NoAssignedRoom_ReturnsError()
        {
            var user = CreateMinimalUser("u-no-room", "noroom@test.com");
            var reservation = new Reservation { Id = 300, UserId = user.Id, User = user };

            _context.Users.Add(user);
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            var result = await _logic.UpdateReservationAsync(
                300,
                DateTime.Today,
                DateTime.Today.AddDays(3),
                1,
                null
            );

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Errors.Any(e => e.Contains("Reservation has no assigned room")), Is.True);
            });
        }

        [Test]
        public async Task UpdateReservationAsync_NonExistentReservation_ReturnsError()
        {
            var result = await _logic.UpdateReservationAsync(
                999999,
                DateTime.Today,
                DateTime.Today.AddDays(3),
                1,
                null
            );

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Errors.Any(e => e.Contains("Reservation not found")), Is.True);
            });
        }

        [Test]
        public async Task UpdateReservationAsync_NoAvailableRoomForNewType_AddsCorrectError()
        {
            var user = CreateMinimalUser("u-noavail", "noavail@test.com");

            var roomType1 = new RoomType { Id = 1, Name = "Standard", PricePerNight = 100m };
            var roomType2 = new RoomType { Id = 2, Name = "Deluxe", PricePerNight = 150m };


            var room = new Room { Id = 4001, RoomNumber = "401", RoomTypeId = 1, IsAvailable = false };

            var reservation = new Reservation
            {
                Id = 600,
                UserId = user.Id,
                User = user,
                CheckInDate = DateTime.Today.AddDays(1),
                CheckOutDate = DateTime.Today.AddDays(4)
            };

            reservation.ReservationRooms.Add(new ReservationRoom
            {
                ReservationId = 600,
                RoomId = 4001
            });

            _context.Users.Add(user);
            _context.RoomTypes.AddRange(roomType1, roomType2);
            _context.Rooms.Add(room);
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            var result = await _logic.UpdateReservationAsync(
                600,
                DateTime.Today.AddDays(1),
                DateTime.Today.AddDays(4),
                2,   
                null
            );

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Errors.Any(e => e.Contains("Няма свободна стая от избрания тип.")), Is.True);
            });
        }

        [Test]
        public async Task CreateAsync_ExistingUser_NewLicensePlate_AddsCar()
        {
            var user = CreateMinimalUser("u-car-new", "carnew@test.com");
            var roomType = new RoomType { Id = 1, Name = "Standard", Capacity = 2, PricePerNight = 90m };
            var room = new Room { Id = 5001, RoomNumber = "501", RoomTypeId = 1, IsAvailable = true };

            _context.Users.Add(user);
            _context.RoomTypes.Add(roomType);
            _context.Rooms.Add(room);
            await _context.SaveChangesAsync();

            _userManagerMock.Setup(m => m.FindByEmailAsync("carnew@test.com"))
                .ReturnsAsync(user);

            var reservationInput = new Reservation
            {
                CheckInDate = DateTime.Today.AddDays(2),
                CheckOutDate = DateTime.Today.AddDays(5)
            };

            var result = await _logic.CreateAsync(
                reservationInput,
                "carnew@test.com",
                null, null, null,
                1,
                null,
                "ZZ-99-XX"
            );

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(_context.UserCars.Any(c =>
                    c.UserId == user.Id &&
                    c.LicensePlate == "ZZ-99-XX"), Is.True);
            });
        }

        [Test]
        public async Task CreateAsync_ExistingUser_DuplicateLicensePlate_DoesNotAddAgain()
        {
            var user = CreateMinimalUser("u-car-dup", "cardup@test.com");

            user.Cars.Add(new UserCar
            {
                LicensePlate = "AB-12-CD",
                UserId = user.Id
            });

            var roomType = new RoomType { Id = 1, Name = "Standard", Capacity = 2, PricePerNight = 90m };
            var room = new Room { Id = 5002, RoomNumber = "502", RoomTypeId = 1, IsAvailable = true };

            _context.Users.Add(user);
            _context.RoomTypes.Add(roomType);
            _context.Rooms.Add(room);
            await _context.SaveChangesAsync();

            _userManagerMock.Setup(m => m.FindByEmailAsync("cardup@test.com"))
                .ReturnsAsync(user);

            var reservationInput = new Reservation
            {
                CheckInDate = DateTime.Today.AddDays(1),
                CheckOutDate = DateTime.Today.AddDays(3)
            };

            var result = await _logic.CreateAsync(
                reservationInput,
                "cardup@test.com",
                null, null, null,
                1,
                null,
                "AB-12-CD" 
            );

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(_context.UserCars.Count(c =>
                    c.UserId == user.Id &&
                    c.LicensePlate == "AB-12-CD"), Is.EqualTo(1));
            });
        }
        [Test]
        public async Task UpdateReservationAsync_ChangesServices_CreatesReservationServiceEntities()
        {
            var user = CreateMinimalUser("u-svc", "svc@test.com");

            var roomType = new RoomType { Id = 1, Name = "Standard", PricePerNight = 100m };
            var room = new Room { Id = 7001, RoomNumber = "701", RoomTypeId = 1, IsAvailable = false };

            var serviceA = new HotelService { Id = 100, Name = "Breakfast", Price = 20m };
            var serviceB = new HotelService { Id = 200, Name = "Spa", Price = 50m };

            var reservation = new Reservation
            {
                Id = 800,
                UserId = user.Id,
                User = user,
                CheckInDate = DateTime.Today.AddDays(1),
                CheckOutDate = DateTime.Today.AddDays(3),
                TotalPrice = 300m
            };

            reservation.ReservationRooms.Add(new ReservationRoom { ReservationId = 800, RoomId = 7001 });

            _context.Users.Add(user);
            _context.RoomTypes.Add(roomType);
            _context.Rooms.Add(room);
            _context.HotelServices.AddRange(serviceA, serviceB);
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            var newServiceIds = new List<int> { 100, 200 };

            var result = await _logic.UpdateReservationAsync(
                800,
                DateTime.Today.AddDays(1),
                DateTime.Today.AddDays(3),
                1,
                newServiceIds
            );

            var updated = await _context.Reservations
                .Include(r => r.ReservationServices)
                .ThenInclude(rs => rs.HotelService)
                .FirstOrDefaultAsync(r => r.Id == 800);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(updated, Is.Not.Null);
                Assert.That(updated!.ReservationServices.Count, Is.EqualTo(2));
                Assert.That(updated.ReservationServices.Any(rs => rs.ServiceId == 100 && rs.HotelService != null), Is.True);
                Assert.That(updated.ReservationServices.Any(rs => rs.ServiceId == 200 && rs.HotelService != null), Is.True);
            });
        }

        [Test]
        public async Task UpdateReservationAsync_NoRoomAvailableForNewType_ReturnsErrorAndStops()
        {
            var user = CreateMinimalUser("u-noroom2", "noroom2@test.com");

            var roomType1 = new RoomType { Id = 1, Name = "Standard", PricePerNight = 100m };
            var roomType2 = new RoomType { Id = 2, Name = "Deluxe", PricePerNight = 180m };

            var room = new Room { Id = 9001, RoomNumber = "901", RoomTypeId = 1, IsAvailable = false };

            var reservation = new Reservation
            {
                Id = 950,
                UserId = user.Id,
                User = user,
                CheckInDate = DateTime.Today.AddDays(2),
                CheckOutDate = DateTime.Today.AddDays(5)
            };

            reservation.ReservationRooms.Add(new ReservationRoom { ReservationId = 950, RoomId = 9001 });

            _context.Users.Add(user);
            _context.RoomTypes.AddRange(roomType1, roomType2);
            _context.Rooms.Add(room);
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            var result = await _logic.UpdateReservationAsync(
                950,
                DateTime.Today.AddDays(2),
                DateTime.Today.AddDays(5),
                2,     
                null
            );

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Errors.Any(e =>
                    e.Contains("Няма свободна стая от избрания тип") ||
                    e.Contains("No available room of the selected type")), Is.True);
            });
        }
    }
}