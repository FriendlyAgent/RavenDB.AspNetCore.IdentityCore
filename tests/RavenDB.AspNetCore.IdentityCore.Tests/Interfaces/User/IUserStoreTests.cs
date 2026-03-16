using Microsoft.AspNetCore.Identity;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Operations.CompareExchange;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.Test.Shared;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace RavenDB.AspNetCore.IdentityCore.Tests.Interfaces.User
{
    /// <summary>
    /// Tests for IUserStore&lt;TUser&gt; interface.
    ///
    /// Methods Tested:
    /// - CreateAsync(TUser user, CancellationToken cancellationToken)
    /// - UpdateAsync(TUser user, CancellationToken cancellationToken)
    /// - DeleteAsync(TUser user, CancellationToken cancellationToken)
    /// - FindByIdAsync(string userId, CancellationToken cancellationToken)
    /// - FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    /// - GetUserIdAsync(TUser user, CancellationToken cancellationToken)
    /// - GetUserNameAsync(TUser user, CancellationToken cancellationToken)
    /// - SetUserNameAsync(TUser user, string userName, CancellationToken cancellationToken)
    /// - GetNormalizedUserNameAsync(TUser user, CancellationToken cancellationToken)
    /// - SetNormalizedUserNameAsync(TUser user, string normalizedName, CancellationToken cancellationToken)
    /// </summary>
    public class IUserStoreTests : RavenDBTestBase
    {
        #region CreateAsync Tests

        [Theory(DisplayName = "IUserStore: CreateAsync - Success")]
        [InlineData("alice", "alice@example.com")]
        [InlineData("bob", "bob@example.com")]
        [InlineData("charlie", "charlie@example.com")]
        public async Task CreateAsync_ValidUser_Success(string username, string email)
        {
            using var store = GetDocumentStore();

            // Act: Create user
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = username,
                    NormalizedUserName = username.ToUpperInvariant()
                };

                user.Email = new RavenIdentityUserEmail
                {
                    Email = email,
                    NormalizedEmail = email.ToUpperInvariant()
                };

                var result = await userStore.CreateAsync(user);

                // Assert: Success
                Assert.True(result.Succeeded);
                Assert.NotNull(user.Id);
                Assert.StartsWith("TestUsers/", user.Id);
            }

            await WaitForIndexing(store);

            // Assert: Can retrieve by username
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var foundUser = await userStore.FindByNameAsync(username.ToUpperInvariant());

                Assert.NotNull(foundUser);
                Assert.Equal(username, foundUser.UserName);
            }
        }

        [Fact(DisplayName = "IUserStore: CreateAsync - Duplicate username fails")]
        public async Task CreateAsync_DuplicateUsername_Fails()
        {
            using var store = GetDocumentStore();
            const string username = "alice";

            // Act: Create first user
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = username,
                    NormalizedUserName = username.ToUpperInvariant()
                };

                user.Email = new RavenIdentityUserEmail
                {
                    Email = "alice1@example.com",
                    NormalizedEmail = "ALICE1@EXAMPLE.COM"
                };

                var result = await userStore.CreateAsync(user);
                Assert.True(result.Succeeded);
            }

            await WaitForIndexing(store);

            // Act: Try to create duplicate username
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var duplicateUser = new TestUser
                {
                    UserName = username,
                    NormalizedUserName = username.ToUpperInvariant()
                };

                duplicateUser.Email = new RavenIdentityUserEmail
                {
                    Email = "alice2@example.com",
                    NormalizedEmail = "ALICE2@EXAMPLE.COM"
                };

                var result = await userStore.CreateAsync(duplicateUser);

                // Assert: Fails with correct error
                Assert.False(result.Succeeded);
                Assert.Contains(result.Errors, e => e.Code == "DuplicateUserName");
            }

            await WaitForIndexing(store);

            // Assert: Only one user exists
            using (var session = store.OpenAsyncSession())
            {
                var users = await session.Query<TestUser>().ToListAsync();
                Assert.Single(users);
            }
        }

        #endregion

        #region FindByIdAsync, FindByNameAsync Tests

        [Fact(DisplayName = "IUserStore: FindByIdAsync - Success")]
        public async Task FindByIdAsync_ExistingUser_ReturnsUser()
        {
            using var store = GetDocumentStore();
            const string username = "developer";
            const string email = "developer@example.com";
            string userId;

            // Arrange: Create user
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = username,
                    NormalizedUserName = username.ToUpperInvariant()
                };

                user.Email = new RavenIdentityUserEmail
                {
                    Email = email,
                    NormalizedEmail = email.ToUpperInvariant()
                };

                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act & Assert: Find by ID
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var foundUser = await userStore.FindByIdAsync(userId);

                Assert.NotNull(foundUser);
                Assert.Equal(userId, foundUser.Id);
                Assert.Equal(username, foundUser.UserName);
            }
        }

        #endregion

        #region UpdateAsync, SetUserNameAsync Tests

        [Fact(DisplayName = "IUserStore: UpdateAsync - Change username")]
        public async Task UpdateAsync_ChangeUsername_Success()
        {
            using var store = GetDocumentStore();
            const string oldUsername = "moderator";
            const string newUsername = "seniormod";
            string userId;

            // Arrange: Create user
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = oldUsername,
                    NormalizedUserName = oldUsername.ToUpperInvariant()
                };

                user.Email = new RavenIdentityUserEmail
                {
                    Email = "mod@example.com",
                    NormalizedEmail = "MOD@EXAMPLE.COM"
                };

                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Update username
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                await userStore.SetUserNameAsync(user, newUsername);
                await userStore.SetNormalizedUserNameAsync(user, newUsername.ToUpperInvariant());
                var result = await userStore.UpdateAsync(user);

                Assert.True(result.Succeeded);
            }

            await WaitForIndexing(store);

            // Assert: New username is set
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                Assert.Equal(newUsername, user.UserName);
            }

            // Assert: Old reservation removed, new one exists
            var oldKey = CompareExchangeKeys.ForUserName(oldUsername);
            var oldReservation = await store.Operations.SendAsync(
                new GetCompareExchangeValueOperation<string>(oldKey));
            Assert.Null(oldReservation);

            var newKey = CompareExchangeKeys.ForUserName(newUsername);
            var newReservation = await store.Operations.SendAsync(
                new GetCompareExchangeValueOperation<string>(newKey));
            Assert.NotNull(newReservation);
        }

        #endregion

        #region DeleteAsync Tests

        [Fact(DisplayName = "IUserStore: DeleteAsync - Success")]
        public async Task DeleteAsync_ExistingUser_Success()
        {
            using var store = GetDocumentStore();
            const string username = "tempuser";
            const string email = "temp@example.com";
            string userId;

            // Arrange: Create user
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = username,
                    NormalizedUserName = username.ToUpperInvariant()
                };

                user.Email = new RavenIdentityUserEmail
                {
                    Email = email,
                    NormalizedEmail = email.ToUpperInvariant()
                };

                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Delete user
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
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

            // Assert: Compare Exchange reservations removed
            var usernameKey = CompareExchangeKeys.ForUserName(username);
            var usernameReservation = await store.Operations.SendAsync(
                new GetCompareExchangeValueOperation<string>(usernameKey));
            Assert.Null(usernameReservation);

            var emailKey = CompareExchangeKeys.ForEmail(email);
            var emailReservation = await store.Operations.SendAsync(
                new GetCompareExchangeValueOperation<string>(emailKey));
            Assert.Null(emailReservation);
        }

        #endregion
    }
}
