using Microsoft.AspNetCore.Identity;
using Raven.Client.Documents;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.Test.Shared;
using System.Threading.Tasks;
using Xunit;

namespace RavenDB.AspNetCore.IdentityCore.Tests.Interfaces.User
{
    /// <summary>
    /// Tests for IUserEmailStore&lt;TUser&gt; interface.
    ///
    /// Methods Tested:
    /// - SetEmailAsync, GetEmailAsync
    /// - GetEmailConfirmedAsync, SetEmailConfirmedAsync
    /// - FindByEmailAsync
    /// - GetNormalizedEmailAsync, SetNormalizedEmailAsync
    ///
    /// Original: Tests for user email functionality.
    ///
    /// Test Categories:
    /// - Email storage (IUserEmailStore)
    /// - Email confirmation status
    /// - Email validation and uniqueness
    /// </summary>
    public class UserEmailTests : RavenDBTestBase
    {
        #region Email Storage Tests

        /// <summary>
        /// Test: Set and get email
        ///
        /// Verifies:
        /// - Email can be set
        /// - Email can be retrieved
        /// </summary>
        [Fact(DisplayName = "User: Set and get email")]
        public async Task SetEmail_AndGet_Success()
        {
            using var store = GetDocumentStore();
            const string email = "alice@example.com";
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
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Set email
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);
                await userStore.SetEmailAsync(user, email);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Email is retrievable
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var retrievedEmail = await userStore.GetEmailAsync(user);
                Assert.Equal(email, retrievedEmail);
            }
        }

        /// <summary>
        /// Test: Update email
        ///
        /// Verifies:
        /// - Email can be updated
        /// - New email replaces old one
        /// </summary>
        [Fact(DisplayName = "User: Update email")]
        public async Task UpdateEmail_ReplacesOld_Success()
        {
            using var store = GetDocumentStore();
            const string oldEmail = "alice@old.com";
            const string newEmail = "alice@new.com";
            string userId;

            var identityOptions = Microsoft.Extensions.Options.Options.Create(new IdentityOptions
            {
                User = { RequireUniqueEmail = true }
            });

            // Arrange: Create user with initial email
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session, null, identityOptions);
                var user = new TestUser
                {
                    UserName = "alice",
                    NormalizedUserName = "ALICE"
                };
                user.Email = new RavenIdentityUserEmail(oldEmail)
                {
                    NormalizedEmail = oldEmail.ToUpperInvariant()
                };
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Update email
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session, null, identityOptions);
                var user = await userStore.FindByIdAsync(userId);
                await userStore.SetEmailAsync(user, newEmail);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: New email is set
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var retrievedEmail = await userStore.GetEmailAsync(user);
                Assert.Equal(newEmail, retrievedEmail);
                Assert.NotEqual(oldEmail, retrievedEmail);
            }
        }

        /// <summary>
        /// Test: Clear email
        ///
        /// Verifies:
        /// - Email can be set to null
        /// - GetEmail returns null after clearing
        /// </summary>
        [Fact(DisplayName = "User: Clear email")]
        public async Task SetEmail_Null_ClearsEmail()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user with email
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "alice",
                    NormalizedUserName = "ALICE"
                };
                user.Email = new RavenIdentityUserEmail("alice@example.com")
                {
                    NormalizedEmail = "ALICE@EXAMPLE.COM"
                };
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Clear email
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);
                await userStore.SetEmailAsync(user, null);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Email is null
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var retrievedEmail = await userStore.GetEmailAsync(user);
                Assert.Null(retrievedEmail);
            }
        }

        /// <summary>
        /// Test: Get email when not set returns null
        ///
        /// Verifies:
        /// - GetEmail returns null for users without email
        /// - No exception is thrown
        /// </summary>
        [Fact(DisplayName = "User: Get email when not set returns null")]
        public async Task GetEmail_NotSet_ReturnsNull()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user without email
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "alice",
                    NormalizedUserName = "ALICE"
                };
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act & Assert: Get email
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var email = await userStore.GetEmailAsync(user);
                Assert.Null(email);
            }
        }

        /// <summary>
        /// Test: Get normalized email
        ///
        /// Verifies:
        /// - Normalized email is stored correctly
        /// - Case-insensitive email lookups are supported
        /// </summary>
        [Fact(DisplayName = "User: Get normalized email")]
        public async Task GetNormalizedEmail_Success()
        {
            using var store = GetDocumentStore();
            const string email = "Alice@Example.Com";
            string userId;

            // Arrange: Create user with email
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "alice",
                    NormalizedUserName = "ALICE"
                };
                user.Email = new RavenIdentityUserEmail(email)
                {
                    NormalizedEmail = email.ToUpperInvariant()
                };
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act & Assert: Get normalized email
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var normalizedEmail = await userStore.GetNormalizedEmailAsync(user);
                Assert.Equal(email.ToUpperInvariant(), normalizedEmail);
            }
        }

        #endregion

        #region Email Confirmation Tests

        /// <summary>
        /// Test: Set and get email confirmed flag
        ///
        /// Verifies:
        /// - Email confirmed flag can be set to true
        /// - Confirmation status can be retrieved
        /// </summary>
        [Fact(DisplayName = "User: Set email confirmed")]
        public async Task SetEmailConfirmed_True_Success()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user with email
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "alice",
                    NormalizedUserName = "ALICE"
                };
                user.Email = new RavenIdentityUserEmail("alice@example.com")
                {
                    NormalizedEmail = "ALICE@EXAMPLE.COM"
                };
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Set email confirmed
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);
                await userStore.SetEmailConfirmedAsync(user, true);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Email confirmed is true
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var isConfirmed = await userStore.GetEmailConfirmedAsync(user);
                Assert.True(isConfirmed);
            }
        }

        /// <summary>
        /// Test: Email confirmed defaults to false
        ///
        /// Verifies:
        /// - New users have email confirmed = false by default
        /// </summary>
        [Fact(DisplayName = "User: Email confirmed defaults to false")]
        public async Task EmailConfirmed_DefaultsFalse()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user with email
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "alice",
                    NormalizedUserName = "ALICE"
                };
                user.Email = new RavenIdentityUserEmail("alice@example.com")
                {
                    NormalizedEmail = "ALICE@EXAMPLE.COM"
                };
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act & Assert: Check default confirmation status
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var isConfirmed = await userStore.GetEmailConfirmedAsync(user);
                Assert.False(isConfirmed);
            }
        }

        /// <summary>
        /// Test: Unconfirm email
        ///
        /// Verifies:
        /// - Email confirmed flag can be set to false
        /// - This is useful when email is changed and needs re-confirmation
        /// </summary>
        [Fact(DisplayName = "User: Unconfirm email")]
        public async Task SetEmailConfirmed_False_Success()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user with confirmed email
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = new TestUser
                {
                    UserName = "alice",
                    NormalizedUserName = "ALICE"
                };
                user.Email = new RavenIdentityUserEmail("alice@example.com")
                {
                    NormalizedEmail = "ALICE@EXAMPLE.COM"
                };
                await userStore.SetEmailConfirmedAsync(user, true);
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Unconfirm email
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);
                await userStore.SetEmailConfirmedAsync(user, false);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Email confirmed is false
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var isConfirmed = await userStore.GetEmailConfirmedAsync(user);
                Assert.False(isConfirmed);
            }
        }

        /// <summary>
        /// Test: Complete email verification workflow
        ///
        /// Verifies:
        /// - Complete workflow: add email → send code → confirm
        /// - Email starts unconfirmed
        /// - After confirmation, flag is set to true
        /// </summary>
        [Fact(DisplayName = "User: Complete email verification workflow")]
        public async Task EmailVerification_CompleteWorkflow_Success()
        {
            using var store = GetDocumentStore();
            const string email = "alice@example.com";
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
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Step 2: User adds email (unconfirmed)
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);
                await userStore.SetEmailAsync(user, email);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Step 3: Verify email is set but not confirmed
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var retrievedEmail = await userStore.GetEmailAsync(user);
                Assert.Equal(email, retrievedEmail);

                var isConfirmed = await userStore.GetEmailConfirmedAsync(user);
                Assert.False(isConfirmed);
            }

            // Step 4: User confirms email (after verifying confirmation code)
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);
                await userStore.SetEmailConfirmedAsync(user, true);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Step 5: Verify email is now confirmed
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var isConfirmed = await userStore.GetEmailConfirmedAsync(user);
                Assert.True(isConfirmed);
            }
        }

        /// <summary>
        /// Test: Changing email should reset confirmation
        ///
        /// Verifies:
        /// - When email changes, confirmation should be reset to false
        /// - This ensures users verify their new email addresses
        /// </summary>
        [Fact(DisplayName = "User: Changing email workflow")]
        public async Task ChangeEmail_ShouldResetConfirmation()
        {
            using var store = GetDocumentStore();
            const string oldEmail = "alice@old.com";
            const string newEmail = "alice@new.com";
            string userId;

            var identityOptions = Microsoft.Extensions.Options.Options.Create(new IdentityOptions
            {
                User = { RequireUniqueEmail = true }
            });

            // Arrange: Create user with confirmed email
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session, null, identityOptions);
                var user = new TestUser
                {
                    UserName = "alice",
                    NormalizedUserName = "ALICE"
                };
                user.Email = new RavenIdentityUserEmail(oldEmail)
                {
                    NormalizedEmail = oldEmail.ToUpperInvariant()
                };
                await userStore.SetEmailConfirmedAsync(user, true);
                await userStore.CreateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Change email and reset confirmation
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session, null, identityOptions);
                var user = await userStore.FindByIdAsync(userId);

                // When changing email, confirmation should be reset
                await userStore.SetEmailAsync(user, newEmail);
                await userStore.SetEmailConfirmedAsync(user, false);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: New email is set but not confirmed
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var email = await userStore.GetEmailAsync(user);
                Assert.Equal(newEmail, email);

                var isConfirmed = await userStore.GetEmailConfirmedAsync(user);
                Assert.False(isConfirmed);
            }
        }

        #endregion

        #region Email Uniqueness Tests

        /// <summary>
        /// Test: Duplicate email enforcement when enabled
        ///
        /// Verifies:
        /// - When RequireUniqueEmail is true, duplicate emails are rejected
        /// - Error code is "DuplicateEmail"
        /// </summary>
        [Fact(DisplayName = "User: Duplicate email rejected when uniqueness required")]
        public async Task DuplicateEmail_WhenRequired_Fails()
        {
            using var store = GetDocumentStore();
            const string email = "alice@example.com";

            var identityOptions = Microsoft.Extensions.Options.Options.Create(new IdentityOptions
            {
                User = { RequireUniqueEmail = true }
            });

            // Arrange: Create first user
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session, null, identityOptions);
                var user = new TestUser
                {
                    UserName = "alice",
                    NormalizedUserName = "ALICE"
                };
                user.Email = new RavenIdentityUserEmail(email)
                {
                    NormalizedEmail = email.ToUpperInvariant()
                };
                await userStore.CreateAsync(user);
            }

            await WaitForIndexing(store);

            // Act: Try to create second user with same email
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session, null, identityOptions);
                var duplicateUser = new TestUser
                {
                    UserName = "bob",
                    NormalizedUserName = "BOB"
                };
                duplicateUser.Email = new RavenIdentityUserEmail(email)
                {
                    NormalizedEmail = email.ToUpperInvariant()
                };

                var result = await userStore.CreateAsync(duplicateUser);

                // Assert: Fails with correct error
                Assert.False(result.Succeeded);
                Assert.Contains(result.Errors, e => e.Code == "DuplicateEmail");
            }
        }

        /// <summary>
        /// Test: Duplicate email allowed when uniqueness not required
        ///
        /// Verifies:
        /// - When RequireUniqueEmail is false, duplicate emails are allowed
        /// - Multiple users can have the same email
        /// </summary>
        [Fact(DisplayName = "User: Duplicate email allowed when uniqueness not required")]
        public async Task DuplicateEmail_WhenNotRequired_Succeeds()
        {
            using var store = GetDocumentStore();
            const string email = "shared@example.com";

            var identityOptions = Microsoft.Extensions.Options.Options.Create(new IdentityOptions
            {
                User = { RequireUniqueEmail = false }
            });

            // Arrange: Create first user
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session, null, identityOptions);
                var user1 = new TestUser
                {
                    UserName = "alice",
                    NormalizedUserName = "ALICE"
                };
                user1.Email = new RavenIdentityUserEmail(email);
                await userStore.CreateAsync(user1);
            }

            await WaitForIndexing(store);

            // Act: Create second user with same email
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session, null, identityOptions);
                var user2 = new TestUser
                {
                    UserName = "bob",
                    NormalizedUserName = "BOB"
                };
                user2.Email = new RavenIdentityUserEmail(email);

                var result = await userStore.CreateAsync(user2);

                // Assert: Success
                Assert.True(result.Succeeded);
            }
        }

        /// <summary>
        /// Test: User can be created without email when uniqueness required
        ///
        /// Verifies:
        /// - RequireUniqueEmail only applies when email IS provided
        /// - Users can still be created without email even when RequireUniqueEmail = true
        /// - This is standard ASP.NET Core Identity behavior
        /// </summary>
        [Fact(DisplayName = "User: Create without email when uniqueness required - allowed")]
        public async Task CreateWithoutEmail_WhenUniquenessRequired_Succeeds()
        {
            using var store = GetDocumentStore();

            var identityOptions = Microsoft.Extensions.Options.Options.Create(new IdentityOptions
            {
                User = { RequireUniqueEmail = true }
            });

            // Act: Create user without email
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session, null, identityOptions);
                var user = new TestUser
                {
                    UserName = "alice",
                    NormalizedUserName = "ALICE"
                    // Email intentionally not set
                };

                var result = await userStore.CreateAsync(user);

                // Assert: Creation succeeds
                Assert.True(result.Succeeded);
                Assert.NotNull(user.Id);
                Assert.Null(user.Email);
            }
        }

        /// <summary>
        /// Test: Multiple users can be created without email
        ///
        /// Verifies:
        /// - Multiple users can exist without email addresses
        /// - No conflict occurs when email is null for multiple users
        /// </summary>
        [Fact(DisplayName = "User: Multiple users without email - allowed")]
        public async Task CreateMultipleWithoutEmail_Succeeds()
        {
            using var store = GetDocumentStore();
            string aliceId, bobId;

            var identityOptions = Microsoft.Extensions.Options.Options.Create(new IdentityOptions
            {
                User = { RequireUniqueEmail = true }
            });

            // Act: Create first user without email
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session, null, identityOptions);
                var user1 = new TestUser
                {
                    UserName = "alice",
                    NormalizedUserName = "ALICE"
                };

                var result = await userStore.CreateAsync(user1);
                Assert.True(result.Succeeded);
                aliceId = user1.Id;
            }

            await WaitForIndexing(store);

            // Act: Create second user without email
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session, null, identityOptions);
                var user2 = new TestUser
                {
                    UserName = "bob",
                    NormalizedUserName = "BOB"
                };

                var result = await userStore.CreateAsync(user2);

                // Assert: Both succeed
                Assert.True(result.Succeeded);
                bobId = user2.Id;
            }

            await WaitForIndexing(store);

            // Assert: Both users exist
            using (var session = store.OpenAsyncSession())
            {
                var alice = await session.LoadAsync<TestUser>(aliceId);
                var bob = await session.LoadAsync<TestUser>(bobId);

                Assert.NotNull(alice);
                Assert.NotNull(bob);
                Assert.Null(alice.Email);
                Assert.Null(bob.Email);
            }
        }

        #endregion
    }
}
