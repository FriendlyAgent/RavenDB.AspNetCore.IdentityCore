using Microsoft.AspNetCore.Identity;
using Raven.Client.Documents;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.Test.Shared;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace RavenDB.AspNetCore.IdentityCore.Tests.Interfaces.User
{
    /// <summary>
    /// Tests for IUserLoginStore&lt;TUser&gt; interface.
    ///
    /// Methods Tested:
    /// - AddLoginAsync, RemoveLoginAsync
    /// - GetLoginsAsync, FindByLoginAsync
    ///
    /// Original: Tests for user external login functionality.
    ///
    /// Test Categories:
    /// - External login storage (IUserLoginStore)
    /// - Adding and removing logins
    /// - Finding users by login
    /// - Multiple login providers
    /// </summary>
    public class UserLoginsTests : RavenDBTestBase
    {
        #region Login Storage Tests

        /// <summary>
        /// Test: Add external login to user
        ///
        /// Verifies:
        /// - External login can be added
        /// - Login can be retrieved
        /// </summary>
        [Fact(DisplayName = "User: Add external login")]
        public async Task AddLogin_Success()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "alice",
                    NormalizedUserName = "ALICE"
                };
                user.Email = new RavenIdentityUserEmail("alice@example.com");
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Add external login
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var login = new UserLoginInfo("Google", "google123", "Google");
                await userStore.AddLoginAsync(user, login);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Login is retrievable
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var logins = await userStore.GetLoginsAsync(user);
                Assert.Single(logins);
                Assert.Contains(logins, l => l.LoginProvider == "Google" && l.ProviderKey == "google123");
            }
        }

        /// <summary>
        /// Test: Add multiple external logins to user
        ///
        /// Verifies:
        /// - User can have multiple external logins
        /// - Different providers can be linked to same user
        /// </summary>
        [Fact(DisplayName = "User: Add multiple external logins")]
        public async Task AddMultipleLogins_Success()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "bob",
                    NormalizedUserName = "BOB"
                };
                user.Email = new RavenIdentityUserEmail("bob@example.com");
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Add multiple external logins
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var googleLogin = new UserLoginInfo("Google", "google456", "Google");
                var facebookLogin = new UserLoginInfo("Facebook", "facebook789", "Facebook");
                var githubLogin = new UserLoginInfo("GitHub", "github012", "GitHub");

                await userStore.AddLoginAsync(user, googleLogin);
                await userStore.AddLoginAsync(user, facebookLogin);
                await userStore.AddLoginAsync(user, githubLogin);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: All logins are retrievable
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var logins = await userStore.GetLoginsAsync(user);
                Assert.Equal(3, logins.Count);
                Assert.Contains(logins, l => l.LoginProvider == "Google");
                Assert.Contains(logins, l => l.LoginProvider == "Facebook");
                Assert.Contains(logins, l => l.LoginProvider == "GitHub");
            }
        }

        /// <summary>
        /// Test: Get logins when user has none
        ///
        /// Verifies:
        /// - GetLogins returns empty list for users without logins
        /// - No exception is thrown
        /// </summary>
        [Fact(DisplayName = "User: Get logins when none exist")]
        public async Task GetLogins_NoLogins_ReturnsEmpty()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user without external logins
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "charlie",
                    NormalizedUserName = "CHARLIE"
                };
                user.Email = new RavenIdentityUserEmail("charlie@example.com");
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act & Assert: Get logins returns empty
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var logins = await userStore.GetLoginsAsync(user);
                Assert.Empty(logins);
            }
        }

        #endregion

        #region Login Removal Tests

        /// <summary>
        /// Test: Remove external login
        ///
        /// Verifies:
        /// - External login can be removed
        /// - Other logins remain
        /// </summary>
        [Fact(DisplayName = "User: Remove external login")]
        public async Task RemoveLogin_Success()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user with multiple logins
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "dave",
                    NormalizedUserName = "DAVE"
                };
                user.Email = new RavenIdentityUserEmail("dave@example.com");
                await userStore.CreateAsync(user);

                var googleLogin = new UserLoginInfo("Google", "google123", "Google");
                var facebookLogin = new UserLoginInfo("Facebook", "facebook456", "Facebook");
                await userStore.AddLoginAsync(user, googleLogin);
                await userStore.AddLoginAsync(user, facebookLogin);
                await userStore.UpdateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Remove one login
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                await userStore.RemoveLoginAsync(user, "Google", "google123");
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Login is removed, other remains
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var logins = await userStore.GetLoginsAsync(user);
                Assert.Single(logins);
                Assert.Contains(logins, l => l.LoginProvider == "Facebook");
                Assert.DoesNotContain(logins, l => l.LoginProvider == "Google");
            }
        }

        /// <summary>
        /// Test: Remove all external logins
        ///
        /// Verifies:
        /// - All logins can be removed
        /// - User has no logins after removal
        /// </summary>
        [Fact(DisplayName = "User: Remove all external logins")]
        public async Task RemoveAllLogins_Success()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user with multiple logins
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "eve",
                    NormalizedUserName = "EVE"
                };
                user.Email = new RavenIdentityUserEmail("eve@example.com");
                await userStore.CreateAsync(user);

                await userStore.AddLoginAsync(user, new UserLoginInfo("Google", "google123", "Google"));
                await userStore.AddLoginAsync(user, new UserLoginInfo("Facebook", "facebook456", "Facebook"));
                await userStore.UpdateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Remove all logins
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var logins = await userStore.GetLoginsAsync(user);
                foreach (var login in logins)
                {
                    await userStore.RemoveLoginAsync(user, login.LoginProvider, login.ProviderKey);
                }
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: No logins remain
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var retrievedLogins = await userStore.GetLoginsAsync(user);
                Assert.Empty(retrievedLogins);
            }
        }

        #endregion

        #region Find User By Login Tests

        /// <summary>
        /// Test: Find user by external login
        ///
        /// Verifies:
        /// - User can be found by their external login
        /// - Correct user is returned
        /// </summary>
        [Fact(DisplayName = "User: Find by external login")]
        public async Task FindByLogin_Success()
        {
            using var store = GetDocumentStore();
            const string username = "frank";

            // Arrange: Create user with external login
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = username,
                    NormalizedUserName = username.ToUpperInvariant()
                };
                user.Email = new RavenIdentityUserEmail($"{username}@example.com");
                await userStore.CreateAsync(user);

                var login = new UserLoginInfo("Google", "google999", "Google");
                await userStore.AddLoginAsync(user, login);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Act: Find user by login
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var foundUser = await userStore.FindByLoginAsync("Google", "google999");

                // Assert: Correct user is found
                Assert.NotNull(foundUser);
                Assert.Equal(username, foundUser.UserName);
            }
        }

        /// <summary>
        /// Test: Find user by external login returns null when not found
        ///
        /// Verifies:
        /// - Null returned when login doesn't exist
        /// - No exception is thrown
        /// </summary>
        [Fact(DisplayName = "User: Find by external login not found")]
        public async Task FindByLogin_NotFound_ReturnsNull()
        {
            using var store = GetDocumentStore();

            // Arrange: Create user with different login
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "grace",
                    NormalizedUserName = "GRACE"
                };
                user.Email = new RavenIdentityUserEmail("grace@example.com");
                await userStore.CreateAsync(user);

                var login = new UserLoginInfo("Google", "google123", "Google");
                await userStore.AddLoginAsync(user, login);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Act: Find user by non-existent login
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var foundUser = await userStore.FindByLoginAsync("Facebook", "nonexistent");

                // Assert: Null is returned
                Assert.Null(foundUser);
            }
        }

        /// <summary>
        /// Test: Multiple users with different logins
        ///
        /// Verifies:
        /// - Different users can have logins from same provider
        /// - Correct user is returned for each login
        /// </summary>
        [Fact(DisplayName = "User: Multiple users with different logins")]
        public async Task FindByLogin_MultipleUsers_CorrectUser()
        {
            using var store = GetDocumentStore();

            // Arrange: Create multiple users with Google logins
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);

                var user1 = new TestUser
                {
                    UserName = "alice",
                    NormalizedUserName = "ALICE"
                };
                user1.Email = new RavenIdentityUserEmail("alice@example.com");
                await userStore.CreateAsync(user1);
                await userStore.AddLoginAsync(user1, new UserLoginInfo("Google", "google_alice", "Google"));
                await userStore.UpdateAsync(user1);

                var user2 = new TestUser
                {
                    UserName = "bob",
                    NormalizedUserName = "BOB"
                };
                user2.Email = new RavenIdentityUserEmail("bob@example.com");
                await userStore.CreateAsync(user2);
                await userStore.AddLoginAsync(user2, new UserLoginInfo("Google", "google_bob", "Google"));
                await userStore.UpdateAsync(user2);
            }

            await WaitForIndexing(store);

            // Act & Assert: Find each user by their login
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);

                var alice = await userStore.FindByLoginAsync("Google", "google_alice");
                Assert.NotNull(alice);
                Assert.Equal("alice", alice.UserName);

                var bob = await userStore.FindByLoginAsync("Google", "google_bob");
                Assert.NotNull(bob);
                Assert.Equal("bob", bob.UserName);
            }
        }

        #endregion

        #region Complete Login Workflow Tests

        /// <summary>
        /// Test: Complete external login workflow
        ///
        /// Verifies:
        /// - User registers with password
        /// - Links external login later
        /// - Can sign in with either method
        /// </summary>
        [Fact(DisplayName = "User: Complete external login workflow")]
        public async Task ExternalLoginWorkflow_Success()
        {
            using var store = GetDocumentStore();
            const string username = "helen";
            string userId;

            // Step 1: User registers with password
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = username,
                    NormalizedUserName = username.ToUpperInvariant()
                };
                user.Email = new RavenIdentityUserEmail($"{username}@example.com");
                await userStore.SetPasswordHashAsync(user, "HASHED_PASSWORD");
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Step 2: User links Google account
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var googleLogin = new UserLoginInfo("Google", "google_helen", "Google");
                await userStore.AddLoginAsync(user, googleLogin);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Step 3: User can be found by username (password login)
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var foundByName = await userStore.FindByNameAsync(username.ToUpperInvariant());

                Assert.NotNull(foundByName);
                Assert.Equal(username, foundByName.UserName);
                Assert.NotNull(await userStore.GetPasswordHashAsync(foundByName));
            }

            // Step 4: User can be found by Google login
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var foundByLogin = await userStore.FindByLoginAsync("Google", "google_helen");

                Assert.NotNull(foundByLogin);
                Assert.Equal(username, foundByLogin.UserName);
            }
        }

        #endregion
    }
}
