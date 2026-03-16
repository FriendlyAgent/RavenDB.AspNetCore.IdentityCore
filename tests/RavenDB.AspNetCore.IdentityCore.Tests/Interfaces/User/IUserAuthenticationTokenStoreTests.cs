using Microsoft.AspNetCore.Identity;
using Raven.Client.Documents;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.Test.Shared;
using System.Threading.Tasks;
using Xunit;

namespace RavenDB.AspNetCore.IdentityCore.Tests.Interfaces.User
{
    /// <summary>
    /// Tests for IUserAuthenticationTokenStore&lt;TUser&gt; interface.
    ///
    /// Methods Tested:
    /// - SetTokenAsync(TUser user, string loginProvider, string name, string value, CancellationToken cancellationToken)
    /// - RemoveTokenAsync(TUser user, string loginProvider, string name, CancellationToken cancellationToken)
    /// - GetTokenAsync(TUser user, string loginProvider, string name, CancellationToken cancellationToken)
    ///
    /// Tokens are used for refresh tokens, access tokens, etc.
    /// </summary>
    public class IUserAuthenticationTokenStoreTests : RavenDBTestBase
    {
        [Fact(DisplayName = "IUserAuthenticationTokenStore: SetTokenAsync and GetTokenAsync")]
        public async Task SetTokenAsync_AndGet_Success()
        {
            using var store = GetDocumentStore();
            const string loginProvider = "Google";
            const string tokenName = "RefreshToken";
            const string tokenValue = "REFRESH_TOKEN_XYZ789";
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

            // Act: Set token
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);
                await userStore.SetTokenAsync(user, loginProvider, tokenName, tokenValue, default);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Token is retrievable
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var retrievedToken = await userStore.GetTokenAsync(user, loginProvider, tokenName, default);
                Assert.Equal(tokenValue, retrievedToken);
            }
        }

        [Fact(DisplayName = "IUserAuthenticationTokenStore: Multiple tokens for different providers")]
        public async Task SetTokenAsync_MultipleProviders_AllStored()
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

            // Act: Set tokens for multiple providers
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                await userStore.SetTokenAsync(user, "Google", "RefreshToken", "GOOGLE_REFRESH", default);
                await userStore.SetTokenAsync(user, "Facebook", "RefreshToken", "FACEBOOK_REFRESH", default);
                await userStore.SetTokenAsync(user, "Google", "AccessToken", "GOOGLE_ACCESS", default);

                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: All tokens are retrievable
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var googleRefresh = await userStore.GetTokenAsync(user, "Google", "RefreshToken", default);
                Assert.Equal("GOOGLE_REFRESH", googleRefresh);

                var facebookRefresh = await userStore.GetTokenAsync(user, "Facebook", "RefreshToken", default);
                Assert.Equal("FACEBOOK_REFRESH", facebookRefresh);

                var googleAccess = await userStore.GetTokenAsync(user, "Google", "AccessToken", default);
                Assert.Equal("GOOGLE_ACCESS", googleAccess);
            }
        }

        [Fact(DisplayName = "IUserAuthenticationTokenStore: RemoveTokenAsync")]
        public async Task RemoveTokenAsync_Success()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user with tokens
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

                await userStore.SetTokenAsync(user, "Google", "RefreshToken", "GOOGLE_REFRESH", default);
                await userStore.SetTokenAsync(user, "Google", "AccessToken", "GOOGLE_ACCESS", default);
                await userStore.UpdateAsync(user);

                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Remove one token
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                await userStore.RemoveTokenAsync(user, "Google", "RefreshToken", default);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Removed token is null, other token remains
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var removedToken = await userStore.GetTokenAsync(user, "Google", "RefreshToken", default);
                Assert.Null(removedToken);

                var remainingToken = await userStore.GetTokenAsync(user, "Google", "AccessToken", default);
                Assert.Equal("GOOGLE_ACCESS", remainingToken);
            }
        }

        [Fact(DisplayName = "IUserAuthenticationTokenStore: GetTokenAsync - Non-existent returns null")]
        public async Task GetTokenAsync_NonExistent_ReturnsNull()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user without tokens
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

            // Act & Assert: Get non-existent token
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var token = await userStore.GetTokenAsync(user, "Google", "RefreshToken", default);
                Assert.Null(token);
            }
        }
    }
}
