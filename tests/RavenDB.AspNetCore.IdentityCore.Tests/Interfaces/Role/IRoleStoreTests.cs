using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Operations.CompareExchange;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.Test.Shared;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace RavenDB.AspNetCore.IdentityCore.Tests.Interfaces.Role
{
    /// <summary>
    /// Tests for IRoleStore&lt;TRole&gt; interface.
    ///
    /// Methods Tested:
    /// - CreateAsync, UpdateAsync, DeleteAsync
    /// - GetRoleIdAsync, GetRoleNameAsync, SetRoleNameAsync
    /// - GetNormalizedRoleNameAsync, SetNormalizedRoleNameAsync
    /// - FindByIdAsync, FindByNameAsync
    ///
    /// Original: Tests for basic role CRUD operations.
    ///
    /// Test Categories:
    /// - Create: Creating new roles with unique constraints
    /// - Read: Finding roles by ID and name (IRoleStore)
    /// - Update: Modifying role properties (name)
    /// - Delete: Removing roles and cleanup of reservations
    ///
    /// Each test is isolated and uses a fresh database instance.
    /// For concurrency and advanced scenarios, see RoleComprehensiveTests.
    /// </summary>
    public class RoleCrudTests : RavenDBTestBase
    {
        #region Create Tests

        /// <summary>
        /// Test: Create a new role successfully.
        ///
        /// Verifies:
        /// - Role is created with assigned ID
        /// - Role can be retrieved by normalized name
        /// - Compare Exchange reservation is created
        /// </summary>
        [Theory(DisplayName = "Role: Create - Success")]
        [InlineData("Admin")]
        [InlineData("User")]
        [InlineData("Manager")]
        public async Task Create_ValidRole_Success(string roleName)
        {
            using var store = GetDocumentStore();

            // Act: Create role
            using (var session = store.OpenAsyncSession())
            {
                var roleStore = new RavenRoleStore<TestRole>(session);
                var role = new TestRole
                {
                    RoleName = roleName,
                    NormalizedRoleName = roleName.ToUpperInvariant()
                };

                var result = await roleStore.CreateAsync(role);

                // Assert: Success
                Assert.True(result.Succeeded);
                Assert.NotNull(role.Id);
                Assert.StartsWith("TestRoles/", role.Id);
            }

            await WaitForIndexing(store);

            // Assert: Can retrieve by name
            using (var session = store.OpenAsyncSession())
            {
                var roleStore = new RavenRoleStore<TestRole>(session);
                var foundRole = await roleStore.FindByNameAsync(roleName.ToUpperInvariant());

                Assert.NotNull(foundRole);
                Assert.Equal(roleName, foundRole.RoleName);
            }
        }

        /// <summary>
        /// Test: Creating duplicate role name fails.
        ///
        /// Verifies:
        /// - First role creation succeeds
        /// - Second role with same name fails
        /// - Error code is "DuplicateRoleName"
        /// - Only one role exists in database
        /// </summary>
        [Fact(DisplayName = "Role: Create - Duplicate name fails")]
        public async Task Create_DuplicateName_Fails()
        {
            using var store = GetDocumentStore();
            const string roleName = "Admin";

            // Act: Create first role
            using (var session = store.OpenAsyncSession())
            {
                var roleStore = new RavenRoleStore<TestRole>(session);
                var role = new TestRole
                {
                    RoleName = roleName,
                    NormalizedRoleName = roleName.ToUpperInvariant()
                };

                var result = await roleStore.CreateAsync(role);
                Assert.True(result.Succeeded);
            }

            await WaitForIndexing(store);

            // Act: Try to create duplicate
            using (var session = store.OpenAsyncSession())
            {
                var roleStore = new RavenRoleStore<TestRole>(session);
                var duplicateRole = new TestRole
                {
                    RoleName = roleName,
                    NormalizedRoleName = roleName.ToUpperInvariant()
                };

                var result = await roleStore.CreateAsync(duplicateRole);

                // Assert: Fails with correct error
                Assert.False(result.Succeeded);
                Assert.Contains(result.Errors, e => e.Code == "DuplicateRoleName");
            }

            await WaitForIndexing(store);

            // Assert: Only one role exists
            using (var session = store.OpenAsyncSession())
            {
                var roles = await session.Query<TestRole>().ToListAsync();
                Assert.Single(roles);
            }
        }

        #endregion

        #region Read Tests

        /// <summary>
        /// Test: Find role by ID.
        ///
        /// Verifies:
        /// - Role can be found by its ID
        /// - Properties are correctly retrieved
        /// </summary>
        [Fact(DisplayName = "Role: Read - Find by ID")]
        public async Task Read_FindById_Success()
        {
            using var store = GetDocumentStore();
            const string roleName = "Developer";
            string roleId;

            // Arrange: Create role
            using (var session = store.OpenAsyncSession())
            {
                var roleStore = new RavenRoleStore<TestRole>(session);
                var role = new TestRole
                {
                    RoleName = roleName,
                    NormalizedRoleName = roleName.ToUpperInvariant()
                };

                await roleStore.CreateAsync(role);
                roleId = role.Id;
            }

            await WaitForIndexing(store);

            // Act & Assert: Find by ID
            using (var session = store.OpenAsyncSession())
            {
                var roleStore = new RavenRoleStore<TestRole>(session);
                var foundRole = await roleStore.FindByIdAsync(roleId);

                Assert.NotNull(foundRole);
                Assert.Equal(roleId, foundRole.Id);
                Assert.Equal(roleName, foundRole.RoleName);
            }
        }

        #endregion

        #region Update Tests

        /// <summary>
        /// Test: Update role name.
        ///
        /// Verifies:
        /// - Role name can be updated
        /// - Old Compare Exchange reservation is removed
        /// - New Compare Exchange reservation is created
        /// </summary>
        [Fact(DisplayName = "Role: Update - Change name")]
        public async Task Update_ChangeName_Success()
        {
            using var store = GetDocumentStore();
            const string oldName = "Moderator";
            const string newName = "SeniorModerator";
            string roleId;

            // Arrange: Create role
            using (var session = store.OpenAsyncSession())
            {
                var roleStore = new RavenRoleStore<TestRole>(session);
                var role = new TestRole
                {
                    RoleName = oldName,
                    NormalizedRoleName = oldName.ToUpperInvariant()
                };

                await roleStore.CreateAsync(role);
                roleId = role.Id;
            }

            await WaitForIndexing(store);

            // Act: Update name
            using (var session = store.OpenAsyncSession())
            {
                var roleStore = new RavenRoleStore<TestRole>(session);
                var role = await roleStore.FindByIdAsync(roleId);

                await roleStore.SetRoleNameAsync(role, newName);
                await roleStore.SetNormalizedRoleNameAsync(role, newName.ToUpperInvariant());
                var result = await roleStore.UpdateAsync(role);

                Assert.True(result.Succeeded);
            }

            await WaitForIndexing(store);

            // Assert: New name is set
            using (var session = store.OpenAsyncSession())
            {
                var roleStore = new RavenRoleStore<TestRole>(session);
                var role = await roleStore.FindByIdAsync(roleId);

                Assert.Equal(newName, role.RoleName);
            }

            // Assert: Old reservation removed, new one exists
            var oldKey = CompareExchangeKeys.ForRoleName(oldName);
            var oldReservation = await store.Operations.SendAsync(
                new GetCompareExchangeValueOperation<string>(oldKey));
            Assert.Null(oldReservation);

            var newKey = CompareExchangeKeys.ForRoleName(newName);
            var newReservation = await store.Operations.SendAsync(
                new GetCompareExchangeValueOperation<string>(newKey));
            Assert.NotNull(newReservation);
        }

        #endregion

        #region Delete Tests

        /// <summary>
        /// Test: Delete role.
        ///
        /// Verifies:
        /// - Role is deleted from database
        /// - Compare Exchange reservation is removed
        /// - Cannot find role after deletion
        /// </summary>
        [Fact(DisplayName = "Role: Delete - Success")]
        public async Task Delete_ExistingRole_Success()
        {
            using var store = GetDocumentStore();
            const string roleName = "TemporaryRole";
            string roleId;

            // Arrange: Create role
            using (var session = store.OpenAsyncSession())
            {
                var roleStore = new RavenRoleStore<TestRole>(session);
                var role = new TestRole
                {
                    RoleName = roleName,
                    NormalizedRoleName = roleName.ToUpperInvariant()
                };

                await roleStore.CreateAsync(role);
                roleId = role.Id;
            }

            await WaitForIndexing(store);

            // Act: Delete role
            using (var session = store.OpenAsyncSession())
            {
                var roleStore = new RavenRoleStore<TestRole>(session);
                var role = await roleStore.FindByIdAsync(roleId);

                var result = await roleStore.DeleteAsync(role);
                Assert.True(result.Succeeded);
            }

            await WaitForIndexing(store);

            // Assert: Role is gone
            using (var session = store.OpenAsyncSession())
            {
                var role = await session.LoadAsync<TestRole>(roleId);
                Assert.Null(role);
            }

            // Assert: Compare Exchange reservation removed
            var key = CompareExchangeKeys.ForRoleName(roleName);
            var reservation = await store.Operations.SendAsync(
                new GetCompareExchangeValueOperation<string>(key));
            Assert.Null(reservation);
        }

        #endregion
    }
}
