using Microsoft.AspNetCore.Identity;
using Raven.Client.Documents;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.Test.Shared;
using System.Threading.Tasks;
using Xunit;

namespace RavenDB.AspNetCore.IdentityCore.Tests.Interfaces.User
{
    /// <summary>
    /// Tests for IUserPasswordStore&lt;TUser&gt; interface.
    ///
    /// Methods Tested:
    /// - SetPasswordHashAsync(TUser user, string passwordHash, CancellationToken cancellationToken)
    /// - GetPasswordHashAsync(TUser user, CancellationToken cancellationToken)
    /// - HasPasswordAsync(TUser user, CancellationToken cancellationToken)
    /// </summary>
    public class IUserPasswordStoreTests : RavenDBTestBase
    {
        [Fact(DisplayName = "IUserPasswordStore: SetPasswordHashAsync and GetPasswordHashAsync")]
        public async Task SetPasswordHashAsync_AndGet_Success()
        {
            using var store = GetDocumentStore();
            const string passwordHash = "HASHED_PASSWORD_123";
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

            // Act: Set password hash
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);
                await userStore.SetPasswordHashAsync(user, passwordHash);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Password hash is set and retrievable
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var retrievedHash = await userStore.GetPasswordHashAsync(user);
                Assert.Equal(passwordHash, retrievedHash);

                var hasPassword = await userStore.HasPasswordAsync(user);
                Assert.True(hasPassword);
            }
        }

        [Fact(DisplayName = "IUserPasswordStore: HasPasswordAsync returns false when no password")]
        public async Task HasPasswordAsync_NoPassword_ReturnsFalse()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user without password
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

            // Act & Assert: Check password status
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var hasPassword = await userStore.HasPasswordAsync(user);
                Assert.False(hasPassword);

                var passwordHash = await userStore.GetPasswordHashAsync(user);
                Assert.Null(passwordHash);
            }
        }

        [Fact(DisplayName = "IUserPasswordStore: SetPasswordHashAsync with null clears password")]
        public async Task SetPasswordHashAsync_Null_ClearsPassword()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user with password
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "alice",
                    NormalizedUserName = "ALICE"
                };
                user.Email = new RavenIdentityUserEmail("alice@example.com");
                await userStore.SetPasswordHashAsync(user, "HASHED_PASSWORD");
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Clear password
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);
                await userStore.SetPasswordHashAsync(user, null);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Password is cleared
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var hasPassword = await userStore.HasPasswordAsync(user);
                Assert.False(hasPassword);

                var passwordHash = await userStore.GetPasswordHashAsync(user);
                Assert.Null(passwordHash);
            }
        }
    }
}
