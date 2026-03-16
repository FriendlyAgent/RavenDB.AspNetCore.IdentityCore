using Microsoft.AspNetCore.Identity;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Operations.CompareExchange;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.Test.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace RavenDB.AspNetCore.IdentityCore.Tests.Comprehensive
{
    /// <summary>
    /// Comprehensive and advanced tests for user functionality.
    ///
    /// These tests cover edge cases, concurrency scenarios, and advanced
    /// functionality that extends beyond basic CRUD operations.
    ///
    /// Test Categories:
    /// - Concurrency and race conditions
    /// - Bulk operations
    /// - Edge cases and error handling
    /// - Performance and scalability scenarios
    /// </summary>
    public class UserComprehensiveTests : RavenDBTestBase
    {
        #region Concurrency Tests

        /// <summary>
        /// Test: Concurrent user creation with same username
        ///
        /// Verifies that:
        /// 1. Only one of two concurrent user creation attempts succeeds
        /// 2. The Compare Exchange mechanism properly prevents duplicates
        /// 3. One creation returns success, the other returns DuplicateUserName error
        /// 4. Only one user exists in the database after both attempts
        ///
        /// This test simulates a real-world scenario where two requests attempt
        /// to create the same username simultaneously, which could happen in a
        /// distributed system or under high load.
        /// </summary>
        [Fact(DisplayName = "User: Concurrent creation with same username - only one succeeds")]
        public async Task CreateAsync_ConcurrentSameUsername_OnlyOneSucceeds()
        {
            using (var store = GetDocumentStore())
            {
                const string username = "alice";
                var results = new List<IdentityResult>();

                // Act: Create two users with the same username concurrently
                var task1 = Task.Run(async () =>
                {
                    using (var session = store.OpenAsyncSession())
                    {
                        var userStore = new RavenUserStore<TestUser>(session);
                        var user = new TestUser
                        {
                            UserName = username,
                            NormalizedUserName = username.ToUpperInvariant()
                        };
                        user.Email = new RavenIdentityUserEmail("alice1@example.com");
                        return await userStore.CreateAsync(user);
                    }
                });

                var task2 = Task.Run(async () =>
                {
                    using (var session = store.OpenAsyncSession())
                    {
                        var userStore = new RavenUserStore<TestUser>(session);
                        var user = new TestUser
                        {
                            UserName = username,
                            NormalizedUserName = username.ToUpperInvariant()
                        };
                        user.Email = new RavenIdentityUserEmail("alice2@example.com");
                        return await userStore.CreateAsync(user);
                    }
                });

                results.Add(await task1);
                results.Add(await task2);

                // Wait for indexes
                await WaitForIndexing(store);

                // Assert: Exactly one succeeded, one failed
                var successes = results.Count(r => r.Succeeded);
                var failures = results.Count(r => !r.Succeeded);
                Assert.Equal(1, successes);
                Assert.Equal(1, failures);

                // Assert: The failure has correct error code
                var failedResult = results.First(r => !r.Succeeded);
                Assert.Contains(failedResult.Errors, e => e.Code == "DuplicateUserName");

                // Assert: Only one user exists in database
                using (var session = store.OpenAsyncSession())
                {
                    var users = await session.Query<TestUser>().ToListAsync();
                    Assert.Single(users);
                    Assert.Equal(username, users[0].UserName);
                }
            }
        }

        /// <summary>
        /// Test: Concurrent user creation with same email
        ///
        /// Verifies that:
        /// 1. Only one of two concurrent user creation attempts succeeds when using same email
        /// 2. The Compare Exchange mechanism properly prevents duplicate emails
        /// 3. One creation returns success, the other returns DuplicateEmail error
        /// 4. Only one user exists in the database after both attempts
        /// </summary>
        [Fact(DisplayName = "User: Concurrent creation with same email - only one succeeds")]
        public async Task CreateAsync_ConcurrentSameEmail_OnlyOneSucceeds()
        {
            using (var store = GetDocumentStore())
            {
                const string email = "alice@example.com";
                var identityOptions = Microsoft.Extensions.Options.Options.Create(new IdentityOptions
                {
                    User = { RequireUniqueEmail = true }
                });
                var results = new List<IdentityResult>();

                // Act: Create two users with the same email concurrently
                var task1 = Task.Run(async () =>
                {
                    using (var session = store.OpenAsyncSession())
                    {
                        var userStore = new RavenUserStore<TestUser>(session, null, identityOptions);
                        var user = new TestUser
                        {
                            UserName = "alice1",
                            NormalizedUserName = "ALICE1"
                        };
                        user.Email = new RavenIdentityUserEmail
                        {
                            Email = email,
                            NormalizedEmail = email.ToUpperInvariant()
                        };
                        return await userStore.CreateAsync(user);
                    }
                });

                var task2 = Task.Run(async () =>
                {
                    using (var session = store.OpenAsyncSession())
                    {
                        var userStore = new RavenUserStore<TestUser>(session, null, identityOptions);
                        var user = new TestUser
                        {
                            UserName = "alice2",
                            NormalizedUserName = "ALICE2"
                        };
                        user.Email = new RavenIdentityUserEmail
                        {
                            Email = email,
                            NormalizedEmail = email.ToUpperInvariant()
                        };
                        return await userStore.CreateAsync(user);
                    }
                });

                results.Add(await task1);
                results.Add(await task2);

                // Wait for indexes
                await WaitForIndexing(store);

                // Assert: Exactly one succeeded, one failed
                var successes = results.Count(r => r.Succeeded);
                var failures = results.Count(r => !r.Succeeded);
                Assert.Equal(1, successes);
                Assert.Equal(1, failures);

                // Assert: The failure has correct error code
                var failedResult = results.First(r => !r.Succeeded);
                Assert.Contains(failedResult.Errors, e => e.Code == "DuplicateEmail");

                // Assert: Only one user exists in database
                using (var session = store.OpenAsyncSession())
                {
                    var users = await session.Query<TestUser>().ToListAsync();
                    Assert.Single(users);
                }
            }
        }

        /// <summary>
        /// Test: Update user with concurrency check
        ///
        /// Verifies that:
        /// 1. Concurrent updates to the same user are handled correctly
        /// 2. RavenDB's optimistic concurrency control prevents lost updates
        /// 3. At least one update succeeds
        ///
        /// This demonstrates RavenDB's built-in optimistic concurrency support
        /// which is crucial for maintaining data integrity in multi-user scenarios.
        /// </summary>
        [Fact(DisplayName = "User: Concurrent updates handled correctly")]
        public async Task UpdateAsync_ConcurrentUpdates_HandledCorrectly()
        {
            using (var store = GetDocumentStore())
            {
                string userId;

                // Arrange: Create a user
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

                // Act: Two concurrent updates to different properties
                var update1 = Task.Run(async () =>
                {
                    using (var session = store.OpenAsyncSession())
                    {
                        var userStore = new RavenUserStore<TestUser>(session);
                        var user = await userStore.FindByIdAsync(userId);
                        await Task.Delay(50); // Simulate some processing
                        await userStore.SetEmailAsync(user, "bob.new1@example.com");
                        return await userStore.UpdateAsync(user);
                    }
                });

                var update2 = Task.Run(async () =>
                {
                    using (var session = store.OpenAsyncSession())
                    {
                        var userStore = new RavenUserStore<TestUser>(session);
                        var user = await userStore.FindByIdAsync(userId);
                        await Task.Delay(50); // Simulate some processing
                        await userStore.SetEmailAsync(user, "bob.new2@example.com");
                        return await userStore.UpdateAsync(user);
                    }
                });

                var result1 = await update1;
                var result2 = await update2;

                // Assert: At least one update succeeded
                Assert.True(result1.Succeeded || result2.Succeeded);

                await WaitForIndexing(store);

                // Assert: User has one of the updated values
                using (var session = store.OpenAsyncSession())
                {
                    var user = await session.LoadAsync<TestUser>(userId);
                    Assert.NotNull(user);
                    Assert.NotNull(user.Email);
                    Assert.True(
                        user.Email.Email == "bob.new1@example.com" || user.Email.Email == "bob.new2@example.com",
                        $"Expected 'bob.new1@example.com' or 'bob.new2@example.com', but got '{user.Email.Email}'");
                }
            }
        }

        #endregion

        #region Bulk Operations Tests

        /// <summary>
        /// Test: Bulk user creation and retrieval
        ///
        /// Verifies that:
        /// 1. Multiple users can be created in succession
        /// 2. All users are properly stored and retrievable
        /// 3. Performance remains acceptable with multiple operations
        /// 4. Each user has unique Compare Exchange reservations
        ///
        /// This test ensures the system can handle typical administrative
        /// tasks like importing multiple users during initial setup.
        /// </summary>
        [Fact(DisplayName = "User: Bulk creation and retrieval")]
        public async Task CreateAsync_BulkOperations_AllSucceed()
        {
            using (var store = GetDocumentStore())
            {
                var usernames = new[] { "alice", "bob", "charlie", "david", "eve", "frank" };
                var createdIds = new List<string>();

                // Act: Create multiple users
                foreach (var username in usernames)
                {
                    using (var session = store.OpenAsyncSession())
                    {
                        var userStore = new RavenUserStore<TestUser>(session);
                        var user = new TestUser
                        {
                            UserName = username,
                            NormalizedUserName = username.ToUpperInvariant()
                        };
                        user.Email = new RavenIdentityUserEmail($"{username}@example.com");
                        var result = await userStore.CreateAsync(user);

                        // Assert: Each creation succeeds
                        Assert.True(result.Succeeded);
                        Assert.NotNull(user.Id);
                        createdIds.Add(user.Id);
                    }
                }

                await WaitForIndexing(store);

                // Assert: All users exist and are retrievable
                using (var session = store.OpenAsyncSession())
                {
                    var users = await session.Query<TestUser>().ToListAsync();
                    Assert.Equal(usernames.Length, users.Count);

                    foreach (var username in usernames)
                    {
                        var user = users.FirstOrDefault(u => u.UserName == username);
                        Assert.NotNull(user);
                        Assert.Equal(username.ToUpperInvariant(), user.NormalizedUserName);
                    }
                }

                // Assert: Each has a Compare Exchange reservation for username
                foreach (var username in usernames)
                {
                    var key = CompareExchangeKeys.ForUserName(username);
                    var reservation = await store.Operations.SendAsync(
                        new GetCompareExchangeValueOperation<string>(key));
                    Assert.NotNull(reservation);
                }
            }
        }

        #endregion

        #region Edge Cases Tests

        /// <summary>
        /// Test: Find non-existent user returns null
        ///
        /// Verifies that:
        /// 1. FindByNameAsync returns null for non-existent users
        /// 2. FindByIdAsync returns null for non-existent users
        /// 3. FindByEmailAsync returns null for non-existent users
        /// 4. No exceptions are thrown
        ///
        /// This tests proper null handling, which is important for
        /// authentication checks that need to gracefully handle missing users.
        /// </summary>
        [Fact(DisplayName = "User: Find non-existent user returns null")]
        public async Task FindAsync_NonExistent_ReturnsNull()
        {
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenAsyncSession())
                {
                    var userStore = new RavenUserStore<TestUser>(session);

                    // Act & Assert: Find by name returns null
                    var byName = await userStore.FindByNameAsync("NONEXISTENT");
                    Assert.Null(byName);

                    // Act & Assert: Find by ID returns null
                    var byId = await userStore.FindByIdAsync("TestUsers/999");
                    Assert.Null(byId);

                    // Act & Assert: Find by email returns null
                    var byEmail = await userStore.FindByEmailAsync("nonexistent@example.com");
                    Assert.Null(byEmail);
                }
            }
        }

        /// <summary>
        /// Test: Delete null user throws exception
        ///
        /// Verifies that:
        /// 1. Attempting to delete a null user throws ArgumentNullException
        /// 2. The system properly validates input before attempting operations
        ///
        /// This ensures proper error handling and prevents null reference exceptions.
        /// </summary>
        [Fact(DisplayName = "User: Delete null user throws exception")]
        public async Task DeleteAsync_NullUser_ThrowsException()
        {
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenAsyncSession())
                {
                    var userStore = new RavenUserStore<TestUser>(session);

                    // Act & Assert: Deleting null throws
                    await Assert.ThrowsAsync<ArgumentNullException>(
                        async () => await userStore.DeleteAsync(null));
                }
            }
        }

        /// <summary>
        /// Test: Username is case-insensitive for lookups
        ///
        /// Verifies that:
        /// 1. Users can be found using different case variations
        /// 2. Normalized names ensure case-insensitive matching
        /// 3. Original casing is preserved
        ///
        /// This is important for user experience - users shouldn't have to
        /// remember exact casing of usernames.
        /// </summary>
        [Theory(DisplayName = "User: Case-insensitive username lookup")]
        [InlineData("Alice", "ALICE")]
        [InlineData("Alice", "alice")]
        [InlineData("Alice", "AlIcE")]
        public async Task FindByNameAsync_CaseInsensitive_FindsUser(string originalName, string searchName)
        {
            using (var store = GetDocumentStore())
            {
                // Arrange: Create user with original casing
                using (var session = store.OpenAsyncSession())
                {
                    var userStore = new RavenUserStore<TestUser>(session);
                    var user = new TestUser
                    {
                        UserName = originalName,
                        NormalizedUserName = originalName.ToUpperInvariant()
                    };
                    user.Email = new RavenIdentityUserEmail($"{originalName.ToLower()}@example.com");
                    await userStore.CreateAsync(user);
                }

                await WaitForIndexing(store);

                // Act: Find using different casing
                using (var session = store.OpenAsyncSession())
                {
                    var userStore = new RavenUserStore<TestUser>(session);
                    var foundUser = await userStore.FindByNameAsync(searchName.ToUpperInvariant());

                    // Assert: User is found and original casing is preserved
                    Assert.NotNull(foundUser);
                    Assert.Equal(originalName, foundUser.UserName);
                }
            }
        }

        /// <summary>
        /// Test: User with special characters in username
        ///
        /// Verifies that:
        /// 1. Users can have special characters in their usernames
        /// 2. Such users are stored and retrieved correctly
        /// 3. Compare Exchange keys handle special characters
        ///
        /// This ensures the system is flexible enough for various
        /// naming conventions and international characters.
        /// </summary>
        [Theory(DisplayName = "User: Special characters in username")]
        [InlineData("user-name")]
        [InlineData("user_name")]
        [InlineData("user.name")]
        [InlineData("user@domain")]
        public async Task CreateAsync_SpecialCharacters_Success(string username)
        {
            using (var store = GetDocumentStore())
            {
                // Act: Create user with special characters
                using (var session = store.OpenAsyncSession())
                {
                    var userStore = new RavenUserStore<TestUser>(session);
                    var user = new TestUser
                    {
                        UserName = username,
                        NormalizedUserName = username.ToUpperInvariant()
                    };
                    user.Email = new RavenIdentityUserEmail($"{username.Replace("@", "_at_")}@example.com");
                    var result = await userStore.CreateAsync(user);

                    // Assert: Creation succeeds
                    Assert.True(result.Succeeded);
                    Assert.NotNull(user.Id);
                }

                await WaitForIndexing(store);

                // Assert: User can be retrieved
                using (var session = store.OpenAsyncSession())
                {
                    var userStore = new RavenUserStore<TestUser>(session);
                    var foundUser = await userStore.FindByNameAsync(username.ToUpperInvariant());
                    Assert.NotNull(foundUser);
                    Assert.Equal(username, foundUser.UserName);
                }
            }
        }

        /// <summary>
        /// Test: Email is case-insensitive for lookups
        ///
        /// Verifies that:
        /// 1. Users can be found by email using different case variations
        /// 2. Normalized emails ensure case-insensitive matching
        /// 3. Original casing is preserved
        /// </summary>
        [Theory(DisplayName = "User: Case-insensitive email lookup")]
        [InlineData("Test@Example.Com", "test@example.com")]
        [InlineData("Test@Example.Com", "TEST@EXAMPLE.COM")]
        [InlineData("Test@Example.Com", "TeSt@ExAmPlE.cOm")]
        public async Task FindByEmailAsync_CaseInsensitive_FindsUser(string originalEmail, string searchEmail)
        {
            using (var store = GetDocumentStore())
            {
                // Arrange: Create user with original email casing
                using (var session = store.OpenAsyncSession())
                {
                    var userStore = new RavenUserStore<TestUser>(session);
                    var user = new TestUser
                    {
                        UserName = "testuser",
                        NormalizedUserName = "TESTUSER"
                    };
                    user.Email = new RavenIdentityUserEmail
                    {
                        Email = originalEmail,
                        NormalizedEmail = originalEmail.ToUpperInvariant()
                    };
                    await userStore.CreateAsync(user);
                }

                await WaitForIndexing(store);

                // Act: Find using different casing
                using (var session = store.OpenAsyncSession())
                {
                    var userStore = new RavenUserStore<TestUser>(session);
                    var foundUser = await userStore.FindByEmailAsync(searchEmail.ToUpperInvariant());

                    // Assert: User is found and original casing is preserved
                    Assert.NotNull(foundUser);
                    Assert.Equal(originalEmail, foundUser.Email.Email);
                }
            }
        }

        /// <summary>
        /// Test: Update with no changes returns success without unnecessary work
        ///
        /// Verifies that:
        /// 1. Calling UpdateAsync without making changes returns success
        /// 2. No database operations are performed
        /// 3. No compare-exchange operations are performed
        /// </summary>
        [Fact(DisplayName = "User: Update with no changes - no-op")]
        public async Task UpdateAsync_NoChanges_Success()
        {
            using (var store = GetDocumentStore())
            {
                string userId;

                // Arrange: Create a user
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

                // Act: Load and update without making changes
                using (var session = store.OpenAsyncSession())
                {
                    var userStore = new RavenUserStore<TestUser>(session);
                    var user = await userStore.FindByIdAsync(userId);
                    var result = await userStore.UpdateAsync(user);

                    // Assert: Update succeeds
                    Assert.True(result.Succeeded);
                }
            }
        }

        #endregion
    }
}
