using Microsoft.AspNetCore.Identity;
using Raven.Client.Documents;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.Test.Shared;
using System.Threading.Tasks;
using Xunit;

namespace RavenDB.AspNetCore.IdentityCore.Tests.Interfaces.User
{
    /// <summary>
    /// Tests for IUserSecurityStampStore&lt;TUser&gt; interface.
    ///
    /// Methods Tested:
    /// - SetSecurityStampAsync(TUser user, string stamp, CancellationToken cancellationToken)
    /// - GetSecurityStampAsync(TUser user, CancellationToken cancellationToken)
    ///
    /// Security stamps are used to invalidate all sessions/tokens when
    /// critical account changes occur (password change, etc.)
    /// </summary>
    public class IUserSecurityStampStoreTests : RavenDBTestBase
    {
        [Fact(DisplayName = "IUserSecurityStampStore: SetSecurityStampAsync and GetSecurityStampAsync")]
        public async Task SetSecurityStampAsync_AndGet_Success()
        {
            using var store = GetDocumentStore();
            const string securityStamp = "SECURITY_STAMP_ABC123";
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

            // Act: Set security stamp
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);
                await userStore.SetSecurityStampAsync(user, securityStamp);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Security stamp is retrievable
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var retrievedStamp = await userStore.GetSecurityStampAsync(user);
                Assert.Equal(securityStamp, retrievedStamp);
            }
        }

        [Fact(DisplayName = "IUserSecurityStampStore: Update security stamp")]
        public async Task UpdateSecurityStampAsync_ReplacesOld_Success()
        {
            using var store = GetDocumentStore();
            const string oldStamp = "OLD_STAMP";
            const string newStamp = "NEW_STAMP";
            string userId;

            // Arrange: Create user with initial security stamp
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "alice",
                    NormalizedUserName = "ALICE"
                };
                user.Email = new RavenIdentityUserEmail("alice@example.com");
                await userStore.SetSecurityStampAsync(user, oldStamp);
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Update security stamp
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);
                await userStore.SetSecurityStampAsync(user, newStamp);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: New security stamp is set
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var retrievedStamp = await userStore.GetSecurityStampAsync(user);
                Assert.Equal(newStamp, retrievedStamp);
                Assert.NotEqual(oldStamp, retrievedStamp);
            }
        }
    }
}
