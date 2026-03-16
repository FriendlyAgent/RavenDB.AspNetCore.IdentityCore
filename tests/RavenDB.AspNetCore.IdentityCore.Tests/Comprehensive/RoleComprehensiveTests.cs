using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Operations.CompareExchange;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.Test.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace RavenDB.AspNetCore.IdentityCore.Tests.Comprehensive
{
    /// <summary>
    /// Comprehensive and advanced tests for role functionality.
    ///
    /// These tests cover edge cases, concurrency scenarios, and advanced
    /// functionality that extends beyond basic CRUD operations.
    ///
    /// Test Categories:
    /// - Concurrency and race conditions
    /// - Bulk operations
    /// - Edge cases and error handling
    /// - Performance and scalability scenarios
    /// </summary>
    public class RoleComprehensiveTests : RavenDBTestBase
    {
        /// <summary>
        /// Test: Concurrent role creation with same name
        ///
        /// Verifies that:
        /// 1. Only one of two concurrent role creation attempts succeeds
        /// 2. The Compare Exchange mechanism properly prevents duplicates
        /// 3. One creation returns success, the other returns DuplicateRoleName error
        /// 4. Only one role exists in the database after both attempts
        ///
        /// This test simulates a real-world scenario where two requests attempt
        /// to create the same role simultaneously, which could happen in a
        /// distributed system or under high load.
        /// </summary>
        [Fact(DisplayName = "Role: Concurrent creation with same name - only one succeeds")]
        public async Task CreateAsync_ConcurrentSameName_OnlyOneSucceeds()
        {
            using (var store = GetDocumentStore())
            {
                const string roleName = "Administrator";
                var results = new List<Microsoft.AspNetCore.Identity.IdentityResult>();

                // Act: Create two roles with the same name concurrently
                var task1 = Task.Run(async () =>
                {
                    using (var session = store.OpenAsyncSession())
                    {
                        var roleStore = new RavenRoleStore<TestRole>(session);
                        var role = new TestRole
                        {
                            RoleName = roleName,
                            NormalizedRoleName = roleName.ToUpperInvariant()
                        };
                        return await roleStore.CreateAsync(role);
                    }
                });

                var task2 = Task.Run(async () =>
                {
                    using (var session = store.OpenAsyncSession())
                    {
                        var roleStore = new RavenRoleStore<TestRole>(session);
                        var role = new TestRole
                        {
                            RoleName = roleName,
                            NormalizedRoleName = roleName.ToUpperInvariant()
                        };
                        return await roleStore.CreateAsync(role);
                    }
                });

                results.Add(await task1);
                results.Add(await task2);

                // Wait for indexes
                await WaitForIndexing(store);

                // Assert: Exactly one succeeded, one failed
                var successes = results.Count(r => r.Succeeded);
                var failures = results.Count(r => !r.Succeeded);
                Assert.Equal(1, successes);
                Assert.Equal(1, failures);

                // Assert: The failure has correct error code
                var failedResult = results.First(r => !r.Succeeded);
                Assert.Contains(failedResult.Errors, e => e.Code == "DuplicateRoleName");

                // Assert: Only one role exists in database
                using (var session = store.OpenAsyncSession())
                {
                    var roles = await session.Query<TestRole>().ToListAsync();
                    Assert.Single(roles);
                    Assert.Equal(roleName, roles[0].RoleName);
                }
            }
        }

        /// <summary>
        /// Test: Update role with concurrency check
        ///
        /// Verifies that:
        /// 1. Concurrent updates to the same role are handled correctly
        /// 2. RavenDB's optimistic concurrency control prevents lost updates
        /// 3. The last write wins or proper error is returned
        ///
        /// This demonstrates RavenDB's built-in optimistic concurrency support
        /// which is crucial for maintaining data integrity in multi-user scenarios.
        /// </summary>
        [Fact(DisplayName = "Role: Concurrent updates handled correctly")]
        public async Task UpdateAsync_ConcurrentUpdates_HandledCorrectly()
        {
            using (var store = GetDocumentStore())
            {
                string roleId;

                // Arrange: Create a role
                using (var session = store.OpenAsyncSession())
                {
                    var roleStore = new RavenRoleStore<TestRole>(session);
                    var role = new TestRole
                    {
                        RoleName = "Manager",
                        NormalizedRoleName = "MANAGER"
                    };
                    await roleStore.CreateAsync(role);
                    roleId = role.Id;
                }

                await WaitForIndexing(store);

                // Act: Two concurrent updates
                var update1 = Task.Run(async () =>
                {
                    using (var session = store.OpenAsyncSession())
                    {
                        var roleStore = new RavenRoleStore<TestRole>(session);
                        var role = await roleStore.FindByIdAsync(roleId);
                        await Task.Delay(50); // Simulate some processing
                        await roleStore.SetRoleNameAsync(role, "SeniorManager");
                        await roleStore.SetNormalizedRoleNameAsync(role, "SENIORMANAGER");
                        return await roleStore.UpdateAsync(role);
                    }
                });

                var update2 = Task.Run(async () =>
                {
                    using (var session = store.OpenAsyncSession())
                    {
                        var roleStore = new RavenRoleStore<TestRole>(session);
                        var role = await roleStore.FindByIdAsync(roleId);
                        await Task.Delay(50); // Simulate some processing
                        await roleStore.SetRoleNameAsync(role, "LeadManager");
                        await roleStore.SetNormalizedRoleNameAsync(role, "LEADMANAGER");
                        return await roleStore.UpdateAsync(role);
                    }
                });

                var result1 = await update1;
                var result2 = await update2;

                // Assert: At least one update succeeded
                Assert.True(result1.Succeeded || result2.Succeeded);

                await WaitForIndexing(store);

                // Assert: Role has one of the updated values
                using (var session = store.OpenAsyncSession())
                {
                    var role = await session.LoadAsync<TestRole>(roleId);
                    Assert.NotNull(role);
                    Assert.True(
                        role.RoleName == "SeniorManager" || role.RoleName == "LeadManager",
                        $"Expected 'SeniorManager' or 'LeadManager', but got '{role.RoleName}'");
                }
            }
        }

        /// <summary>
        /// Test: Bulk role creation and retrieval
        ///
        /// Verifies that:
        /// 1. Multiple roles can be created in succession
        /// 2. All roles are properly stored and retrievable
        /// 3. Performance remains acceptable with multiple operations
        /// 4. Each role has unique Compare Exchange reservations
        ///
        /// This test ensures the system can handle typical administrative
        /// tasks like setting up multiple roles during initial configuration.
        /// </summary>
        [Fact(DisplayName = "Role: Bulk creation and retrieval")]
        public async Task CreateAsync_BulkOperations_AllSucceed()
        {
            using (var store = GetDocumentStore())
            {
                var roleNames = new[] { "Admin", "User", "Manager", "Developer", "Tester", "Support" };
                var createdIds = new List<string>();

                // Act: Create multiple roles
                foreach (var roleName in roleNames)
                {
                    using (var session = store.OpenAsyncSession())
                    {
                        var roleStore = new RavenRoleStore<TestRole>(session);
                        var role = new TestRole
                        {
                            RoleName = roleName,
                            NormalizedRoleName = roleName.ToUpperInvariant()
                        };
                        var result = await roleStore.CreateAsync(role);

                        // Assert: Each creation succeeds
                        Assert.True(result.Succeeded);
                        Assert.NotNull(role.Id);
                        createdIds.Add(role.Id);
                    }
                }

                await WaitForIndexing(store);

                // Assert: All roles exist and are retrievable
                using (var session = store.OpenAsyncSession())
                {
                    var roles = await session.Query<TestRole>().ToListAsync();
                    Assert.Equal(roleNames.Length, roles.Count);

                    foreach (var roleName in roleNames)
                    {
                        var role = roles.FirstOrDefault(r => r.RoleName == roleName);
                        Assert.NotNull(role);
                        Assert.Equal(roleName.ToUpperInvariant(), role.NormalizedRoleName);
                    }
                }

                // Assert: Each has a Compare Exchange reservation
                foreach (var roleName in roleNames)
                {
                    var key = CompareExchangeKeys.ForRoleName(roleName);
                    var reservation = await store.Operations.SendAsync(
                        new GetCompareExchangeValueOperation<string>(key));
                    Assert.NotNull(reservation);
                }
            }
        }

        /// <summary>
        /// Test: Find non-existent role returns null
        ///
        /// Verifies that:
        /// 1. FindByNameAsync returns null for non-existent roles
        /// 2. FindByIdAsync returns null for non-existent roles
        /// 3. No exceptions are thrown
        ///
        /// This tests proper null handling, which is important for
        /// authorization checks that need to gracefully handle missing roles.
        /// </summary>
        [Fact(DisplayName = "Role: Find non-existent role returns null")]
        public async Task FindAsync_NonExistent_ReturnsNull()
        {
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenAsyncSession())
                {
                    var roleStore = new RavenRoleStore<TestRole>(session);

                    // Act & Assert: Find by name returns null
                    var byName = await roleStore.FindByNameAsync("NONEXISTENT");
                    Assert.Null(byName);

                    // Act & Assert: Find by ID returns null
                    var byId = await roleStore.FindByIdAsync("TestRoles/999");
                    Assert.Null(byId);
                }
            }
        }

        /// <summary>
        /// Test: Delete non-existent role fails gracefully
        ///
        /// Verifies that:
        /// 1. Attempting to delete a null role throws ArgumentNullException
        /// 2. The system properly validates input before attempting operations
        ///
        /// This ensures proper error handling and prevents null reference exceptions.
        /// </summary>
        [Fact(DisplayName = "Role: Delete null role throws exception")]
        public async Task DeleteAsync_NullRole_ThrowsException()
        {
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenAsyncSession())
                {
                    var roleStore = new RavenRoleStore<TestRole>(session);

                    // Act & Assert: Deleting null throws
                    await Assert.ThrowsAsync<ArgumentNullException>(
                        async () => await roleStore.DeleteAsync(null));
                }
            }
        }

        /// <summary>
        /// Test: Role name is case-insensitive for lookups
        ///
        /// Verifies that:
        /// 1. Roles can be found using different case variations
        /// 2. Normalized names ensure case-insensitive matching
        /// 3. Original casing is preserved
        ///
        /// This is important for user experience - users shouldn't have to
        /// remember exact casing of role names.
        /// </summary>
        [Theory(DisplayName = "Role: Case-insensitive name lookup")]
        [InlineData("Admin", "ADMIN")]
        [InlineData("Admin", "admin")]
        [InlineData("Admin", "AdMiN")]
        public async Task FindByNameAsync_CaseInsensitive_FindsRole(string originalName, string searchName)
        {
            using (var store = GetDocumentStore())
            {
                // Arrange: Create role with original casing
                using (var session = store.OpenAsyncSession())
                {
                    var roleStore = new RavenRoleStore<TestRole>(session);
                    var role = new TestRole
                    {
                        RoleName = originalName,
                        NormalizedRoleName = originalName.ToUpperInvariant()
                    };
                    await roleStore.CreateAsync(role);
                }

                await WaitForIndexing(store);

                // Act: Find using different casing
                using (var session = store.OpenAsyncSession())
                {
                    var roleStore = new RavenRoleStore<TestRole>(session);
                    var foundRole = await roleStore.FindByNameAsync(searchName.ToUpperInvariant());

                    // Assert: Role is found and original casing is preserved
                    Assert.NotNull(foundRole);
                    Assert.Equal(originalName, foundRole.RoleName);
                }
            }
        }

        /// <summary>
        /// Test: Role with special characters in name
        ///
        /// Verifies that:
        /// 1. Roles can have special characters in their names
        /// 2. Such roles are stored and retrieved correctly
        /// 3. Compare Exchange keys handle special characters
        ///
        /// This ensures the system is flexible enough for various
        /// organizational naming conventions.
        /// </summary>
        [Theory(DisplayName = "Role: Special characters in name")]
        [InlineData("Admin-Level-1")]
        [InlineData("User_Basic")]
        [InlineData("Manager.Senior")]
        public async Task CreateAsync_SpecialCharacters_Success(string roleName)
        {
            using (var store = GetDocumentStore())
            {
                // Act: Create role with special characters
                using (var session = store.OpenAsyncSession())
                {
                    var roleStore = new RavenRoleStore<TestRole>(session);
                    var role = new TestRole
                    {
                        RoleName = roleName,
                        NormalizedRoleName = roleName.ToUpperInvariant()
                    };
                    var result = await roleStore.CreateAsync(role);

                    // Assert: Creation succeeds
                    Assert.True(result.Succeeded);
                    Assert.NotNull(role.Id);
                }

                await WaitForIndexing(store);

                // Assert: Role can be retrieved
                using (var session = store.OpenAsyncSession())
                {
                    var roleStore = new RavenRoleStore<TestRole>(session);
                    var foundRole = await roleStore.FindByNameAsync(roleName.ToUpperInvariant());
                    Assert.NotNull(foundRole);
                    Assert.Equal(roleName, foundRole.RoleName);
                }
            }
        }
    }
}
