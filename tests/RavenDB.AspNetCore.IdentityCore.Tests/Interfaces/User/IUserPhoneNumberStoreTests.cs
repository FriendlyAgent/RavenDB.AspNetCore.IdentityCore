using Microsoft.AspNetCore.Identity;
using Raven.Client.Documents;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.Test.Shared;
using System.Threading.Tasks;
using Xunit;

namespace RavenDB.AspNetCore.IdentityCore.Tests.Interfaces.User
{
    /// <summary>
    /// Tests for IUserPhoneNumberStore&lt;TUser&gt; interface.
    ///
    /// Methods Tested:
    /// - SetPhoneNumberAsync, GetPhoneNumberAsync
    /// - GetPhoneNumberConfirmedAsync, SetPhoneNumberConfirmedAsync
    ///
    /// Original: Tests for user phone number functionality.
    ///
    /// Test Categories:
    /// - Phone number storage (IUserPhoneNumberStore)
    /// - Phone number confirmation status
    /// </summary>
    public class UserPhoneNumberTests : RavenDBTestBase
    {
        #region Phone Number Storage Tests

        /// <summary>
        /// Test: Set and get phone number
        ///
        /// Verifies:
        /// - Phone number can be set
        /// - Phone number can be retrieved
        /// </summary>
        [Fact(DisplayName = "User: Set and get phone number")]
        public async Task SetPhoneNumber_AndGet_Success()
        {
            using var store = GetDocumentStore();
            const string phoneNumber = "+1-555-123-4567";
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

            // Act: Set phone number
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);
                await userStore.SetPhoneNumberAsync(user, phoneNumber);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Phone number is retrievable
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var retrievedPhone = await userStore.GetPhoneNumberAsync(user);
                Assert.Equal(phoneNumber, retrievedPhone);
            }
        }

        /// <summary>
        /// Test: Update phone number
        ///
        /// Verifies:
        /// - Phone number can be updated
        /// - New phone number replaces old one
        /// </summary>
        [Fact(DisplayName = "User: Update phone number")]
        public async Task UpdatePhoneNumber_ReplacesOld_Success()
        {
            using var store = GetDocumentStore();
            const string oldPhone = "+1-555-111-1111";
            const string newPhone = "+1-555-222-2222";
            string userId;

            // Arrange: Create user with initial phone number
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "alice",
                    NormalizedUserName = "ALICE"
                };
                user.Email = new RavenIdentityUserEmail("alice@example.com");
                await userStore.SetPhoneNumberAsync(user, oldPhone);
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Update phone number
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);
                await userStore.SetPhoneNumberAsync(user, newPhone);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: New phone number is set
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var retrievedPhone = await userStore.GetPhoneNumberAsync(user);
                Assert.Equal(newPhone, retrievedPhone);
                Assert.NotEqual(oldPhone, retrievedPhone);
            }
        }

        /// <summary>
        /// Test: Clear phone number
        ///
        /// Verifies:
        /// - Phone number can be set to null
        /// - GetPhoneNumber returns null after clearing
        /// </summary>
        [Fact(DisplayName = "User: Clear phone number")]
        public async Task SetPhoneNumber_Null_ClearsPhone()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user with phone number
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "alice",
                    NormalizedUserName = "ALICE"
                };
                user.Email = new RavenIdentityUserEmail("alice@example.com");
                await userStore.SetPhoneNumberAsync(user, "+1-555-123-4567");
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Clear phone number
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);
                await userStore.SetPhoneNumberAsync(user, null);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Phone number is null
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var retrievedPhone = await userStore.GetPhoneNumberAsync(user);
                Assert.Null(retrievedPhone);
            }
        }

        /// <summary>
        /// Test: Get phone number when not set returns null
        ///
        /// Verifies:
        /// - GetPhoneNumber returns null for users without phone numbers
        /// - No exception is thrown
        /// </summary>
        [Fact(DisplayName = "User: Get phone number when not set returns null")]
        public async Task GetPhoneNumber_NotSet_ReturnsNull()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user without phone number
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

            // Act & Assert: Get phone number
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var phoneNumber = await userStore.GetPhoneNumberAsync(user);
                Assert.Null(phoneNumber);
            }
        }

        #endregion

        #region Phone Number Confirmation Tests

        /// <summary>
        /// Test: Set and get phone number confirmed flag
        ///
        /// Verifies:
        /// - Phone number confirmed flag can be set to true
        /// - Confirmation status can be retrieved
        /// </summary>
        [Fact(DisplayName = "User: Set phone number confirmed")]
        public async Task SetPhoneNumberConfirmed_True_Success()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user with phone number
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "alice",
                    NormalizedUserName = "ALICE"
                };
                user.Email = new RavenIdentityUserEmail("alice@example.com");
                await userStore.SetPhoneNumberAsync(user, "+1-555-123-4567");
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Set phone number confirmed
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);
                await userStore.SetPhoneNumberConfirmedAsync(user, true);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Phone number confirmed is true
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var isConfirmed = await userStore.GetPhoneNumberConfirmedAsync(user);
                Assert.True(isConfirmed);
            }
        }

        /// <summary>
        /// Test: Phone number confirmed defaults to false
        ///
        /// Verifies:
        /// - New users have phone number confirmed = false by default
        /// </summary>
        [Fact(DisplayName = "User: Phone number confirmed defaults to false")]
        public async Task PhoneNumberConfirmed_DefaultsFalse()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user with phone number
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "alice",
                    NormalizedUserName = "ALICE"
                };
                user.Email = new RavenIdentityUserEmail("alice@example.com");
                await userStore.SetPhoneNumberAsync(user, "+1-555-123-4567");
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act & Assert: Check default confirmation status
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var isConfirmed = await userStore.GetPhoneNumberConfirmedAsync(user);
                Assert.False(isConfirmed);
            }
        }

        /// <summary>
        /// Test: Unconfirm phone number
        ///
        /// Verifies:
        /// - Phone number confirmed flag can be set to false
        /// - This is useful when phone number is changed and needs re-confirmation
        /// </summary>
        [Fact(DisplayName = "User: Unconfirm phone number")]
        public async Task SetPhoneNumberConfirmed_False_Success()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user with confirmed phone number
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "alice",
                    NormalizedUserName = "ALICE"
                };
                user.Email = new RavenIdentityUserEmail("alice@example.com");
                await userStore.SetPhoneNumberAsync(user, "+1-555-123-4567");
                await userStore.SetPhoneNumberConfirmedAsync(user, true);
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Unconfirm phone number
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);
                await userStore.SetPhoneNumberConfirmedAsync(user, false);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Phone number confirmed is false
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var isConfirmed = await userStore.GetPhoneNumberConfirmedAsync(user);
                Assert.False(isConfirmed);
            }
        }

        /// <summary>
        /// Test: Complete phone number verification workflow
        ///
        /// Verifies:
        /// - Complete workflow: add phone → send code → confirm
        /// - Phone starts unconfirmed
        /// - After confirmation, flag is set to true
        /// </summary>
        [Fact(DisplayName = "User: Complete phone number verification workflow")]
        public async Task PhoneNumberVerification_CompleteWorkflow_Success()
        {
            using var store = GetDocumentStore();
            const string phoneNumber = "+1-555-123-4567";
            string userId;

            // Step 1: Create user
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

            // Step 2: User adds phone number (unconfirmed)
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);
                await userStore.SetPhoneNumberAsync(user, phoneNumber);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Step 3: Verify phone is set but not confirmed
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var phone = await userStore.GetPhoneNumberAsync(user);
                Assert.Equal(phoneNumber, phone);

                var isConfirmed = await userStore.GetPhoneNumberConfirmedAsync(user);
                Assert.False(isConfirmed);
            }

            // Step 4: User confirms phone number (after verifying SMS code)
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);
                await userStore.SetPhoneNumberConfirmedAsync(user, true);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Step 5: Verify phone is now confirmed
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var isConfirmed = await userStore.GetPhoneNumberConfirmedAsync(user);
                Assert.True(isConfirmed);
            }
        }

        /// <summary>
        /// Test: Changing phone number should reset confirmation
        ///
        /// Verifies:
        /// - When phone number changes, confirmation should be reset to false
        /// - This ensures users verify their new phone numbers
        /// </summary>
        [Fact(DisplayName = "User: Changing phone number workflow")]
        public async Task ChangePhoneNumber_ShouldResetConfirmation()
        {
            using var store = GetDocumentStore();
            const string oldPhone = "+1-555-111-1111";
            const string newPhone = "+1-555-222-2222";
            string userId;

            // Arrange: Create user with confirmed phone number
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "alice",
                    NormalizedUserName = "ALICE"
                };
                user.Email = new RavenIdentityUserEmail("alice@example.com");
                await userStore.SetPhoneNumberAsync(user, oldPhone);
                await userStore.SetPhoneNumberConfirmedAsync(user, true);
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Change phone number and reset confirmation
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                // When changing phone, confirmation should be reset
                await userStore.SetPhoneNumberAsync(user, newPhone);
                await userStore.SetPhoneNumberConfirmedAsync(user, false);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: New phone is set but not confirmed
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var phone = await userStore.GetPhoneNumberAsync(user);
                Assert.Equal(newPhone, phone);

                var isConfirmed = await userStore.GetPhoneNumberConfirmedAsync(user);
                Assert.False(isConfirmed);
            }
        }

        #endregion
    }
}
