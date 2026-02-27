using Hotel_Manager.Controllers;
using Hotel_Manager.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace HotelManager.Tests.Controllers
{
    [TestFixture]
    public class AdminUsersControllerTests
    {
        private Mock<UserManager<ApplicationUser>> _userManagerMock = null!;
        private Mock<RoleManager<IdentityRole>> _roleManagerMock = null!;
        private AdminUsersController _controller = null!;

        [SetUp]
        public void Setup()
        {
            var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
            _userManagerMock = new Mock<UserManager<ApplicationUser>>(
                userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!
            );

            var roleStoreMock = new Mock<IRoleStore<IdentityRole>>();
            _roleManagerMock = new Mock<RoleManager<IdentityRole>>(
                roleStoreMock.Object, null!, null!, null!, null!
            );

            _controller = new AdminUsersController(_userManagerMock.Object, _roleManagerMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _controller?.Dispose();
        }

        private void SetupControllerAsAdmin()
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "admin-id"),
                new Claim(ClaimTypes.Role, "Admin")
            };

            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }

        [Test]
        public async Task Index_ReturnsViewWithAllUsersAndTheirRoles()
        {
            var users = new List<ApplicationUser>
            {
                new ApplicationUser { Id = "u1", Email = "a@test.com", FirstName = "A", LastName = "B", Age = 30, IsActive = true },
                new ApplicationUser { Id = "u2", Email = "b@test.com", FirstName = "C", LastName = "D", Age = 25, IsActive = false }
            };

            _userManagerMock.Setup(m => m.Users).Returns(users.AsQueryable());

            _userManagerMock.Setup(m => m.GetRolesAsync(users[0]))
                .ReturnsAsync(new List<string> { "Admin", "Receptionist" });

            _userManagerMock.Setup(m => m.GetRolesAsync(users[1]))
                .ReturnsAsync(new List<string> { "Guest" });

            SetupControllerAsAdmin();

            var result = await _controller.Index() as ViewResult;

            Assert.That(result, Is.Not.Null);

            var model = result!.Model as List<UserViewModel>;
            Assert.That(model, Is.Not.Null);
            Assert.That(model!.Count, Is.EqualTo(2));

            Assert.That(model[0].Id, Is.EqualTo("u1"));
            Assert.That(model[0].Email, Is.EqualTo("a@test.com"));
            Assert.That(model[0].Roles, Is.EqualTo("Admin, Receptionist"));

            Assert.That(model[1].Id, Is.EqualTo("u2"));
            Assert.That(model[1].IsActive, Is.False);
        }

        [Test]
        public async Task Details_ValidId_ReturnsUserViewModel()
        {
            var user = new ApplicationUser
            {
                Id = "u-details",
                Email = "details@test.com",
                FirstName = "John",
                LastName = "Doe",
                Age = 35,
                IsActive = true
            };

            _userManagerMock.Setup(m => m.FindByIdAsync("u-details"))
                .ReturnsAsync(user);

            _userManagerMock.Setup(m => m.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "Receptionist" });

            SetupControllerAsAdmin();

            var result = await _controller.Details("u-details") as ViewResult;

            Assert.That(result, Is.Not.Null);

            var model = result!.Model as UserViewModel;
            Assert.That(model, Is.Not.Null);
            Assert.That(model!.Id, Is.EqualTo("u-details"));
            Assert.That(model.Email, Is.EqualTo("details@test.com"));
            Assert.That(model.Roles, Is.EqualTo("Receptionist"));
        }

        [Test]
        public async Task Details_NullOrEmptyId_ReturnsNotFound()
        {
            var resultNull = await _controller.Details(null) as NotFoundResult;
            Assert.That(resultNull, Is.Not.Null);

            var resultEmpty = await _controller.Details("") as NotFoundResult;
            Assert.That(resultEmpty, Is.Not.Null);
        }

        [Test]
        public async Task Details_UserNotFound_ReturnsNotFound()
        {
            _userManagerMock.Setup(m => m.FindByIdAsync("missing"))
                .ReturnsAsync((ApplicationUser?)null);

            SetupControllerAsAdmin();

            var result = await _controller.Details("missing") as NotFoundResult;
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public void Create_Get_ReturnsViewWithRoleSelectList()
        {
            SetupControllerAsAdmin();

            var result = _controller.Create() as ViewResult;

            Assert.That(result, Is.Not.Null);

            var selectList = _controller.ViewBag.Roles as SelectList;
            Assert.That(selectList, Is.Not.Null);

            var items = selectList.Items as IEnumerable<string>;
            Assert.That(items, Is.Not.Null);
            Assert.That(items, Is.EquivalentTo(new[] { "Admin", "Receptionist", "Guest" }));
        }

        [Test]
        public async Task Create_Post_ValidModel_CreatesUserAndAddsRole_RedirectsToIndex()
        {
            var model = new CreateUserViewModel
            {
                Email = "newuser@test.com",
                Password = "Pass123!",
                ConfirmPassword = "Pass123!",
                FirstName = "New",
                LastName = "User",
                Age = 28,
                Role = "Guest"
            };

            ApplicationUser? createdUser = null;

            _userManagerMock.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), "Pass123!"))
                .ReturnsAsync(IdentityResult.Success)
                .Callback<ApplicationUser, string>((u, p) => createdUser = u);

            _userManagerMock.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Guest"))
                .ReturnsAsync(IdentityResult.Success);

            SetupControllerAsAdmin();

            var result = await _controller.Create(model) as RedirectToActionResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.ActionName, Is.EqualTo(nameof(AdminUsersController.Index)));

            Assert.That(createdUser, Is.Not.Null);
            Assert.That(createdUser!.Email, Is.EqualTo("newuser@test.com"));
            Assert.That(createdUser.FirstName, Is.EqualTo("New"));
            Assert.That(createdUser.IsActive, Is.True);

            _userManagerMock.Verify(m => m.CreateAsync(It.IsAny<ApplicationUser>(), "Pass123!"), Times.Once);
            _userManagerMock.Verify(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Guest"), Times.Once);
        }

        [Test]
        public async Task Create_Post_UserCreationFails_AddsErrorsAndReturnsView()
        {
            var model = new CreateUserViewModel
            {
                Email = "fail@test.com",
                Password = "weak",
                FirstName = "Fail",
                LastName = "Test",
                Age = 20,
                Role = "Guest"
            };

            _userManagerMock.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), "weak"))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too weak" }));

            SetupControllerAsAdmin();

            var result = await _controller.Create(model) as ViewResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Model, Is.SameAs(model));

            Assert.That(_controller.ModelState[""].Errors.Any(e => e.ErrorMessage.Contains("Password too weak")), Is.True);
        }

        [Test]
        public async Task Create_Post_RoleAdditionFails_AddsErrorsAndReturnsView()
        {
            var model = new CreateUserViewModel
            {
                Email = "role-fail@test.com",
                Password = "Pass123!",
                ConfirmPassword = "Pass123!",
                FirstName = "Role",
                LastName = "Fail",
                Age = 25,
                Role = "Receptionist"
            };

            var user = new ApplicationUser();

            _userManagerMock.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), "Pass123!"))
                .ReturnsAsync(IdentityResult.Success)
                .Callback<ApplicationUser, string>((u, p) => user = u);

            _userManagerMock.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Receptionist"))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Role not found" }));

            SetupControllerAsAdmin();

            var result = await _controller.Create(model) as ViewResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Model, Is.SameAs(model));

            Assert.That(_controller.ModelState[""].Errors.Any(e => e.ErrorMessage.Contains("Role not found")), Is.True);
        }

        [Test]
        public async Task Edit_Get_ValidId_ReturnsEditViewModelWithCurrentRole()
        {
            var user = new ApplicationUser
            {
                Id = "edit1",
                Email = "edit@test.com",
                FirstName = "Edit",
                LastName = "Me",
                Age = 40,
                IsActive = true
            };

            _userManagerMock.Setup(m => m.FindByIdAsync("edit1"))
                .ReturnsAsync(user);

            _userManagerMock.Setup(m => m.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "Receptionist" });

            SetupControllerAsAdmin();

            var result = await _controller.Edit("edit1") as ViewResult;

            Assert.That(result, Is.Not.Null);

            var model = result!.Model as EditUserViewModel;
            Assert.That(model, Is.Not.Null);
            Assert.That(model!.Id, Is.EqualTo("edit1"));
            Assert.That(model.Email, Is.EqualTo("edit@test.com"));
            Assert.That(model.CurrentRole, Is.EqualTo("Receptionist"));
            Assert.That(model.NewRole, Is.Null);
        }

        [Test]
        public async Task Edit_Post_ValidModel_UpdatesUserAndChangesRoleIfSpecified()
        {
            var user = new ApplicationUser
            {
                Id = "edit2",
                Email = "edit2@test.com",
                FirstName = "Old",
                LastName = "Name",
                Age = 33,
                IsActive = true
            };

            _userManagerMock.Setup(m => m.FindByIdAsync("edit2"))
                .ReturnsAsync(user);

            _userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync(IdentityResult.Success);

            _userManagerMock.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync(new List<string> { "Guest" });

            _userManagerMock.Setup(m => m.RemoveFromRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IList<string>>()))
                .ReturnsAsync(IdentityResult.Success);

            _userManagerMock.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Admin"))
                .ReturnsAsync(IdentityResult.Success);

            var model = new EditUserViewModel
            {
                Id = "edit2",
                Email = "edit2@test.com",
                FirstName = "New",
                LastName = "Name",
                Age = 34,
                IsActive = false,
                CurrentRole = "Guest",
                NewRole = "Admin"
            };

            SetupControllerAsAdmin();

            var result = await _controller.Edit("edit2", model) as RedirectToActionResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.ActionName, Is.EqualTo(nameof(AdminUsersController.Index)));

            _userManagerMock.Verify(m => m.UpdateAsync(It.Is<ApplicationUser>(u =>
                u.FirstName == "New" &&
                u.Age == 34 &&
                u.IsActive == false
            )), Times.Once);

            _userManagerMock.Verify(m => m.RemoveFromRolesAsync(It.IsAny<ApplicationUser>(), It.Is<IList<string>>(r => r.Contains("Guest"))), Times.Once);
            _userManagerMock.Verify(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Admin"), Times.Once);
        }

        [Test]
        public async Task Edit_Post_NoNewRole_UpdatesUserWithoutRoleChange()
        {
            var user = new ApplicationUser { Id = "edit-no-role", FirstName = "Old" };

            _userManagerMock.Setup(m => m.FindByIdAsync("edit-no-role"))
                .ReturnsAsync(user);

            _userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync(IdentityResult.Success);

            var model = new EditUserViewModel
            {
                Id = "edit-no-role",
                FirstName = "Updated",
                LastName = "Name",
                Age = 30,
                IsActive = true,
                NewRole = null
            };

            SetupControllerAsAdmin();

            var result = await _controller.Edit("edit-no-role", model) as RedirectToActionResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.ActionName, Is.EqualTo(nameof(AdminUsersController.Index)));

            _userManagerMock.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Once);
            _userManagerMock.Verify(m => m.RemoveFromRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IList<string>>()), Times.Never);
            _userManagerMock.Verify(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task Edit_Post_UpdateFails_AddsErrorsAndReturnsView()
        {
            var user = new ApplicationUser { Id = "edit-fail" };

            _userManagerMock.Setup(m => m.FindByIdAsync("edit-fail"))
                .ReturnsAsync(user);

            _userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Update failed" }));

            var model = new EditUserViewModel
            {
                Id = "edit-fail",
                FirstName = "New",
                LastName = "Name",
                Age = 30,
                IsActive = true
            };

            SetupControllerAsAdmin();

            var result = await _controller.Edit("edit-fail", model) as ViewResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Model, Is.SameAs(model));

            Assert.That(_controller.ModelState[""].Errors.Any(e => e.ErrorMessage.Contains("Update failed")), Is.True);
        }

        [Test]
        public async Task Edit_Post_RoleAdditionFails_AddsErrorsAndReturnsView()
        {
            var user = new ApplicationUser { Id = "edit-role-fail" };

            _userManagerMock.Setup(m => m.FindByIdAsync("edit-role-fail"))
                .ReturnsAsync(user);

            _userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync(IdentityResult.Success);

            _userManagerMock.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync(new List<string> { "Guest" });

            _userManagerMock.Setup(m => m.RemoveFromRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IList<string>>()))
                .ReturnsAsync(IdentityResult.Success);

            _userManagerMock.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Admin"))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Role addition failed" }));

            var model = new EditUserViewModel
            {
                Id = "edit-role-fail",
                FirstName = "New",
                LastName = "Name",
                Age = 30,
                IsActive = true,
                CurrentRole = "Guest",
                NewRole = "Admin"
            };

            SetupControllerAsAdmin();

            var result = await _controller.Edit("edit-role-fail", model) as ViewResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Model, Is.SameAs(model));

            Assert.That(_controller.ModelState[""].Errors.Any(e => e.ErrorMessage.Contains("Role addition failed")), Is.True);
        }

        [Test]
        public async Task Edit_Post_IdMismatch_ReturnsNotFound()
        {
            var model = new EditUserViewModel { Id = "wrong-id" };

            SetupControllerAsAdmin();

            var result = await _controller.Edit("correct-id", model) as NotFoundResult;

            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public async Task Delete_Get_ValidId_ReturnsDeleteViewModel()
        {
            var user = new ApplicationUser
            {
                Id = "del1",
                Email = "del@test.com",
                FirstName = "Delete",
                LastName = "Me"
            };

            _userManagerMock.Setup(m => m.FindByIdAsync("del1"))
                .ReturnsAsync(user);

            _userManagerMock.Setup(m => m.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "Guest" });

            SetupControllerAsAdmin();

            var result = await _controller.Delete("del1") as ViewResult;

            Assert.That(result, Is.Not.Null);

            var model = result!.Model as DeleteUserViewModel;
            Assert.That(model, Is.Not.Null);
            Assert.That(model!.Id, Is.EqualTo("del1"));
            Assert.That(model.Email, Is.EqualTo("del@test.com"));
            Assert.That(model.Roles, Is.EqualTo("Guest"));
        }

        [Test]
        public async Task DeleteConfirmed_Post_ValidId_DeletesUserAndRedirects()
        {
            var user = new ApplicationUser { Id = "del2" };

            _userManagerMock.Setup(m => m.FindByIdAsync("del2"))
                .ReturnsAsync(user);

            _userManagerMock.Setup(m => m.DeleteAsync(user))
                .ReturnsAsync(IdentityResult.Success);

            SetupControllerAsAdmin();

            var result = await _controller.DeleteConfirmed("del2") as RedirectToActionResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.ActionName, Is.EqualTo(nameof(AdminUsersController.Index)));

            _userManagerMock.Verify(m => m.DeleteAsync(user), Times.Once);
        }

        [Test]
        public async Task DeleteConfirmed_Post_UserNotFound_RedirectsWithoutDelete()
        {
            _userManagerMock.Setup(m => m.FindByIdAsync("missing"))
                .ReturnsAsync((ApplicationUser?)null);

            SetupControllerAsAdmin();

            var result = await _controller.DeleteConfirmed("missing") as RedirectToActionResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.ActionName, Is.EqualTo(nameof(AdminUsersController.Index)));

            _userManagerMock.Verify(m => m.DeleteAsync(It.IsAny<ApplicationUser>()), Times.Never);
        }
    }
}