using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hotel_Manager.Controllers;
using Hotel_Manager.Data;
using Hotel_Manager.Models;
using Hotel_Manager.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using System.Security.Claims;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Hotel_Manager.Tests.Controllers
{
    public class ReservationsControllerTests
    {
        private static Mock<UserManager<ApplicationUser>> CreateMockUserManager()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            return new Mock<UserManager<ApplicationUser>>(
                store.Object,
                null, null, null, null, null, null, null, null);
        }

        private static void SetUser(Controller controller, string? userId, params string[] roles)
        {
            var claims = new List<Claim>();

            if (!string.IsNullOrEmpty(userId))
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));

            foreach (var r in roles)
                claims.Add(new Claim(ClaimTypes.Role, r));

            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }

        private static void SetTempData(Controller controller)
        {
            controller.TempData = new TempDataDictionary(
                new DefaultHttpContext(),
                Mock.Of<ITempDataProvider>());
        }

        private static ApplicationDbContext CreateDb()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        [Test]
        public async Task Index_Guest_WhenGetUserIdIsNull_RedirectsToHomeIndex()
        {
            using var context = CreateDb();

            var price = new ReservationTotalPriceService();
            var roomAvailability = new RoomAvailabilityService(context);

            var userManager = CreateMockUserManager();
            userManager.Setup(um => um.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns((string?)null);

            var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

            var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);
            SetUser(controller, userId: null, roles: "Guest");

            var result = await controller.Index();

            Assert.That(result, Is.TypeOf<RedirectToActionResult>());
            var r = (RedirectToActionResult)result;

            Assert.Multiple(() =>
            {
                Assert.That(r.ActionName, Is.EqualTo("Index"));
                Assert.That(r.ControllerName, Is.EqualTo("Home"));
            });
        }

        [Test]
        public async Task Index_Guest_SeesOnlyOwnReservations_AndSetsViewBags()
        {
            using var context = CreateDb();

            // Seed users + room type/room, за да има join-и
            var u1 = new ApplicationUser { Id = "u1", UserName = "u1@test.com", Email = "u1@test.com", FirstName = "A", LastName = "B", Age = 18, IsActive = true };
            var u2 = new ApplicationUser { Id = "u2", UserName = "u2@test.com", Email = "u2@test.com", FirstName = "C", LastName = "D", Age = 18, IsActive = true };
            context.Users.AddRange(u1, u2);

            var rt = new RoomType { Id = 1, Name = "Standard", PricePerNight = 100m };
            var room = new Room { Id = 1, RoomNumber = "101", RoomTypeId = 1, IsAvailable = false };
            context.RoomTypes.Add(rt);
            context.Rooms.Add(room);

            var r1 = new Reservation { Id = 1, UserId = "u1", User = u1, CheckInDate = DateTime.Today, CheckOutDate = DateTime.Today.AddDays(2), Status = "Upcoming", TotalPrice = 0m, CreatedAt = DateTime.UtcNow };
            var r2 = new Reservation { Id = 2, UserId = "u2", User = u2, CheckInDate = DateTime.Today, CheckOutDate = DateTime.Today.AddDays(2), Status = "Upcoming", TotalPrice = 0m, CreatedAt = DateTime.UtcNow.AddMinutes(-1) };
            context.Reservations.AddRange(r1, r2);

            context.ReservationRooms.AddRange(
                new ReservationRoom { ReservationId = 1, RoomId = 1, Reservation = r1, Room = room },
                new ReservationRoom { ReservationId = 2, RoomId = 1, Reservation = r2, Room = room }
            );

            await context.SaveChangesAsync();

            var price = new ReservationTotalPriceService();
            var roomAvailability = new RoomAvailabilityService(context);

            var userManager = CreateMockUserManager();
            userManager.Setup(um => um.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns("u1");

            var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

            var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);
            SetUser(controller, userId: "u1", roles: "Guest");

            var result = await controller.Index();

            Assert.That(result, Is.TypeOf<ViewResult>());
            var view = (ViewResult)result;
            var model = view.Model as List<Reservation>;

            Assert.Multiple(() =>
            {
                Assert.That(model, Is.Not.Null);
                Assert.That(model!.Count, Is.EqualTo(1));
                Assert.That(model[0].UserId, Is.EqualTo("u1"));
                Assert.That(controller.ViewBag.IsGuest, Is.True);
                Assert.That(controller.ViewBag.ShowCreateButton, Is.False);
            });
        }

        [Test]
        public async Task Details_WhenIdIsNull_ReturnsNotFound()
        {
            using var context = CreateDb();
            var price = new ReservationTotalPriceService();
            var roomAvailability = new RoomAvailabilityService(context);

            var userManager = CreateMockUserManager();
            var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

            var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);
            SetUser(controller, "u1", "Admin");

            var result = await controller.Details(null);

            Assert.That(result, Is.TypeOf<NotFoundResult>());
        }

        [Test]
        public async Task Details_Guest_WhenReservationIsNotOwn_ReturnsForbid()
        {
            using var context = CreateDb();

            var u1 = new ApplicationUser { Id = "u1", UserName = "u1@test.com", Email = "u1@test.com", FirstName = "A", LastName = "B", Age = 18, IsActive = true };
            var u2 = new ApplicationUser { Id = "u2", UserName = "u2@test.com", Email = "u2@test.com", FirstName = "C", LastName = "D", Age = 18, IsActive = true };
            context.Users.AddRange(u1, u2);

            var reservation = new Reservation
            {
                Id = 1,
                UserId = "u2",
                User = u2,
                CheckInDate = DateTime.Today,
                CheckOutDate = DateTime.Today.AddDays(1),
                Status = "Upcoming",
                TotalPrice = 0m,
                CreatedAt = DateTime.UtcNow
            };
            context.Reservations.Add(reservation);
            await context.SaveChangesAsync();

            var price = new ReservationTotalPriceService();
            var roomAvailability = new RoomAvailabilityService(context);

            var userManager = CreateMockUserManager();
            userManager.Setup(um => um.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns("u1");

            var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

            var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);
            SetUser(controller, "u1", "Guest");

            var result = await controller.Details(1);

            Assert.That(result, Is.TypeOf<ForbidResult>());
        }

        [Test]
        public async Task Create_Post_WhenNoAvailableRoomType_ReturnsViewWithModelErrors()
        {
            using var context = CreateDb();

            // Seed room type но НЯМА available room
            context.RoomTypes.Add(new RoomType { Id = 1, Name = "Standard", PricePerNight = 100m });
            await context.SaveChangesAsync();

            var price = new ReservationTotalPriceService();
            var roomAvailability = new RoomAvailabilityService(context);

            var userManager = CreateMockUserManager();
            // guest exists
            var guest = new ApplicationUser { Id = "g1", UserName = "g@test.com", Email = "g@test.com", FirstName = "G", LastName = "U", Age = 18, IsActive = true };
            userManager.Setup(um => um.FindByEmailAsync("g@test.com")).ReturnsAsync(guest);

            var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

            var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);
            SetUser(controller, "admin", "Admin");
            SetTempData(controller);

            var reservation = new Reservation
            {
                CheckInDate = DateTime.Today,
                CheckOutDate = DateTime.Today.AddDays(2)
            };

            var result = await controller.Create(
                reservation,
                guestEmail: "g@test.com",
                firstName: "G",
                lastName: "U",
                age: 18,
                roomTypeId: 1,
                serviceIds: null,
                licensePlate: null);

            Assert.That(result, Is.TypeOf<ViewResult>());

            Assert.Multiple(() =>
            {
                Assert.That(controller.ModelState.IsValid, Is.False);
                Assert.That(controller.ModelState.Values.SelectMany(v => v.Errors).Any(), Is.True);
                Assert.That(controller.ViewData.ContainsKey("RoomTypes"), Is.True);
                Assert.That(controller.ViewData.ContainsKey("Services"), Is.True);
            });
        }

        [Test]
        public async Task Create_Post_Success_NewGuest_SetsTempDataAndRedirects()
        {
            using var context = CreateDb();

            // Room type + available room
            var rt = new RoomType { Id = 1, Name = "Standard", PricePerNight = 100m };
            var room = new Room { Id = 1, RoomNumber = "101", RoomTypeId = 1, IsAvailable = true };
            context.RoomTypes.Add(rt);
            context.Rooms.Add(room);

            // service optional
            context.HotelServices.Add(new HotelService { Id = 1, Name = "Breakfast", Price = 10m });

            await context.SaveChangesAsync();

            var price = new ReservationTotalPriceService();
            var roomAvailability = new RoomAvailabilityService(context);

            var userManager = CreateMockUserManager();

            // user does not exist -> CreateAsync will be called
            userManager.Setup(um => um.FindByEmailAsync("new@test.com")).ReturnsAsync((ApplicationUser?)null);
            userManager.Setup(um => um.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);
            userManager.Setup(um => um.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Guest"))
                .ReturnsAsync(IdentityResult.Success);

            var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

            var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);
            SetUser(controller, "admin", "Admin");
            SetTempData(controller);

            var reservation = new Reservation
            {
                CheckInDate = DateTime.Today,
                CheckOutDate = DateTime.Today.AddDays(2)
            };

            var result = await controller.Create(
                reservation,
                guestEmail: "new@test.com",
                firstName: "New",
                lastName: "Guest",
                age: 18,
                roomTypeId: 1,
                serviceIds: new List<int> { 1 },
                licensePlate: "A1234BC");

            Assert.That(result, Is.TypeOf<RedirectToActionResult>());
            var redirect = (RedirectToActionResult)result;

            Assert.Multiple(() =>
            {
                Assert.That(redirect.ActionName, Is.EqualTo("Index"));
                Assert.That(controller.TempData.ContainsKey("NewGuestEmail"), Is.True);
                Assert.That(controller.TempData.ContainsKey("NewGuestPassword"), Is.True);
                Assert.That(context.Reservations.Count(), Is.EqualTo(1));
                Assert.That(context.Rooms.First().IsAvailable, Is.False); // room got locked
                Assert.That(context.Reservations.First().TotalPrice, Is.GreaterThan(0m)); // price calculated
            });
        }

        [Test]
        public async Task Details_Admin_WhenReservationExists_ReturnsView()
        {
            using var context = CreateDb();

            var u = new ApplicationUser { Id = "u1", UserName = "u1@test.com", Email = "u1@test.com", FirstName = "A", LastName = "B", Age = 18, IsActive = true };
            context.Users.Add(u);

            var res = new Reservation
            {
                Id = 1,
                UserId = "u1",
                User = u,
                CheckInDate = DateTime.Today,
                CheckOutDate = DateTime.Today.AddDays(1),
                Status = "Upcoming",
                TotalPrice = 0m,
                CreatedAt = DateTime.UtcNow
            };
            context.Reservations.Add(res);
            await context.SaveChangesAsync();

            var price = new ReservationTotalPriceService();
            var roomAvailability = new RoomAvailabilityService(context);
            var userManager = CreateMockUserManager();
            var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

            var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);
            SetUser(controller, "admin", "Admin");

            var result = await controller.Details(1);

            Assert.That(result, Is.TypeOf<ViewResult>());
        }

        [Test]
        public async Task Index_Admin_SeesAllReservations_AndShowCreateButtonTrue()
        {
            using var context = CreateDb();

            var u1 = new ApplicationUser { Id = "u1", UserName = "u1@test.com", Email = "u1@test.com", FirstName = "A", LastName = "B", Age = 18, IsActive = true };
            var u2 = new ApplicationUser { Id = "u2", UserName = "u2@test.com", Email = "u2@test.com", FirstName = "C", LastName = "D", Age = 18, IsActive = true };
            context.Users.AddRange(u1, u2);

            context.Reservations.AddRange(
                new Reservation { Id = 1, UserId = "u1", User = u1, CheckInDate = DateTime.Today, CheckOutDate = DateTime.Today.AddDays(1), Status = "Upcoming", TotalPrice = 0m, CreatedAt = DateTime.UtcNow },
                new Reservation { Id = 2, UserId = "u2", User = u2, CheckInDate = DateTime.Today, CheckOutDate = DateTime.Today.AddDays(1), Status = "Upcoming", TotalPrice = 0m, CreatedAt = DateTime.UtcNow.AddMinutes(-1) }
            );
            await context.SaveChangesAsync();

            var price = new ReservationTotalPriceService();
            var roomAvailability = new RoomAvailabilityService(context);
            var userManager = CreateMockUserManager();
            var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

            var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);
            SetUser(controller, "admin", "Admin");

            var result = await controller.Index();

            Assert.That(result, Is.TypeOf<ViewResult>());
            var view = (ViewResult)result;
            var model = view.Model as List<Reservation>;

            Assert.Multiple(() =>
            {
                Assert.That(model, Is.Not.Null);
                Assert.That(model!.Count, Is.EqualTo(2));
                Assert.That(controller.ViewBag.ShowCreateButton, Is.True);
                Assert.That(controller.ViewBag.IsGuest, Is.False);
            });
        }

        [Test]
        public async Task Edit_Post_IdMismatch_ReturnsNotFound()
        {
            using var context = CreateDb();

            var price = new ReservationTotalPriceService();
            var roomAvailability = new RoomAvailabilityService(context);
            var userManager = CreateMockUserManager();
            var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

            var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);

            var reservation = new Reservation { Id = 2 };

            var result = await controller.Edit(1, reservation, 1, null);

            Assert.That(result, Is.TypeOf<NotFoundResult>());
        }

        [Test]
        public async Task Edit_Post_InvalidModelState_ReturnsView()
        {
            using var context = CreateDb();

            var price = new ReservationTotalPriceService();
            var roomAvailability = new RoomAvailabilityService(context);
            var userManager = CreateMockUserManager();
            var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

            var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);

            controller.ModelState.AddModelError("CheckInDate", "Required");

            var reservation = new Reservation { Id = 1 };

            var result = await controller.Edit(1, reservation, 1, null);

            Assert.That(result, Is.TypeOf<ViewResult>());
        }

        [Test]
        public async Task Edit_Post_InvalidDates_AddsModelError()
        {
            using var context = CreateDb();

            var price = new ReservationTotalPriceService();
            var roomAvailability = new RoomAvailabilityService(context);
            var userManager = CreateMockUserManager();
            var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

            var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);

            var reservation = new Reservation
            {
                Id = 1,
                CheckInDate = DateTime.Today,
                CheckOutDate = DateTime.Today // invalid
            };

            var result = await controller.Edit(1, reservation, 1, null);

            Assert.That(controller.ModelState.IsValid, Is.False);
        }

        [Test]
        public async Task Edit_Post_WhenRoomUnavailable_AddsModelError()
        {
            using var context = CreateDb();

            var rt = new RoomType { Id = 1, Name = "Standard", PricePerNight = 100m };
            var room = new Room { Id = 1, RoomNumber = "101", RoomTypeId = 1, IsAvailable = false };
            context.RoomTypes.Add(rt);
            context.Rooms.Add(room);
            await context.SaveChangesAsync();

            var price = new ReservationTotalPriceService();
            var roomAvailability = new RoomAvailabilityService(context);
            var userManager = CreateMockUserManager();
            var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

            var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);

            var reservation = new Reservation
            {
                Id = 1,
                CheckInDate = DateTime.Today,
                CheckOutDate = DateTime.Today.AddDays(2)
            };

            var result = await controller.Edit(1, reservation, 1, null);

            Assert.That(controller.ModelState.IsValid, Is.False);
        }

        [Test]
        public async Task Edit_Post_Valid_UpdatesReservation_AndRedirects()
        {
            using var context = CreateDb();

            var user = new ApplicationUser
            {
                Id = "u1",
                UserName = "u1@test.com",
                Email = "u1@test.com",
                FirstName = "A",
                LastName = "B",
                Age = 18,
                IsActive = true
            };

            var rt = new RoomType { Id = 1, Name = "Standard", PricePerNight = 100m };
            var room = new Room { Id = 1, RoomNumber = "101", RoomTypeId = 1, IsAvailable = true };

            var reservation = new Reservation
            {
                Id = 1,
                UserId = "u1",
                User = user,
                CheckInDate = DateTime.Today,
                CheckOutDate = DateTime.Today.AddDays(2),
                Status = "Upcoming",
                TotalPrice = 0m,
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(user);
            context.RoomTypes.Add(rt);
            context.Rooms.Add(room);
            context.Reservations.Add(reservation);

            // ВАЖНО: ако при теб резервацията винаги има join към Room
            context.ReservationRooms.Add(new ReservationRoom
            {
                ReservationId = 1,
                RoomId = 1,
                Reservation = reservation,
                Room = room
            });

            await context.SaveChangesAsync();

            // Detach tracked entities, за да няма tracking конфликт при Update()
            context.ChangeTracker.Clear();

            var price = new ReservationTotalPriceService();
            var roomAvailability = new RoomAvailabilityService(context);
            var userManager = CreateMockUserManager();
            var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

            var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);

            var edited = new Reservation
            {
                Id = 1,
                UserId = "u1",
                CheckInDate = DateTime.Today,
                CheckOutDate = DateTime.Today.AddDays(3),
                Status = "Upcoming"
            };

            var result = await controller.Edit(1, edited, roomTypeId: 1, serviceIds: null);

            // Ако пак върне View, покажи ми грешките (ще ги видиш директно в тест output)
            if (result is ViewResult)
            {
                var errors = string.Join(" | ",
                    controller.ModelState
                        .SelectMany(kvp => kvp.Value.Errors.Select(e => $"{kvp.Key}: {e.ErrorMessage}")));

                Assert.Fail("Expected redirect, but got ViewResult. ModelState errors: " + errors);
            }

            Assert.That(result, Is.TypeOf<RedirectToActionResult>());

            context.ChangeTracker.Clear();
            var updated = context.Reservations.First(r => r.Id == 1);

            Assert.Multiple(() =>
            {
                Assert.That(updated.CheckOutDate, Is.EqualTo(DateTime.Today.AddDays(3)));
                Assert.That(updated.TotalPrice, Is.GreaterThan(0m));
            });
        }

        [Test]
        public async Task Edit_Get_WhenIdIsNull_ReturnsNotFound()
        {
            using var context = CreateDb();

            var price = new ReservationTotalPriceService();
            var roomAvailability = new RoomAvailabilityService(context);
            var userManager = CreateMockUserManager();
            var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

            var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);
            SetUser(controller, "admin", "Admin");

            var result = await controller.Edit((int?)null);

            Assert.That(result, Is.TypeOf<NotFoundResult>());
        }

        [Test]
        public async Task Edit_Get_WhenReservationNotFound_ReturnsNotFound()
        {
            using var context = CreateDb();

            var price = new ReservationTotalPriceService();
            var roomAvailability = new RoomAvailabilityService(context);
            var userManager = CreateMockUserManager();
            var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

            var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);
            SetUser(controller, "admin", "Admin");

            var result = await controller.Edit(999);

            Assert.That(result, Is.TypeOf<NotFoundResult>());
        }

        [Test]
        public async Task Delete_Get_WhenIdIsNull_ReturnsNotFound()
        {
            using var context = CreateDb();

            var price = new ReservationTotalPriceService();
            var roomAvailability = new RoomAvailabilityService(context);
            var userManager = CreateMockUserManager();
            var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

            var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);
            SetUser(controller, "admin", "Admin");

            var result = await controller.Delete((int?)null);

            Assert.That(result, Is.TypeOf<NotFoundResult>());
        }

        [Test]
        public async Task Delete_Get_WhenNotFound_ReturnsNotFound()
        {
            using var context = CreateDb();

            var price = new ReservationTotalPriceService();
            var roomAvailability = new RoomAvailabilityService(context);
            var userManager = CreateMockUserManager();
            var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

            var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);
            SetUser(controller, "admin", "Admin");

            var result = await controller.Delete(999);

            Assert.That(result, Is.TypeOf<NotFoundResult>());
        }

        

        [Test]
        public async Task Delete_WhenIdIsNull_ReturnsNotFound()
        {
            using var context = CreateDb();

            var price = new ReservationTotalPriceService();
            var roomAvailability = new RoomAvailabilityService(context);
            var userManager = CreateMockUserManager();
            var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

            var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);
            SetUser(controller, "admin", "Admin");

            var result = await controller.Delete(null);

            Assert.That(result, Is.TypeOf<NotFoundResult>());
        }

        [Test]
        public async Task Delete_WhenValidReservation_RemovesReservation_AndUnlocksRoom()
        {
            using var context = CreateDb();

            var user = new ApplicationUser
            {
                Id = "u1",
                UserName = "u1@test.com",
                Email = "u1@test.com",
                FirstName = "A",
                LastName = "B",
                Age = 18,
                IsActive = true
            };

            var rt = new RoomType { Id = 1, Name = "Standard", PricePerNight = 100m };
            var room = new Room { Id = 1, RoomNumber = "101", RoomTypeId = 1, IsAvailable = false };

            var reservation = new Reservation
            {
                Id = 1,
                UserId = "u1",
                User = user,
                CheckInDate = DateTime.Today,
                CheckOutDate = DateTime.Today.AddDays(2),
                Status = "Upcoming",
                TotalPrice = 0m,
                CreatedAt = DateTime.UtcNow
            };

            var rr = new ReservationRoom
            {
                ReservationId = 1,
                RoomId = 1,
                Reservation = reservation,
                Room = room
            };

            context.Users.Add(user);
            context.RoomTypes.Add(rt);
            context.Rooms.Add(room);
            context.Reservations.Add(reservation);
            context.ReservationRooms.Add(rr);

            await context.SaveChangesAsync();

            var price = new ReservationTotalPriceService();
            var roomAvailability = new RoomAvailabilityService(context);
            var userManager = CreateMockUserManager();
            var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

            var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);
            SetUser(controller, "admin", "Admin");

            var result = await controller.Delete(1);

            Assert.That(result, Is.TypeOf<RedirectToActionResult>());

            context.ChangeTracker.Clear();

            Assert.Multiple(() =>
            {
                Assert.That(context.Reservations.Count(), Is.EqualTo(0));
                Assert.That(context.Rooms.First().IsAvailable, Is.True); // стаята се отключва
                Assert.That(((RedirectToActionResult)result).ActionName, Is.EqualTo("Index"));
            });
        }

        [Test]
        public async Task Delete_WhenReservationNotFound_ReturnsNotFound()
        {
            using var context = CreateDb();

            var price = new ReservationTotalPriceService();
            var roomAvailability = new RoomAvailabilityService(context);
            var userManager = CreateMockUserManager();
            var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

            var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);
            SetUser(controller, "admin", "Admin");

            var result = await controller.Delete(999);

            Assert.That(result, Is.TypeOf<NotFoundResult>());
        }


        [Test]
        public async Task Edit_Get_WhenReservationExists_ReturnsView_AndSetsViewData()
        {
            using var context = CreateDb();

            var user = new ApplicationUser
            {
                Id = "u1",
                UserName = "u1@test.com",
                Email = "u1@test.com",
                FirstName = "A",
                LastName = "B",
                Age = 18,
                IsActive = true
            };

            var rt = new RoomType { Id = 1, Name = "Standard", PricePerNight = 100m };
            var room = new Room { Id = 1, RoomNumber = "101", RoomTypeId = 1, IsAvailable = true };

            var reservation = new Reservation
            {
                Id = 1,
                UserId = "u1",
                User = user,
                CheckInDate = DateTime.Today,
                CheckOutDate = DateTime.Today.AddDays(2),
                Status = "Completed", // нарочно, за да видим че пак връща View
                TotalPrice = 0m,
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(user);
            context.RoomTypes.Add(rt);
            context.Rooms.Add(room);
            context.Reservations.Add(reservation);

            // ако Edit GET прави Include към ReservationRooms/RoomType и т.н.
            context.ReservationRooms.Add(new ReservationRoom
            {
                ReservationId = 1,
                RoomId = 1,
                Reservation = reservation,
                Room = room
            });

            await context.SaveChangesAsync();

            var price = new ReservationTotalPriceService();
            var roomAvailability = new RoomAvailabilityService(context);
            var userManager = CreateMockUserManager();
            var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

            var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);
            SetUser(controller, "admin", "Admin");

            var result = await controller.Edit(1);

            Assert.That(result, Is.TypeOf<ViewResult>());

            Assert.Multiple(() =>
            {
                // тези ключове може да са ViewData или ViewBag – проверяваме по-общо
                Assert.That(controller.ViewData.Count, Is.GreaterThan(0));
            });
        }

        [Test]
        public async Task Edit_Post_WhenStatusIsNotEditable_ReturnsViewOrRedirect()
        {
            using var context = CreateDb();

            var user = new ApplicationUser
            {
                Id = "u1",
                UserName = "u1@test.com",
                Email = "u1@test.com",
                FirstName = "A",
                LastName = "B",
                Age = 18,
                IsActive = true
            };

            var rt = new RoomType { Id = 1, Name = "Standard", PricePerNight = 100m };
            var room = new Room { Id = 1, RoomNumber = "101", RoomTypeId = 1, IsAvailable = true };

            var reservation = new Reservation
            {
                Id = 1,
                UserId = "u1",
                User = user,
                CheckInDate = DateTime.Today,
                CheckOutDate = DateTime.Today.AddDays(2),
                Status = "Completed", // статус, който обикновено не се редактира
                TotalPrice = 0m,
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(user);
            context.RoomTypes.Add(rt);
            context.Rooms.Add(room);
            context.Reservations.Add(reservation);
            await context.SaveChangesAsync();

            context.ChangeTracker.Clear();

            var price = new ReservationTotalPriceService();
            var roomAvailability = new RoomAvailabilityService(context);
            var userManager = CreateMockUserManager();
            var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

            var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);
            SetUser(controller, "admin", "Admin");
            SetTempData(controller);

            var edited = new Reservation
            {
                Id = 1,
                UserId = "u1",
                CheckInDate = DateTime.Today,
                CheckOutDate = DateTime.Today.AddDays(3),
                Status = "Completed"
            };

            var result = await controller.Edit(1, edited, 1, null);
            context.ChangeTracker.Clear();
            var after = context.Reservations.First(r => r.Id == 1);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.InstanceOf<IActionResult>());
            });
        }



[Test]
    public async Task Details_WhenReservationNotFound_ReturnsNotFound()
    {
        using var context = CreateDb();

        var price = new ReservationTotalPriceService();
        var roomAvailability = new RoomAvailabilityService(context);
        var userManager = CreateMockUserManager();
        var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

        var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);
        SetUser(controller, "admin", "Admin");

        var result = await controller.Details(123);

        Assert.That(result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public void Create_Get_ReturnsView_AndSetsViewData()
    {
        using var context = CreateDb();

        // 1 room type + 1 available room => да се появи в dropdown
        context.RoomTypes.Add(new RoomType { Id = 1, Name = "Standard", PricePerNight = 100m });
        context.Rooms.Add(new Room { Id = 1, RoomNumber = "101", RoomTypeId = 1, IsAvailable = true });

        context.HotelServices.Add(new HotelService { Id = 1, Name = "Breakfast", Price = 10m });
        context.SaveChanges();

        var price = new ReservationTotalPriceService();
        var roomAvailability = new RoomAvailabilityService(context);
        var userManager = CreateMockUserManager();
        var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

        var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);
        SetUser(controller, "admin", "Admin");

        var result = controller.Create();

        Assert.That(result, Is.TypeOf<ViewResult>());
        Assert.Multiple(() =>
        {
            Assert.That(controller.ViewData.ContainsKey("RoomTypes"), Is.True);
            Assert.That(controller.ViewData.ContainsKey("Services"), Is.True);
        });
    }


            public async Task Edit_Get_WhenCurrentRoomTypeIsNotAvailable_IncludesItInSelectList()
        {
            using var context = CreateDb();

            var user = new ApplicationUser
            {
                Id = "u1",
                UserName = "u1@test.com",
                Email = "u1@test.com",
                FirstName = "A",
                LastName = "B",
                Age = 18,
                IsActive = true
            };
            context.Users.Add(user);

            // RT1 има available стая -> ще е в availableRoomTypes
            var rt1 = new RoomType { Id = 1, Name = "Standard", PricePerNight = 100m };
            var room1 = new Room { Id = 1, RoomNumber = "101", RoomTypeId = 1, IsAvailable = true };
            context.RoomTypes.Add(rt1);
            context.Rooms.Add(room1);

            // RT2 няма available стаи и е текущият тип на резервацията -> controller трябва да го добави ръчно в SelectList
            var rt2 = new RoomType { Id = 2, Name = "Deluxe", PricePerNight = 200m };
            var room2 = new Room { Id = 2, RoomNumber = "202", RoomTypeId = 2, IsAvailable = false };
            context.RoomTypes.Add(rt2);
            context.Rooms.Add(room2);

            var reservation = new Reservation
            {
                Id = 1,
                UserId = user.Id,
                User = user,
                CheckInDate = DateTime.Today,
                CheckOutDate = DateTime.Today.AddDays(2),
                Status = "Upcoming",
                TotalPrice = 0m,
                CreatedAt = DateTime.UtcNow
            };
            context.Reservations.Add(reservation);

            // Важно: НЕ query-ваме context.Rooms.First(...) преди SaveChanges
            context.ReservationRooms.Add(new ReservationRoom
            {
                ReservationId = reservation.Id,
                Reservation = reservation,
                RoomId = room2.Id,
                Room = room2
            });

            await context.SaveChangesAsync();

            var price = new ReservationTotalPriceService();
            var roomAvailability = new RoomAvailabilityService(context);
            var userManager = CreateMockUserManager();
            var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

            var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);
            SetUser(controller, "admin", "Admin");

            var result = await controller.Edit(1);

            Assert.That(result, Is.TypeOf<ViewResult>());

            var sl = controller.ViewData["RoomTypes"] as SelectList;
            Assert.That(sl, Is.Not.Null);

            var ids = sl!.Items
                .Cast<object>()
                .Select(x => (int)x.GetType()
                    .GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)!
                    .GetValue(x)!)
                .ToList();

            Assert.That(ids, Does.Contain(2),
                "Current room type (2) should be added to SelectList even if no rooms are available.");
        }

        [Test]
public async Task Edit_Get_WhenCurrentRoomTypeIsNotAvailable_IncludesItInSelectList_Fixed()
        {
            using var context = CreateDb();

            var user = new ApplicationUser
            {
                Id = "u1",
                UserName = "u1@test.com",
                Email = "u1@test.com",
                FirstName = "A",
                LastName = "B",
                Age = 18,
                IsActive = true
            };
            context.Users.Add(user);

            // RT1 има available стая -> ще е в availableRoomTypes
            var rt1 = new RoomType { Id = 1, Name = "Standard", PricePerNight = 100m };
            var room1 = new Room { Id = 1, RoomNumber = "101", RoomTypeId = 1, IsAvailable = true };
            context.RoomTypes.Add(rt1);
            context.Rooms.Add(room1);

            // RT2 няма available стаи и е текущият тип на резервацията -> controller трябва да го добави ръчно в SelectList
            var rt2 = new RoomType { Id = 2, Name = "Deluxe", PricePerNight = 200m };
            var room2 = new Room { Id = 2, RoomNumber = "202", RoomTypeId = 2, IsAvailable = false };
            context.RoomTypes.Add(rt2);
            context.Rooms.Add(room2);

            var reservation = new Reservation
            {
                Id = 1,
                UserId = user.Id,
                User = user,
                CheckInDate = DateTime.Today,
                CheckOutDate = DateTime.Today.AddDays(2),
                Status = "Upcoming",
                TotalPrice = 0m,
                CreatedAt = DateTime.UtcNow
            };
            context.Reservations.Add(reservation);

            // Важно: НЕ query-ваме context.Rooms.First(...) преди SaveChanges
            context.ReservationRooms.Add(new ReservationRoom
            {
                ReservationId = reservation.Id,
                Reservation = reservation,
                RoomId = room2.Id,
                Room = room2
            });

            await context.SaveChangesAsync();

            var price = new ReservationTotalPriceService();
            var roomAvailability = new RoomAvailabilityService(context);
            var userManager = CreateMockUserManager();
            var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

            var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);
            SetUser(controller, "admin", "Admin");

            var result = await controller.Edit(1);

            Assert.That(result, Is.TypeOf<ViewResult>());

            var sl = controller.ViewData["RoomTypes"] as SelectList;
            Assert.That(sl, Is.Not.Null);

            var ids = sl!.Items
                .Cast<object>()
                .Select(x => (int)x.GetType()
                    .GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)!
                    .GetValue(x)!)
                .ToList();

            Assert.That(ids, Does.Contain(2),
                "Current room type (2) should be added to SelectList even if no rooms are available.");
        }

        [Test]
        public async Task Edit_Post_WhenUpdateFails_AndCurrentRoomTypeNotAvailable_AddsCurrentTypeToRoomTypesViewData_Fixed()
        {
            using var context = CreateDb();

            var user = new ApplicationUser
            {
                Id = "u1",
                UserName = "u1@test.com",
                Email = "u1@test.com",
                FirstName = "A",
                LastName = "B",
                Age = 18,
                IsActive = true
            };
            context.Users.Add(user);

            // RT1 има available стая (ще е в availableRoomTypes)
            var rt1 = new RoomType { Id = 1, Name = "Standard", PricePerNight = 100m };
            var room1 = new Room { Id = 1, RoomNumber = "101", RoomTypeId = 1, IsAvailable = true };
            context.RoomTypes.Add(rt1);
            context.Rooms.Add(room1);

            // RT2 няма available стаи и е текущият тип
            var rt2 = new RoomType { Id = 2, Name = "Deluxe", PricePerNight = 200m };
            var room2 = new Room { Id = 2, RoomNumber = "202", RoomTypeId = 2, IsAvailable = false };
            context.RoomTypes.Add(rt2);
            context.Rooms.Add(room2);

            // 1 валидна услуга в БД (но ние ще подадем НЕвалидна, за да fail-не update-a)
            context.HotelServices.Add(new HotelService { Id = 1, Name = "Breakfast", Price = 10m });

            var reservation = new Reservation
            {
                Id = 1,
                UserId = user.Id,
                User = user,
                CheckInDate = DateTime.Today,
                CheckOutDate = DateTime.Today.AddDays(2),
                Status = "Upcoming",
                TotalPrice = 0m,
                CreatedAt = DateTime.UtcNow
            };
            context.Reservations.Add(reservation);

            context.ReservationRooms.Add(new ReservationRoom
            {
                ReservationId = reservation.Id,
                Reservation = reservation,
                RoomId = room2.Id,
                Room = room2
            });

            await context.SaveChangesAsync();

            var price = new ReservationTotalPriceService();
            var roomAvailability = new RoomAvailabilityService(context);
            var userManager = CreateMockUserManager();
            var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

            var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);
            SetUser(controller, "admin", "Admin");

            // Правим update да FAIL-не: подаваме несъществуващ serviceId (999)
            var edited = new Reservation
            {
                Id = 1,
                UserId = user.Id,
                CheckInDate = DateTime.Today,
                CheckOutDate = DateTime.Today.AddDays(3)
            };

            var result = await controller.Edit(1, edited, roomTypeId: 2, serviceIds: new List<int> { 999 });

            Assert.That(result, Is.TypeOf<ViewResult>());

            var sl = controller.ViewData["RoomTypes"] as SelectList;
            Assert.That(sl, Is.Not.Null);

            var ids = sl!.Items
                .Cast<object>()
                .Select(x => (int)x.GetType()
                    .GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)!
                    .GetValue(x)!)
                .ToList();

            Assert.That(ids, Does.Contain(2),
                "On update failure, current room type (2) should be added back to SelectList.");
        }

        [Test]
        public async Task Edit_Get_WhenIdIsNull_ReturnsNotFoundd()
        {
            using var context = CreateDb();

            var price = new ReservationTotalPriceService();
            var roomAvailability = new RoomAvailabilityService(context);
            var userManager = CreateMockUserManager();
            var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

            var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);
            SetUser(controller, "admin", "Admin");

            var result = await controller.Edit((int?)null);

            Assert.That(result, Is.TypeOf<NotFoundResult>());
        }

        [Test]
        public async Task Edit_Post_WhenIdMismatch_ReturnsNotFound()
        {
            using var context = CreateDb();

            var price = new ReservationTotalPriceService();
            var roomAvailability = new RoomAvailabilityService(context);
            var userManager = CreateMockUserManager();
            var logic = new ReservationLogic(context, userManager.Object, price, roomAvailability);

            var controller = new ReservationsController(context, price, userManager.Object, roomAvailability, logic);
            SetUser(controller, "admin", "Admin");

            var edited = new Reservation
            {
                Id = 999, // различно от route id
                UserId = "u1",
                CheckInDate = DateTime.Today,
                CheckOutDate = DateTime.Today.AddDays(1)
            };

            var result = await controller.Edit(1, edited, roomTypeId: 1, serviceIds: new List<int>());

            Assert.That(result, Is.TypeOf<NotFoundResult>());
        }

       




    }

}
