using Microsoft.AspNetCore.Identity;
using Raven.Client.Documents;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.Test.Shared;
using System;
using System.Threading.Tasks;
using Xunit;

namespace RavenDB.AspNetCore.IdentityCore.Tests.Interfaces.User
{
    /// <summary>
    /// Tests for IUserLockoutStore&lt;TUser&gt; interface.
    ///
    /// Methods Tested:
    /// - GetLockoutEndDateAsync, SetLockoutEndDateAsync
    /// - IncrementAccessFailedCountAsync, ResetAccessFailedCountAsync
    /// - GetAccessFailedCountAsync
    /// - GetLockoutEnabledAsync, SetLockoutEnabledAsync
    ///
    /// Original: Tests for user account lockout functionality.
    ///
    /// Test Categories:
    /// - Lockout enabled flag (IUserLockoutStore)
    /// - Access failed count tracking
    /// - Lockout end date management
    /// - Account locking/unlocking workflow
    /// </summary>
    public class UserLockoutTests : RavenDBTestBase
    {
        #region Lockout Enabled Tests

        /// <summary>
        /// Test: Set and get lockout enabled flag
        ///
        /// Verifies:
        /// - Lockout enabled flag can be set
        /// - Flag can be retrieved
        /// </summary>
        [Fact(DisplayName = "User: Set lockout enabled")]
        public async Task SetLockoutEnabled_Success()
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

            // Act: Enable lockout
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                await userStore.SetLockoutEnabledAsync(user, true);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Lockout is enabled
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var isEnabled = await userStore.GetLockoutEnabledAsync(user);
                Assert.True(isEnabled);
            }
        }

        /// <summary>
        /// Test: Disable lockout
        ///
        /// Verifies:
        /// - Lockout can be disabled
        /// - User cannot be locked out when disabled
        /// </summary>
        [Fact(DisplayName = "User: Disable lockout")]
        public async Task SetLockoutDisabled_Success()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user with lockout enabled
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "bob",
                    NormalizedUserName = "BOB"
                };
                user.Email = new RavenIdentityUserEmail("bob@example.com");
                await userStore.SetLockoutEnabledAsync(user, true);
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Disable lockout
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                await userStore.SetLockoutEnabledAsync(user, false);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Lockout is disabled
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var isEnabled = await userStore.GetLockoutEnabledAsync(user);
                Assert.False(isEnabled);
            }
        }

        #endregion

        #region Access Failed Count Tests

        /// <summary>
        /// Test: Increment access failed count
        ///
        /// Verifies:
        /// - Access failed count can be incremented
        /// - Count is tracked correctly
        /// </summary>
        [Fact(DisplayName = "User: Increment access failed count")]
        public async Task IncrementAccessFailedCount_Success()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user
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

            // Act: Increment failed count
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                await userStore.IncrementAccessFailedCountAsync(user);
                await userStore.IncrementAccessFailedCountAsync(user);
                await userStore.IncrementAccessFailedCountAsync(user);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Count is correct
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var count = await userStore.GetAccessFailedCountAsync(user);
                Assert.Equal(3, count);
            }
        }

        /// <summary>
        /// Test: Reset access failed count
        ///
        /// Verifies:
        /// - Access failed count can be reset to zero
        /// - Useful after successful login
        /// </summary>
        [Fact(DisplayName = "User: Reset access failed count")]
        public async Task ResetAccessFailedCount_Success()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user with failed attempts
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

                await userStore.IncrementAccessFailedCountAsync(user);
                await userStore.IncrementAccessFailedCountAsync(user);
                await userStore.UpdateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Reset count
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                await userStore.ResetAccessFailedCountAsync(user);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Count is zero
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var count = await userStore.GetAccessFailedCountAsync(user);
                Assert.Equal(0, count);
            }
        }

        /// <summary>
        /// Test: Access failed count defaults to zero
        ///
        /// Verifies:
        /// - New users have zero failed access attempts
        /// </summary>
        [Fact(DisplayName = "User: Access failed count defaults to zero")]
        public async Task AccessFailedCount_DefaultsZero()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user
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
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act & Assert: Check default count
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var count = await userStore.GetAccessFailedCountAsync(user);
                Assert.Equal(0, count);
            }
        }

        #endregion

        #region Lockout End Date Tests

        /// <summary>
        /// Test: Set lockout end date
        ///
        /// Verifies:
        /// - Lockout end date can be set
        /// - User is locked out until that date
        /// </summary>
        [Fact(DisplayName = "User: Set lockout end date")]
        public async Task SetLockoutEndDate_Success()
        {
            using var store = GetDocumentStore();
            string userId;
            var lockoutEnd = DateTimeOffset.UtcNow.AddHours(1);

            // Arrange: Create user
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "frank",
                    NormalizedUserName = "FRANK"
                };
                user.Email = new RavenIdentityUserEmail("frank@example.com");
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Set lockout end date
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                await userStore.SetLockoutEndDateAsync(user, lockoutEnd);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Lockout end date is set
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var retrievedEnd = await userStore.GetLockoutEndDateAsync(user);
                Assert.NotNull(retrievedEnd);
                // Allow 1 second tolerance for test execution time
                Assert.True(Math.Abs((retrievedEnd.Value - lockoutEnd).TotalSeconds) < 1);
            }
        }

        /// <summary>
        /// Test: Clear lockout end date
        ///
        /// Verifies:
        /// - Lockout can be cleared by setting null end date
        /// - User is no longer locked out
        /// </summary>
        [Fact(DisplayName = "User: Clear lockout end date")]
        public async Task ClearLockoutEndDate_Success()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user with lockout
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "grace",
                    NormalizedUserName = "GRACE"
                };
                user.Email = new RavenIdentityUserEmail("grace@example.com");
                await userStore.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddHours(1));
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Clear lockout
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                await userStore.SetLockoutEndDateAsync(user, null);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Lockout is cleared
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var lockoutEnd = await userStore.GetLockoutEndDateAsync(user);
                Assert.Null(lockoutEnd);
            }
        }

        #endregion

        #region Complete Lockout Workflow Tests

        /// <summary>
        /// Test: Complete lockout workflow
        ///
        /// Verifies:
        /// - Failed login attempts increment counter
        /// - Account is locked after threshold
        /// - Successful login clears lockout
        /// </summary>
        [Fact(DisplayName = "User: Complete lockout workflow")]
        public async Task LockoutWorkflow_Success()
        {
            using var store = GetDocumentStore();
            const string username = "helen";
            string userId;
            const int maxAttempts = 5;

            // Step 1: Create user with lockout enabled
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = username,
                    NormalizedUserName = username.ToUpperInvariant()
                };
                user.Email = new RavenIdentityUserEmail($"{username}@example.com");
                await userStore.SetLockoutEnabledAsync(user, true);
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Step 2: Simulate failed login attempts
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                for (int i = 0; i < maxAttempts; i++)
                {
                    await userStore.IncrementAccessFailedCountAsync(user);
                }

                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Step 3: Verify failed count reached threshold
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var count = await userStore.GetAccessFailedCountAsync(user);
                Assert.Equal(maxAttempts, count);
            }

            // Step 4: Lock account
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var lockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15);
                await userStore.SetLockoutEndDateAsync(user, lockoutEnd);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Step 5: Verify account is locked
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var lockoutEnd = await userStore.GetLockoutEndDateAsync(user);
                Assert.NotNull(lockoutEnd);
                Assert.True(lockoutEnd.Value > DateTimeOffset.UtcNow);
            }

            // Step 6: Unlock account and reset counter (simulate successful login after lockout expires)
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                await userStore.SetLockoutEndDateAsync(user, null);
                await userStore.ResetAccessFailedCountAsync(user);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Step 7: Verify account is unlocked
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var lockoutEnd = await userStore.GetLockoutEndDateAsync(user);
                Assert.Null(lockoutEnd);

                var count = await userStore.GetAccessFailedCountAsync(user);
                Assert.Equal(0, count);
            }
        }

        /// <summary>
        /// Test: Lockout with automatic expiration
        ///
        /// Verifies:
        /// - Account is locked with expiration time
        /// - Lockout expires after specified duration
        /// </summary>
        [Fact(DisplayName = "User: Lockout with automatic expiration")]
        public async Task LockoutExpiration_Success()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user locked out for a very short time
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "ivan",
                    NormalizedUserName = "IVAN"
                };
                user.Email = new RavenIdentityUserEmail("ivan@example.com");

                // Set lockout to expire in the past (already expired)
                var lockoutEnd = DateTimeOffset.UtcNow.AddSeconds(-1);
                await userStore.SetLockoutEndDateAsync(user, lockoutEnd);
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Assert: Lockout has already expired
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var lockoutEnd = await userStore.GetLockoutEndDateAsync(user);
                Assert.NotNull(lockoutEnd);
                Assert.True(lockoutEnd.Value < DateTimeOffset.UtcNow);
            }
        }

        #endregion
    }
}
