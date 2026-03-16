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
    /// Tests for IUserTwoFactorStore&lt;TUser&gt;, IUserAuthenticatorKeyStore&lt;TUser&gt;,
    /// and IUserTwoFactorRecoveryCodeStore&lt;TUser&gt; interfaces.
    ///
    /// Methods Tested:
    /// - IUserTwoFactorStore: SetTwoFactorEnabledAsync, GetTwoFactorEnabledAsync
    /// - IUserAuthenticatorKeyStore: SetAuthenticatorKeyAsync, GetAuthenticatorKeyAsync
    /// - IUserTwoFactorRecoveryCodeStore: ReplaceCodesAsync, RedeemCodeAsync, CountCodesAsync
    ///
    /// Original: Tests for user two-factor authentication functionality.
    ///
    /// Test Categories:
    /// - Two-factor enabled flag (IUserTwoFactorStore)
    /// - Authenticator keys (IUserAuthenticatorKeyStore)
    /// - Recovery codes (IUserTwoFactorRecoveryCodeStore)
    /// - Complete 2FA workflow
    /// </summary>
    public class UserTwoFactorTests : RavenDBTestBase
    {
        #region Two-Factor Enabled Tests

        /// <summary>
        /// Test: Enable two-factor authentication
        ///
        /// Verifies:
        /// - Two-factor can be enabled
        /// - Flag can be retrieved
        /// </summary>
        [Fact(DisplayName = "User: Enable two-factor authentication")]
        public async Task EnableTwoFactor_Success()
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

            // Act: Enable two-factor
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                await userStore.SetTwoFactorEnabledAsync(user, true);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Two-factor is enabled
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var isEnabled = await userStore.GetTwoFactorEnabledAsync(user);
                Assert.True(isEnabled);
            }
        }

        /// <summary>
        /// Test: Disable two-factor authentication
        ///
        /// Verifies:
        /// - Two-factor can be disabled
        /// - User can sign in without 2FA
        /// </summary>
        [Fact(DisplayName = "User: Disable two-factor authentication")]
        public async Task DisableTwoFactor_Success()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user with 2FA enabled
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "bob",
                    NormalizedUserName = "BOB"
                };
                user.Email = new RavenIdentityUserEmail("bob@example.com");
                await userStore.SetTwoFactorEnabledAsync(user, true);
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Disable two-factor
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                await userStore.SetTwoFactorEnabledAsync(user, false);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Two-factor is disabled
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var isEnabled = await userStore.GetTwoFactorEnabledAsync(user);
                Assert.False(isEnabled);
            }
        }

        /// <summary>
        /// Test: Two-factor defaults to false
        ///
        /// Verifies:
        /// - New users have two-factor disabled by default
        /// </summary>
        [Fact(DisplayName = "User: Two-factor defaults to false")]
        public async Task TwoFactorEnabled_DefaultsFalse()
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

            // Act & Assert: Check default value
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var isEnabled = await userStore.GetTwoFactorEnabledAsync(user);
                Assert.False(isEnabled);
            }
        }

        #endregion

        #region Authenticator Key Tests

        /// <summary>
        /// Test: Set and get authenticator key
        ///
        /// Verifies:
        /// - Authenticator key can be stored
        /// - Key can be retrieved
        /// </summary>
        [Fact(DisplayName = "User: Set authenticator key")]
        public async Task SetAuthenticatorKey_Success()
        {
            using var store = GetDocumentStore();
            const string authenticatorKey = "JBSWY3DPEHPK3PXP";
            string userId;

            // Arrange: Create user
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
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Set authenticator key
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                await userStore.SetAuthenticatorKeyAsync(user, authenticatorKey, default);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Key is retrievable
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var retrievedKey = await userStore.GetAuthenticatorKeyAsync(user, default);
                Assert.Equal(authenticatorKey, retrievedKey);
            }
        }

        /// <summary>
        /// Test: Get authenticator key when not set
        ///
        /// Verifies:
        /// - Returns null when key not set
        /// - No exception is thrown
        /// </summary>
        [Fact(DisplayName = "User: Get authenticator key when not set")]
        public async Task GetAuthenticatorKey_NotSet_ReturnsNull()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user without authenticator key
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

            // Act & Assert: Key is null
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var key = await userStore.GetAuthenticatorKeyAsync(user, default);
                Assert.Null(key);
            }
        }

        #endregion

        #region Recovery Codes Tests

        /// <summary>
        /// Test: Replace recovery codes
        ///
        /// Verifies:
        /// - Recovery codes can be generated and stored
        /// - Old codes are replaced with new ones
        /// </summary>
        [Fact(DisplayName = "User: Replace recovery codes")]
        public async Task ReplaceRecoveryCodes_Success()
        {
            using var store = GetDocumentStore();
            var newCodes = new[] { "CODE1", "CODE2", "CODE3", "CODE4", "CODE5" };
            string userId;

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

            // Act: Set recovery codes
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                await userStore.ReplaceCodesAsync(user, newCodes, default);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Codes are stored
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var count = await userStore.CountCodesAsync(user, default);
                Assert.Equal(newCodes.Length, count);
            }
        }

        /// <summary>
        /// Test: Redeem recovery code
        ///
        /// Verifies:
        /// - Recovery code can be used for authentication
        /// - Used code is removed from available codes
        /// </summary>
        [Fact(DisplayName = "User: Redeem recovery code")]
        public async Task RedeemRecoveryCode_Success()
        {
            using var store = GetDocumentStore();
            var recoveryCodes = new[] { "ABCD1234", "EFGH5678", "IJKL9012" };
            string userId;

            // Arrange: Create user with recovery codes
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
                await userStore.ReplaceCodesAsync(user, recoveryCodes, default);
                await userStore.UpdateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Redeem one code
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var result = await userStore.RedeemCodeAsync(user, "ABCD1234", default);
                await userStore.UpdateAsync(user);

                Assert.True(result);
            }

            await WaitForIndexing(store);

            // Assert: Code count decreased
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var count = await userStore.CountCodesAsync(user, default);
                Assert.Equal(recoveryCodes.Length - 1, count);
            }
        }

        /// <summary>
        /// Test: Redeem invalid recovery code
        ///
        /// Verifies:
        /// - Invalid code cannot be redeemed
        /// - Code count remains the same
        /// </summary>
        [Fact(DisplayName = "User: Redeem invalid recovery code")]
        public async Task RedeemRecoveryCode_Invalid_ReturnsFalse()
        {
            using var store = GetDocumentStore();
            var recoveryCodes = new[] { "ABCD1234", "EFGH5678" };
            string userId;

            // Arrange: Create user with recovery codes
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "helen",
                    NormalizedUserName = "HELEN"
                };
                user.Email = new RavenIdentityUserEmail("helen@example.com");
                await userStore.CreateAsync(user);
                await userStore.ReplaceCodesAsync(user, recoveryCodes, default);
                await userStore.UpdateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Try to redeem invalid code
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var result = await userStore.RedeemCodeAsync(user, "INVALID", default);
                await userStore.UpdateAsync(user);

                Assert.False(result);
            }

            await WaitForIndexing(store);

            // Assert: Code count unchanged
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var count = await userStore.CountCodesAsync(user, default);
                Assert.Equal(recoveryCodes.Length, count);
            }
        }

        /// <summary>
        /// Test: Count recovery codes when none exist
        ///
        /// Verifies:
        /// - Returns zero when no codes exist
        /// - No exception is thrown
        /// </summary>
        [Fact(DisplayName = "User: Count recovery codes when none exist")]
        public async Task CountRecoveryCodes_NoCodes_ReturnsZero()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user without recovery codes
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "ivan",
                    NormalizedUserName = "IVAN"
                };
                user.Email = new RavenIdentityUserEmail("ivan@example.com");
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act & Assert: Count is zero
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var count = await userStore.CountCodesAsync(user, default);
                Assert.Equal(0, count);
            }
        }

        #endregion

        #region Complete Two-Factor Workflow Tests

        /// <summary>
        /// Test: Complete two-factor setup workflow
        ///
        /// Verifies:
        /// - User sets up authenticator app
        /// - Generates recovery codes
        /// - Enables two-factor authentication
        /// </summary>
        [Fact(DisplayName = "User: Complete two-factor setup workflow")]
        public async Task TwoFactorSetupWorkflow_Success()
        {
            using var store = GetDocumentStore();
            const string username = "julia";
            const string authenticatorKey = "JBSWY3DPEHPK3PXP";
            var recoveryCodes = new[] { "CODE1", "CODE2", "CODE3", "CODE4", "CODE5",
                                       "CODE6", "CODE7", "CODE8", "CODE9", "CODE10" };
            string userId;

            // Step 1: Create user
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
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Step 2: Set authenticator key
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                await userStore.SetAuthenticatorKeyAsync(user, authenticatorKey, default);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Step 3: Generate recovery codes
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                await userStore.ReplaceCodesAsync(user, recoveryCodes, default);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Step 4: Enable two-factor
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                await userStore.SetTwoFactorEnabledAsync(user, true);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Step 5: Verify setup is complete
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var twoFactorEnabled = await userStore.GetTwoFactorEnabledAsync(user);
                Assert.True(twoFactorEnabled);

                var key = await userStore.GetAuthenticatorKeyAsync(user, default);
                Assert.Equal(authenticatorKey, key);

                var codeCount = await userStore.CountCodesAsync(user, default);
                Assert.Equal(recoveryCodes.Length, codeCount);
            }
        }

        /// <summary>
        /// Test: Disable two-factor and cleanup
        ///
        /// Verifies:
        /// - Two-factor can be disabled
        /// - Authenticator key and recovery codes are retained
        /// - User can re-enable later with same setup
        /// </summary>
        [Fact(DisplayName = "User: Disable two-factor workflow")]
        public async Task DisableTwoFactorWorkflow_Success()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user with full 2FA setup
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "kate",
                    NormalizedUserName = "KATE"
                };
                user.Email = new RavenIdentityUserEmail("kate@example.com");
                await userStore.CreateAsync(user);

                await userStore.SetAuthenticatorKeyAsync(user, "TESTKEY123", default);
                await userStore.ReplaceCodesAsync(user, new[] { "CODE1", "CODE2" }, default);
                await userStore.SetTwoFactorEnabledAsync(user, true);
                await userStore.UpdateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Disable two-factor
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                await userStore.SetTwoFactorEnabledAsync(user, false);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: 2FA is disabled but data remains
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var twoFactorEnabled = await userStore.GetTwoFactorEnabledAsync(user);
                Assert.False(twoFactorEnabled);

                // Key and codes are still there for potential re-enable
                var key = await userStore.GetAuthenticatorKeyAsync(user, default);
                Assert.NotNull(key);

                var codeCount = await userStore.CountCodesAsync(user, default);
                Assert.Equal(2, codeCount);
            }
        }

        #endregion
    }
}
