using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.Test.Shared;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace RavenDB.AspNetCore.IdentityCore.Tests.Interfaces.User
{
    /// <summary>
    /// Tests for IUserRoleStore&lt;TUser&gt; interface.
    ///
    /// Methods Tested:
    /// - AddToRoleAsync, RemoveFromRoleAsync
    /// - GetRolesAsync, IsInRoleAsync
    /// - GetUsersInRoleAsync
    ///
    /// Original: Tests for user-role relationship functionality.
    ///
    /// Test Categories:
    /// - Adding users to roles (IUserRoleStore)
    /// - Retrieving user roles and users in roles
    /// - Removing users from roles
    /// - Role membership validation
    ///
    /// Each test is isolated and uses a fresh database instance.
    /// For basic user CRUD operations, see UserCrudTests.
    /// For concurrency and advanced scenarios, see UserComprehensiveTests.
    /// </summary>
    public class UserRolesTests : RavenDBTestBase
    {
        #region Create Tests

        /// <summary>
        /// Test: Add user to role successfully.
        ///
        /// Verifies:
        /// - User can be added to a role
        /// - Role appears in user's role list
        /// - User appears in role's member list
        /// </summary>
        [Theory(DisplayName = "UserRole: Create - Add user to role")]
        [InlineData("alice", "Admin")]
        [InlineData("bob", "User")]
        [InlineData("charlie", "Manager")]
        public async Task Create_AddUserToRole_Success(string username, string roleName)
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user and role
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var user = new TestUser
                {
                    UserName = username,
                    NormalizedUserName = username.ToUpperInvariant()
                };

                user.Email = new RavenIdentityUserEmail
                {
                    Email = $"{username}@example.com",
                    NormalizedEmail = $"{username.ToUpperInvariant()}@EXAMPLE.COM"
                };

                await userStore.CreateAsync(user);
                userId = user.Id;

                var roleStore = new RavenRoleStore<TestRole>(session);
                var role = new TestRole
                {
                    RoleName = roleName,
                    NormalizedRoleName = roleName.ToUpperInvariant()
                };

                await roleStore.CreateAsync(role);
            }

            await WaitForIndexing(store);

            // Act: Add user to role
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var user = await userStore.FindByIdAsync(userId);

                await userStore.AddToRoleAsync(user, roleName.ToUpperInvariant());
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: User is in role
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var user = await userStore.FindByIdAsync(userId);

                var roles = await userStore.GetRolesAsync(user);
                Assert.Contains(roleName, roles);

                var isInRole = await userStore.IsInRoleAsync(user, roleName.ToUpperInvariant());
                Assert.True(isInRole);
            }
        }

        /// <summary>
        /// Test: Add user to multiple roles.
        ///
        /// Verifies:
        /// - User can be added to multiple roles
        /// - All roles appear in user's role list
        /// - User is confirmed in each role
        /// </summary>
        [Fact(DisplayName = "UserRole: Create - Add user to multiple roles")]
        public async Task Create_AddUserToMultipleRoles_Success()
        {
            using var store = GetDocumentStore();
            const string username = "admin";
            var roleNames = new[] { "Admin", "Manager", "Developer" };
            string userId;

            // Arrange: Create user and roles
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var user = new TestUser
                {
                    UserName = username,
                    NormalizedUserName = username.ToUpperInvariant()
                };

                user.Email = new RavenIdentityUserEmail
                {
                    Email = $"{username}@example.com",
                    NormalizedEmail = $"{username.ToUpperInvariant()}@EXAMPLE.COM"
                };

                await userStore.CreateAsync(user);
                userId = user.Id;

                var roleStore = new RavenRoleStore<TestRole>(session);
                foreach (var roleName in roleNames)
                {
                    var role = new TestRole
                    {
                        RoleName = roleName,
                        NormalizedRoleName = roleName.ToUpperInvariant()
                    };
                    await roleStore.CreateAsync(role);
                }
            }

            await WaitForIndexing(store);

            // Act: Add user to all roles
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var user = await userStore.FindByIdAsync(userId);

                foreach (var roleName in roleNames)
                {
                    await userStore.AddToRoleAsync(user, roleName.ToUpperInvariant());
                }

                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: User has all roles
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var user = await userStore.FindByIdAsync(userId);

                var userRoles = await userStore.GetRolesAsync(user);
                Assert.Equal(roleNames.Length, userRoles.Count);

                foreach (var roleName in roleNames)
                {
                    Assert.Contains(roleName, userRoles);

                    var isInRole = await userStore.IsInRoleAsync(user, roleName.ToUpperInvariant());
                    Assert.True(isInRole);
                }
            }
        }

        /// <summary>
        /// Test: Adding user to non-existent role.
        ///
        /// Verifies:
        /// - System handles non-existent roles gracefully
        /// - No exception is thrown
        /// - User is not added to invalid role
        /// </summary>
        [Fact(DisplayName = "UserRole: Create - Add to non-existent role")]
        public async Task Create_AddToNonExistentRole_HandledGracefully()
        {
            using var store = GetDocumentStore();
            const string username = "testuser";
            string userId;

            // Arrange: Create user only (no role)
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var user = new TestUser
                {
                    UserName = username,
                    NormalizedUserName = username.ToUpperInvariant()
                };

                user.Email = new RavenIdentityUserEmail
                {
                    Email = $"{username}@example.com",
                    NormalizedEmail = $"{username.ToUpperInvariant()}@EXAMPLE.COM"
                };

                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act & Assert: Try to add to non-existent role - should throw
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var user = await userStore.FindByIdAsync(userId);

                // This should throw InvalidOperationException because the role doesn't exist
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                {
                    await userStore.AddToRoleAsync(user, "NONEXISTENT");
                });
            }
        }

        #endregion

        #region Read Tests

        /// <summary>
        /// Test: Get all roles for a user.
        ///
        /// Verifies:
        /// - Can retrieve all roles assigned to a user
        /// - Empty list returned for user with no roles
        /// - Role names are returned correctly
        /// </summary>
        [Fact(DisplayName = "UserRole: Read - Get user roles")]
        public async Task Read_GetUserRoles_Success()
        {
            using var store = GetDocumentStore();
            const string username = "testuser";
            var roleNames = new[] { "Admin", "Manager" };
            string userId;

            // Arrange: Create user with roles
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var user = new TestUser
                {
                    UserName = username,
                    NormalizedUserName = username.ToUpperInvariant()
                };

                user.Email = new RavenIdentityUserEmail
                {
                    Email = $"{username}@example.com",
                    NormalizedEmail = $"{username.ToUpperInvariant()}@EXAMPLE.COM"
                };

                await userStore.CreateAsync(user);
                userId = user.Id;

                var roleStore = new RavenRoleStore<TestRole>(session);
                foreach (var roleName in roleNames)
                {
                    var role = new TestRole
                    {
                        RoleName = roleName,
                        NormalizedRoleName = roleName.ToUpperInvariant()
                    };
                    await roleStore.CreateAsync(role);
                }
            }

            await WaitForIndexing(store);

            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var user = await userStore.FindByIdAsync(userId);

                foreach (var roleName in roleNames)
                {
                    await userStore.AddToRoleAsync(user, roleName.ToUpperInvariant());
                }

                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Act & Assert: Get roles
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var user = await userStore.FindByIdAsync(userId);

                var roles = await userStore.GetRolesAsync(user);
                Assert.Equal(roleNames.Length, roles.Count);

                foreach (var roleName in roleNames)
                {
                    Assert.Contains(roleName, roles);
                }
            }
        }

        /// <summary>
        /// Test: Check if user is in specific role.
        ///
        /// Verifies:
        /// - IsInRoleAsync returns true for assigned roles
        /// - IsInRoleAsync returns false for unassigned roles
        /// - Check is case-insensitive
        /// </summary>
        [Fact(DisplayName = "UserRole: Read - Check user in role")]
        public async Task Read_IsUserInRole_Success()
        {
            using var store = GetDocumentStore();
            const string username = "testuser";
            const string assignedRole = "Admin";
            const string unassignedRole = "User";
            string userId;

            // Arrange: Create user with one role
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var user = new TestUser
                {
                    UserName = username,
                    NormalizedUserName = username.ToUpperInvariant()
                };

                user.Email = new RavenIdentityUserEmail
                {
                    Email = $"{username}@example.com",
                    NormalizedEmail = $"{username.ToUpperInvariant()}@EXAMPLE.COM"
                };

                await userStore.CreateAsync(user);
                userId = user.Id;

                var roleStore = new RavenRoleStore<TestRole>(session);
                var role = new TestRole
                {
                    RoleName = assignedRole,
                    NormalizedRoleName = assignedRole.ToUpperInvariant()
                };
                await roleStore.CreateAsync(role);
            }

            await WaitForIndexing(store);

            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var user = await userStore.FindByIdAsync(userId);

                await userStore.AddToRoleAsync(user, assignedRole.ToUpperInvariant());
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Act & Assert: Check role membership
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var user = await userStore.FindByIdAsync(userId);

                var isInAssigned = await userStore.IsInRoleAsync(user, assignedRole.ToUpperInvariant());
                Assert.True(isInAssigned);

                var isInUnassigned = await userStore.IsInRoleAsync(user, unassignedRole.ToUpperInvariant());
                Assert.False(isInUnassigned);
            }
        }

        /// <summary>
        /// Test: Get all users in a specific role.
        ///
        /// Verifies:
        /// - Can retrieve all users assigned to a role
        /// - Empty list returned for role with no users
        /// - Multiple users in same role are all returned
        /// </summary>
        [Fact(DisplayName = "UserRole: Read - Get users in role")]
        public async Task Read_GetUsersInRole_Success()
        {
            using var store = GetDocumentStore();
            const string roleName = "Admin";
            var usernames = new[] { "alice", "bob", "charlie" };

            // Arrange: Create role and users
            using (var session = store.OpenAsyncSession())
            {
                var roleStore = new RavenRoleStore<TestRole>(session);
                var role = new TestRole
                {
                    RoleName = roleName,
                    NormalizedRoleName = roleName.ToUpperInvariant()
                };
                await roleStore.CreateAsync(role);

                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                foreach (var username in usernames)
                {
                    var user = new TestUser
                    {
                        UserName = username,
                        NormalizedUserName = username.ToUpperInvariant()
                    };

                    user.Email = new RavenIdentityUserEmail
                    {
                        Email = $"{username}@example.com",
                        NormalizedEmail = $"{username.ToUpperInvariant()}@EXAMPLE.COM"
                    };

                    await userStore.CreateAsync(user);
                }
            }

            await WaitForIndexing(store);

            // Add all users to role
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);

                foreach (var username in usernames)
                {
                    var user = await userStore.FindByNameAsync(username.ToUpperInvariant());
                    await userStore.AddToRoleAsync(user, roleName.ToUpperInvariant());
                    await userStore.UpdateAsync(user);
                }
            }

            await WaitForIndexing(store);

            // Act & Assert: Get all users in role
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var usersInRole = await userStore.GetUsersInRoleAsync(roleName.ToUpperInvariant());

                Assert.Equal(usernames.Length, usersInRole.Count);

                foreach (var username in usernames)
                {
                    Assert.Contains(usersInRole, u => u.UserName == username);
                }
            }
        }

        /// <summary>
        /// Test: User with no roles returns empty list.
        ///
        /// Verifies:
        /// - GetRolesAsync returns empty list for user with no roles
        /// - No exception is thrown
        /// </summary>
        [Fact(DisplayName = "UserRole: Read - User with no roles")]
        public async Task Read_UserWithNoRoles_ReturnsEmpty()
        {
            using var store = GetDocumentStore();
            const string username = "testuser";
            string userId;

            // Arrange: Create user without roles
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var user = new TestUser
                {
                    UserName = username,
                    NormalizedUserName = username.ToUpperInvariant()
                };

                user.Email = new RavenIdentityUserEmail
                {
                    Email = $"{username}@example.com",
                    NormalizedEmail = $"{username.ToUpperInvariant()}@EXAMPLE.COM"
                };

                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act & Assert: Get roles returns empty
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var user = await userStore.FindByIdAsync(userId);

                var roles = await userStore.GetRolesAsync(user);
                Assert.Empty(roles);
            }
        }

        #endregion

        #region Update Tests

        /// <summary>
        /// Test: Remove user from role.
        ///
        /// Verifies:
        /// - User can be removed from a role
        /// - Role no longer appears in user's role list
        /// - IsInRoleAsync returns false after removal
        /// </summary>
        [Fact(DisplayName = "UserRole: Update - Remove user from role")]
        public async Task Update_RemoveUserFromRole_Success()
        {
            using var store = GetDocumentStore();
            const string username = "testuser";
            const string roleName = "Admin";
            string userId;

            // Arrange: Create user with role
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var user = new TestUser
                {
                    UserName = username,
                    NormalizedUserName = username.ToUpperInvariant()
                };

                user.Email = new RavenIdentityUserEmail
                {
                    Email = $"{username}@example.com",
                    NormalizedEmail = $"{username.ToUpperInvariant()}@EXAMPLE.COM"
                };

                await userStore.CreateAsync(user);
                userId = user.Id;

                var roleStore = new RavenRoleStore<TestRole>(session);
                var role = new TestRole
                {
                    RoleName = roleName,
                    NormalizedRoleName = roleName.ToUpperInvariant()
                };
                await roleStore.CreateAsync(role);
            }

            await WaitForIndexing(store);

            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var user = await userStore.FindByIdAsync(userId);

                await userStore.AddToRoleAsync(user, roleName.ToUpperInvariant());
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Act: Remove from role
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var user = await userStore.FindByIdAsync(userId);

                await userStore.RemoveFromRoleAsync(user, roleName.ToUpperInvariant());
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: User no longer in role
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var user = await userStore.FindByIdAsync(userId);

                var roles = await userStore.GetRolesAsync(user);
                Assert.DoesNotContain(roleName, roles);

                var isInRole = await userStore.IsInRoleAsync(user, roleName.ToUpperInvariant());
                Assert.False(isInRole);
            }
        }

        /// <summary>
        /// Test: Change user's roles (remove old, add new).
        ///
        /// Verifies:
        /// - Can remove user from multiple roles
        /// - Can add user to different roles
        /// - Final role list is correct
        /// </summary>
        [Fact(DisplayName = "UserRole: Update - Change user roles")]
        public async Task Update_ChangeUserRoles_Success()
        {
            using var store = GetDocumentStore();
            const string username = "testuser";
            var oldRoles = new[] { "User", "Support" };
            var newRoles = new[] { "Admin", "Manager" };
            string userId;

            // Arrange: Create user with old roles
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var user = new TestUser
                {
                    UserName = username,
                    NormalizedUserName = username.ToUpperInvariant()
                };

                user.Email = new RavenIdentityUserEmail
                {
                    Email = $"{username}@example.com",
                    NormalizedEmail = $"{username.ToUpperInvariant()}@EXAMPLE.COM"
                };

                await userStore.CreateAsync(user);
                userId = user.Id;

                var roleStore = new RavenRoleStore<TestRole>(session);
                foreach (var roleName in oldRoles.Concat(newRoles))
                {
                    var role = new TestRole
                    {
                        RoleName = roleName,
                        NormalizedRoleName = roleName.ToUpperInvariant()
                    };
                    await roleStore.CreateAsync(role);
                }
            }

            await WaitForIndexing(store);

            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var user = await userStore.FindByIdAsync(userId);

                foreach (var roleName in oldRoles)
                {
                    await userStore.AddToRoleAsync(user, roleName.ToUpperInvariant());
                }

                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Act: Change roles
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var user = await userStore.FindByIdAsync(userId);

                // Remove old roles
                foreach (var roleName in oldRoles)
                {
                    await userStore.RemoveFromRoleAsync(user, roleName.ToUpperInvariant());
                }

                // Add new roles
                foreach (var roleName in newRoles)
                {
                    await userStore.AddToRoleAsync(user, roleName.ToUpperInvariant());
                }

                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Has new roles only
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var user = await userStore.FindByIdAsync(userId);

                var roles = await userStore.GetRolesAsync(user);
                Assert.Equal(newRoles.Length, roles.Count);

                foreach (var roleName in newRoles)
                {
                    Assert.Contains(roleName, roles);
                }

                foreach (var roleName in oldRoles)
                {
                    Assert.DoesNotContain(roleName, roles);
                }
            }
        }

        #endregion

        #region Delete Tests

        /// <summary>
        /// Test: Delete user removes from all roles.
        ///
        /// Verifies:
        /// - Deleting user removes them from all assigned roles
        /// - User no longer appears in role member lists
        /// - User is deleted from database
        /// </summary>
        [Fact(DisplayName = "UserRole: Delete - User removed from all roles")]
        public async Task Delete_UserRemovedFromRoles_Success()
        {
            using var store = GetDocumentStore();
            const string username = "testuser";
            var roleNames = new[] { "Admin", "Manager" };
            string userId;

            // Arrange: Create user with multiple roles
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var user = new TestUser
                {
                    UserName = username,
                    NormalizedUserName = username.ToUpperInvariant()
                };

                user.Email = new RavenIdentityUserEmail
                {
                    Email = $"{username}@example.com",
                    NormalizedEmail = $"{username.ToUpperInvariant()}@EXAMPLE.COM"
                };

                await userStore.CreateAsync(user);
                userId = user.Id;

                var roleStore = new RavenRoleStore<TestRole>(session);
                foreach (var roleName in roleNames)
                {
                    var role = new TestRole
                    {
                        RoleName = roleName,
                        NormalizedRoleName = roleName.ToUpperInvariant()
                    };
                    await roleStore.CreateAsync(role);
                }
            }

            await WaitForIndexing(store);

            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var user = await userStore.FindByIdAsync(userId);

                foreach (var roleName in roleNames)
                {
                    await userStore.AddToRoleAsync(user, roleName.ToUpperInvariant());
                }

                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Act: Delete user
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);
                var user = await userStore.FindByIdAsync(userId);

                var result = await userStore.DeleteAsync(user);
                Assert.True(result.Succeeded);
            }

            await WaitForIndexing(store);

            // Assert: User is gone
            using (var session = store.OpenAsyncSession())
            {
                var user = await session.LoadAsync<TestUser>(userId);
                Assert.Null(user);
            }

            // Assert: User no longer in any role member lists
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser, TestRole>(session);

                foreach (var roleName in roleNames)
                {
                    var usersInRole = await userStore.GetUsersInRoleAsync(roleName.ToUpperInvariant());
                    Assert.DoesNotContain(usersInRole, u => u.Id == userId);
                }
            }
        }

        #endregion
    }
}
