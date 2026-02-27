using Hotel_Manager.Controllers;
using Hotel_Manager.Data;
using Hotel_Manager.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Update;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
namespace Hotel_Manager.Tests.Controllers
{
    public class RoomsControllerTests
    {
        [Test]
        public async Task Index_ReturnsView_WithAllRooms()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new ApplicationDbContext(options);

            var roomType = new RoomType { Id = 1, Name = "Deluxe" };
            context.RoomTypes.Add(roomType);

            context.Rooms.AddRange(
                new Room { Id = 1, RoomNumber = "101", RoomTypeId = 1, IsAvailable = true },
                new Room { Id = 2, RoomNumber = "102", RoomTypeId = 1, IsAvailable = false }
            );

            await context.SaveChangesAsync();

            var mockAvailabilityService = new Mock<RoomAvailabilityService>(context);

            var controller = new RoomsController(context, mockAvailabilityService.Object);

            // Act
            var result = await controller.Index();

            // Assert
            Assert.That(result, Is.TypeOf<ViewResult>());

            var viewResult = result as ViewResult;
            var model = viewResult.Model as List<Room>;

            Assert.Multiple(() =>
            {
                Assert.That(model, Is.Not.Null);
                Assert.That(model.Count, Is.EqualTo(2));
                Assert.That(model.Any(r => r.RoomNumber == "101"), Is.True);
                Assert.That(model.Any(r => r.RoomNumber == "102"), Is.True);
            });
        }
        [Test]
        public async Task Details_WhenIdIsNull_ReturnsNotFound()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new ApplicationDbContext(options);
            var service = new RoomAvailabilityService(context);
            var controller = new RoomsController(context, service);

            var result = await controller.Details(null);

            Assert.That(result, Is.TypeOf<NotFoundResult>());
        }

        [Test]
        public async Task Details_WhenRoomDoesNotExist_ReturnsNotFound()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new ApplicationDbContext(options);
            var service = new RoomAvailabilityService(context);
            var controller = new RoomsController(context, service);

            var result = await controller.Details(1);

            Assert.That(result, Is.TypeOf<NotFoundResult>());
        }

        [Test]
        public async Task Details_WhenRoomExists_ReturnsViewWithRoom()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new ApplicationDbContext(options);

            context.RoomTypes.Add(new RoomType { Id = 1, Name = "Standard" });
            context.Rooms.Add(new Room
            {
                Id = 1,
                RoomNumber = "101",
                RoomTypeId = 1,
                IsAvailable = true
            });

            await context.SaveChangesAsync();

            var service = new RoomAvailabilityService(context);
            var controller = new RoomsController(context, service);

            var result = await controller.Details(1);

            Assert.That(result, Is.TypeOf<ViewResult>());

            var view = result as ViewResult;
            var model = view.Model as Room;

            Assert.Multiple(() =>
            {
                Assert.That(model, Is.Not.Null);
                Assert.That(model.Id, Is.EqualTo(1));
                Assert.That(model.RoomNumber, Is.EqualTo("101"));
            });
        }

        [Test]
        public async Task Create_Post_InvalidModelState_ReturnsView()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new ApplicationDbContext(options);

            context.RoomTypes.Add(new RoomType { Id = 1, Name = "Standard" });
            await context.SaveChangesAsync();

            var service = new RoomAvailabilityService(context);
            var controller = new RoomsController(context, service);

            controller.ModelState.AddModelError("RoomNumber", "Required");

            var room = new Room
            {
                Id = 1,
                RoomNumber = "",
                RoomTypeId = 1
            };

            var result = await controller.Create(room);

            Assert.That(result, Is.TypeOf<ViewResult>());

            var view = result as ViewResult;

            Assert.Multiple(() =>
            {
                Assert.That(view.Model, Is.EqualTo(room));
                Assert.That(context.Rooms.Count(), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task Create_Post_ValidModel_AddsRoomAndRedirects()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new ApplicationDbContext(options);

            context.RoomTypes.Add(new RoomType { Id = 1, Name = "Standard" });
            await context.SaveChangesAsync();

            var service = new RoomAvailabilityService(context);
            var controller = new RoomsController(context, service);

            var room = new Room
            {
                Id = 1,
                RoomNumber = "101",
                RoomTypeId = 1,
                IsAvailable = true
            };

            var result = await controller.Create(room);

            Assert.That(result, Is.TypeOf<RedirectToActionResult>());

            var redirect = result as RedirectToActionResult;

            Assert.Multiple(() =>
            {
                Assert.That(redirect.ActionName, Is.EqualTo("Index"));
                Assert.That(context.Rooms.Count(), Is.EqualTo(1));
                Assert.That(context.Rooms.First().RoomNumber, Is.EqualTo("101"));
            });
        }

        [Test]
        public async Task Edit_Post_IdMismatch_ReturnsNotFound()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new ApplicationDbContext(options);

            context.RoomTypes.Add(new RoomType { Id = 1, Name = "Standard" });
            context.Rooms.Add(new Room
            {
                Id = 1,
                RoomNumber = "101",
                RoomTypeId = 1,
                IsAvailable = true
            });

            await context.SaveChangesAsync();

            var service = new RoomAvailabilityService(context);
            var controller = new RoomsController(context, service);

            var editedRoom = new Room
            {
                Id = 2, // различно от id параметъра
                RoomNumber = "102",
                RoomTypeId = 1,
                IsAvailable = false
            };

            var result = await controller.Edit(1, editedRoom);

            Assert.That(result, Is.TypeOf<NotFoundResult>());
        }

        [Test]
        public async Task Edit_Post_InvalidModelState_ReturnsViewWithoutUpdating()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new ApplicationDbContext(options);

            context.RoomTypes.Add(new RoomType { Id = 1, Name = "Standard" });

            context.Rooms.Add(new Room
            {
                Id = 1,
                RoomNumber = "101",
                RoomTypeId = 1,
                IsAvailable = true
            });

            await context.SaveChangesAsync();

            var service = new RoomAvailabilityService(context);
            var controller = new RoomsController(context, service);

            controller.ModelState.AddModelError("RoomNumber", "Required");

            var editedRoom = new Room
            {
                Id = 1,
                RoomNumber = "",   // невалидно
                RoomTypeId = 1,
                IsAvailable = false
            };

            var result = await controller.Edit(1, editedRoom);

            Assert.That(result, Is.TypeOf<ViewResult>());

            var roomInDb = context.Rooms.First();

            Assert.Multiple(() =>
            {
                Assert.That(roomInDb.RoomNumber, Is.EqualTo("101")); // не е променено
                Assert.That(roomInDb.IsAvailable, Is.True);          // не е променено
            });
        }
        [Test]
        public async Task Edit_Post_ValidModel_UpdatesRoomAndRedirects()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new ApplicationDbContext(options);

            context.RoomTypes.Add(new RoomType { Id = 1, Name = "Standard" });

            var originalRoom = new Room
            {
                Id = 1,
                RoomNumber = "101",
                RoomTypeId = 1
            };

            context.Rooms.Add(originalRoom);
            await context.SaveChangesAsync();

            // МНОГО ВАЖНО – махаме tracking-а
            context.Entry(originalRoom).State = EntityState.Detached;

            var service = new RoomAvailabilityService(context);
            var controller = new RoomsController(context, service);

            var editedRoom = new Room
            {
                Id = 1,
                RoomNumber = "202",
                RoomTypeId = 1
            };

            var result = await controller.Edit(1, editedRoom);

            context.ChangeTracker.Clear();
            var updatedRoom = context.Rooms.First();

            Assert.That(result, Is.TypeOf<RedirectToActionResult>());

            Assert.Multiple(() =>
            {
                Assert.That(updatedRoom.RoomNumber, Is.EqualTo("202"));
                Assert.That(((RedirectToActionResult)result).ActionName, Is.EqualTo("Index"));
            });
        }

        [Test]
        public async Task Edit_Post_WhenRoomDeletedDuringUpdate_ReturnsNotFound()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new ApplicationDbContext(options);

            context.RoomTypes.Add(new RoomType { Id = 1, Name = "Standard" });

            var room = new Room
            {
                Id = 1,
                RoomNumber = "101",
                RoomTypeId = 1,
                IsAvailable = true
            };

            context.Rooms.Add(room);
            await context.SaveChangesAsync();

            var service = new RoomAvailabilityService(context);
            var controller = new RoomsController(context, service);

            // изтриваме стаята преди update
            context.Rooms.Remove(room);
            await context.SaveChangesAsync();

            var editedRoom = new Room
            {
                Id = 1,
                RoomNumber = "999",
                RoomTypeId = 1,
                IsAvailable = false
            };

            var result = await controller.Edit(1, editedRoom);

            Assert.That(result, Is.TypeOf<NotFoundResult>());
        }

        [Test]
        public async Task DeleteConfirmed_RemovesRoomAndRedirects()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new ApplicationDbContext(options);

            context.RoomTypes.Add(new RoomType { Id = 1, Name = "Standard" });

            var room = new Room
            {
                Id = 1,
                RoomNumber = "101",
                RoomTypeId = 1,
                IsAvailable = true
            };

            context.Rooms.Add(room);
            await context.SaveChangesAsync();

            var service = new RoomAvailabilityService(context);
            var controller = new RoomsController(context, service);

            var result = await controller.DeleteConfirmed(1);

            Assert.That(result, Is.TypeOf<RedirectToActionResult>());

            Assert.Multiple(() =>
            {
                Assert.That(context.Rooms.Count(), Is.EqualTo(0));
                Assert.That(((RedirectToActionResult)result).ActionName, Is.EqualTo("Index"));
            });
        }

        [Test]
        public async Task Delete_Get_WhenIdIsNull_ReturnsNotFound()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new ApplicationDbContext(options);
            var service = new RoomAvailabilityService(context);
            var controller = new RoomsController(context, service);

            var result = await controller.Delete(null);

            Assert.That(result, Is.TypeOf<NotFoundResult>());
        }

        [Test]
        public async Task Delete_Get_WhenRoomDoesNotExist_ReturnsNotFound()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new ApplicationDbContext(options);
            var service = new RoomAvailabilityService(context);
            var controller = new RoomsController(context, service);

            var result = await controller.Delete(1);

            Assert.That(result, Is.TypeOf<NotFoundResult>());
        }

        [Test]
        public async Task Delete_Get_WhenRoomExists_ReturnsViewWithRoom()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new ApplicationDbContext(options);

            context.RoomTypes.Add(new RoomType { Id = 1, Name = "Standard" });
            context.Rooms.Add(new Room
            {
                Id = 1,
                RoomNumber = "101",
                RoomTypeId = 1
            });

            await context.SaveChangesAsync();

            var service = new RoomAvailabilityService(context);
            var controller = new RoomsController(context, service);

            var result = await controller.Delete(1);

            Assert.That(result, Is.TypeOf<ViewResult>());

            var view = result as ViewResult;
            var model = view.Model as Room;

            Assert.Multiple(() =>
            {
                Assert.That(model, Is.Not.Null);
                Assert.That(model.Id, Is.EqualTo(1));
                Assert.That(model.RoomNumber, Is.EqualTo("101"));
            });
        }

        [Test]
        public void Create_Get_ReturnsViewWithRoomTypes()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new ApplicationDbContext(options);

            context.RoomTypes.Add(new RoomType { Id = 1, Name = "Standard" });
            context.RoomTypes.Add(new RoomType { Id = 2, Name = "Deluxe" });
            context.SaveChanges();

            var service = new RoomAvailabilityService(context);
            var controller = new RoomsController(context, service);

            var result = controller.Create();

            Assert.That(result, Is.TypeOf<ViewResult>());
            Assert.That(controller.ViewBag.RoomTypeId, Is.Not.Null);
        }

        [Test]
        public async Task Create_Post_InvalidRoomTypeId_ReturnsViewAndAddsModelError()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new ApplicationDbContext(options);

            // имаме само RoomType с Id=1, но ще подадем 999
            context.RoomTypes.Add(new RoomType { Id = 1, Name = "Standard" });
            await context.SaveChangesAsync();

            var service = new RoomAvailabilityService(context);
            var controller = new RoomsController(context, service);

            var room = new Room
            {
                Id = 1,
                RoomNumber = "101",
                IsAvailable = true,
                RoomTypeId = 999
            };

            var result = await controller.Create(room);

            Assert.That(result, Is.TypeOf<ViewResult>());
            var view = (ViewResult)result;

            Assert.Multiple(() =>
            {
                Assert.That(controller.ModelState.IsValid, Is.False);
                Assert.That(controller.ModelState.ContainsKey("RoomTypeId"), Is.True);
                Assert.That(view.Model, Is.EqualTo(room));
                Assert.That(controller.ViewBag.RoomTypeId, Is.Not.Null);
                Assert.That(context.Rooms.Count(), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task Edit_Get_WhenIdIsNull_ReturnsNotFound()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new ApplicationDbContext(options);
            var service = new RoomAvailabilityService(context);
            var controller = new RoomsController(context, service);

            var result = await controller.Edit((int?)null);

            Assert.That(result, Is.TypeOf<NotFoundResult>());
        }

        [Test]
        public async Task Edit_Get_WhenRoomDoesNotExist_ReturnsNotFound()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new ApplicationDbContext(options);
            var service = new RoomAvailabilityService(context);
            var controller = new RoomsController(context, service);

            var result = await controller.Edit(123);

            Assert.That(result, Is.TypeOf<NotFoundResult>());
        }

        [Test]
        public async Task Edit_Get_WhenRoomIsLocked_RedirectsToIndex_AndSetsTempDataError()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new ApplicationDbContext(options);

            var user = new ApplicationUser
            {
                Id = "u1",
                UserName = "u1@test.com",
                Email = "u1@test.com",
                FirstName = "Test",
                LastName = "User",
                Age = 18,
                IsActive = true
            };

            var roomType = new RoomType { Id = 1, Name = "Standard", PricePerNight = 100m };
            var room = new Room { Id = 1, RoomNumber = "101", RoomTypeId = 1, IsAvailable = true };

            var reservation = new Reservation
            {
                Id = 1,
                CheckInDate = DateTime.Today,
                CheckOutDate = DateTime.Today.AddDays(2),
                Status = "Pending",
                TotalPrice = 0m,

                UserId = user.Id,
                User = user
            };

            var rr = new ReservationRoom
            {
                ReservationId = reservation.Id,
                RoomId = room.Id,
                Reservation = reservation,
                Room = room
            };

            context.Users.Add(user);
            context.RoomTypes.Add(roomType);
            context.Rooms.Add(room);
            context.Reservations.Add(reservation);
            context.ReservationRooms.Add(rr);

            await context.SaveChangesAsync();

            var service = new RoomAvailabilityService(context);

            // sanity check: наистина да е locked
            var locked = await service.IsRoomLockedAsync(room.Id);
            Assert.That(locked, Is.True);

            var controller = new RoomsController(context, service);
            controller.TempData = new TempDataDictionary(
                new DefaultHttpContext(),
                Mock.Of<ITempDataProvider>());

            var result = await controller.Edit(room.Id);

            Assert.That(result, Is.TypeOf<RedirectToActionResult>());
            var redirect = (RedirectToActionResult)result;

            Assert.Multiple(() =>
            {
                Assert.That(redirect.ActionName, Is.EqualTo("Index"));
                Assert.That(controller.TempData.ContainsKey("Error"), Is.True);
            });
        }

        [Test]
        public async Task Edit_Get_WhenRoomIsNotLocked_ReturnsViewWithRoom_AndSetsViewData()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new ApplicationDbContext(options);

            context.RoomTypes.Add(new RoomType { Id = 1, Name = "Standard" });
            context.RoomTypes.Add(new RoomType { Id = 2, Name = "Deluxe" });

            context.Rooms.Add(new Room { Id = 1, RoomNumber = "101", RoomTypeId = 2, IsAvailable = true });
            await context.SaveChangesAsync();

            var service = new RoomAvailabilityService(context);
            var controller = new RoomsController(context, service);

            var result = await controller.Edit(1);

            Assert.That(result, Is.TypeOf<ViewResult>());
            var view = (ViewResult)result;
            var model = view.Model as Room;

            Assert.Multiple(() =>
            {
                Assert.That(model, Is.Not.Null);
                Assert.That(model.Id, Is.EqualTo(1));
                Assert.That(controller.ViewData.ContainsKey("RoomTypeId"), Is.True);
                Assert.That(controller.ViewData["RoomTypeId"], Is.TypeOf<SelectList>());
            });
        }

        [Test]
        public async Task DeleteConfirmed_WhenRoomIsNull_StillRedirectsAndDoesNotThrow()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new ApplicationDbContext(options);
            var service = new RoomAvailabilityService(context);
            var controller = new RoomsController(context, service);

            var result = await controller.DeleteConfirmed(999);

            Assert.That(result, Is.TypeOf<RedirectToActionResult>());

            Assert.Multiple(() =>
            {
                Assert.That(((RedirectToActionResult)result).ActionName, Is.EqualTo("Index"));
                Assert.That(context.Rooms.Count(), Is.EqualTo(0));
            });
        }

        public class ThrowingApplicationDbContext : ApplicationDbContext
        {
            public ThrowingApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
                : base(options)
            {
            }

            public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
                => throw new DbUpdateConcurrencyException("Forced concurrency exception for test.");
        }

        [Test]
        public async Task Edit_Post_WhenConcurrencyExceptionAndRoomStillExists_ShouldRethrow()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;

            // Seed with NORMAL context (за да можем да записваме)
            using (var seedContext = new ApplicationDbContext(options))
            {
                seedContext.RoomTypes.Add(new RoomType { Id = 1, Name = "Standard" });
                seedContext.Rooms.Add(new Room
                {
                    Id = 1,
                    RoomNumber = "101",
                    RoomTypeId = 1
                });
                await seedContext.SaveChangesAsync();
            }

            // Use THROWING context for controller call
            using var context = new ThrowingApplicationDbContext(options);
            var service = new RoomAvailabilityService(context);
            var controller = new RoomsController(context, service);

            var editedRoom = new Room
            {
                Id = 1,
                RoomNumber = "202",
                RoomTypeId = 1
            };

            // Act + Assert (точно rethrow branch-а)
            Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () =>
                await controller.Edit(1, editedRoom));
        }


    }
}
