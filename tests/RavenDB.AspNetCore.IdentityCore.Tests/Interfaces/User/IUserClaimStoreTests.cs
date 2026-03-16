using Microsoft.AspNetCore.Identity;
using Raven.Client.Documents;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.Test.Shared;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace RavenDB.AspNetCore.IdentityCore.Tests.Interfaces.User
{
    /// <summary>
    /// Tests for IUserClaimStore&lt;TUser&gt; interface.
    ///
    /// Methods Tested:
    /// - GetClaimsAsync, AddClaimsAsync
    /// - ReplaceClaimAsync, RemoveClaimsAsync
    /// - GetUsersForClaimAsync
    ///
    /// Original: Tests for user claims functionality.
    ///
    /// Test Categories:
    /// - Claim storage (IUserClaimStore)
    /// - Adding and removing claims
    /// - Finding users by claim
    /// - Replacing claims
    /// </summary>
    public class UserClaimsTests : RavenDBTestBase
    {
        #region Claim Storage Tests

        /// <summary>
        /// Test: Add claim to user
        ///
        /// Verifies:
        /// - Claim can be added to user
        /// - Claim can be retrieved
        /// </summary>
        [Fact(DisplayName = "User: Add claim")]
        public async Task AddClaim_Success()
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

            // Act: Add claim
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var claim = new Claim("Department", "Engineering");
                await userStore.AddClaimsAsync(user, new[] { claim });
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Claim is retrievable
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var claims = await userStore.GetClaimsAsync(user);
                Assert.Single(claims);
                Assert.Contains(claims, c => c.Type == "Department" && c.Value == "Engineering");
            }
        }

        /// <summary>
        /// Test: Add multiple claims to user
        ///
        /// Verifies:
        /// - Multiple claims can be added
        /// - All claims are stored
        /// </summary>
        [Fact(DisplayName = "User: Add multiple claims")]
        public async Task AddMultipleClaims_Success()
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

            // Act: Add multiple claims
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var claims = new[]
                {
                    new Claim("Department", "Engineering"),
                    new Claim("Title", "Senior Developer"),
                    new Claim("Level", "L5")
                };

                await userStore.AddClaimsAsync(user, claims);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: All claims are retrievable
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var retrievedClaims = await userStore.GetClaimsAsync(user);
                Assert.Equal(3, retrievedClaims.Count);
                Assert.Contains(retrievedClaims, c => c.Type == "Department" && c.Value == "Engineering");
                Assert.Contains(retrievedClaims, c => c.Type == "Title" && c.Value == "Senior Developer");
                Assert.Contains(retrievedClaims, c => c.Type == "Level" && c.Value == "L5");
            }
        }

        /// <summary>
        /// Test: Get claims when user has none
        ///
        /// Verifies:
        /// - GetClaims returns empty list for users without claims
        /// - No exception is thrown
        /// </summary>
        [Fact(DisplayName = "User: Get claims when none exist")]
        public async Task GetClaims_NoClaims_ReturnsEmpty()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user without claims
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

            // Act & Assert: Get claims returns empty
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var claims = await userStore.GetClaimsAsync(user);
                Assert.Empty(claims);
            }
        }

        #endregion

        #region Claim Removal Tests

        /// <summary>
        /// Test: Remove claim from user
        ///
        /// Verifies:
        /// - Claim can be removed
        /// - Other claims remain
        /// </summary>
        [Fact(DisplayName = "User: Remove claim")]
        public async Task RemoveClaim_Success()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user with claims
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

                var claims = new[]
                {
                    new Claim("Department", "Engineering"),
                    new Claim("Title", "Developer")
                };

                await userStore.AddClaimsAsync(user, claims);
                await userStore.UpdateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Remove one claim
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var claimToRemove = new Claim("Department", "Engineering");
                await userStore.RemoveClaimsAsync(user, new[] { claimToRemove });
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Claim is removed, other remains
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var claims = await userStore.GetClaimsAsync(user);
                Assert.Single(claims);
                Assert.Contains(claims, c => c.Type == "Title" && c.Value == "Developer");
                Assert.DoesNotContain(claims, c => c.Type == "Department");
            }
        }

        /// <summary>
        /// Test: Remove all claims from user
        ///
        /// Verifies:
        /// - All claims can be removed
        /// - User has no claims after removal
        /// </summary>
        [Fact(DisplayName = "User: Remove all claims")]
        public async Task RemoveAllClaims_Success()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user with multiple claims
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

                var claims = new[]
                {
                    new Claim("Department", "Engineering"),
                    new Claim("Title", "Developer"),
                    new Claim("Level", "L4")
                };

                await userStore.AddClaimsAsync(user, claims);
                await userStore.UpdateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Remove all claims
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var claimsToRemove = await userStore.GetClaimsAsync(user);
                await userStore.RemoveClaimsAsync(user, claimsToRemove);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: No claims remain
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var claims = await userStore.GetClaimsAsync(user);
                Assert.Empty(claims);
            }
        }

        #endregion

        #region Replace Claim Tests

        /// <summary>
        /// Test: Replace claim value
        ///
        /// Verifies:
        /// - Claim value can be updated
        /// - Old value is replaced with new value
        /// </summary>
        [Fact(DisplayName = "User: Replace claim")]
        public async Task ReplaceClaim_Success()
        {
            using var store = GetDocumentStore();
            string userId;

            // Arrange: Create user with claim
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

                var claim = new Claim("Title", "Junior Developer");
                await userStore.AddClaimsAsync(user, new[] { claim });
                await userStore.UpdateAsync(user);
                userId = user.Id;
            }

            await WaitForIndexing(store);

            // Act: Replace claim
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var oldClaim = new Claim("Title", "Junior Developer");
                var newClaim = new Claim("Title", "Senior Developer");
                await userStore.ReplaceClaimAsync(user, oldClaim, newClaim);
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Assert: Claim is updated
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var user = await userStore.FindByIdAsync(userId);

                var claims = await userStore.GetClaimsAsync(user);
                Assert.Single(claims);
                Assert.Contains(claims, c => c.Type == "Title" && c.Value == "Senior Developer");
                Assert.DoesNotContain(claims, c => c.Value == "Junior Developer");
            }
        }

        #endregion

        #region Find Users By Claim Tests

        /// <summary>
        /// Test: Find users by claim
        ///
        /// Verifies:
        /// - Can find all users with a specific claim
        /// - Users without the claim are not returned
        /// </summary>
        [Fact(DisplayName = "User: Find users by claim")]
        public async Task GetUsersForClaim_Success()
        {
            using var store = GetDocumentStore();

            // Arrange: Create users with various claims
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);

                // User 1: Has Department=Engineering claim
                var user1 = new TestUser
                {
                    UserName = "alice",
                    NormalizedUserName = "ALICE"
                };
                user1.Email = new RavenIdentityUserEmail("alice@example.com");
                await userStore.CreateAsync(user1);
                await userStore.AddClaimsAsync(user1, new[] { new Claim("Department", "Engineering") });
                await userStore.UpdateAsync(user1);

                // User 2: Has Department=Engineering claim
                var user2 = new TestUser
                {
                    UserName = "bob",
                    NormalizedUserName = "BOB"
                };
                user2.Email = new RavenIdentityUserEmail("bob@example.com");
                await userStore.CreateAsync(user2);
                await userStore.AddClaimsAsync(user2, new[] { new Claim("Department", "Engineering") });
                await userStore.UpdateAsync(user2);

                // User 3: Has Department=Sales claim
                var user3 = new TestUser
                {
                    UserName = "charlie",
                    NormalizedUserName = "CHARLIE"
                };
                user3.Email = new RavenIdentityUserEmail("charlie@example.com");
                await userStore.CreateAsync(user3);
                await userStore.AddClaimsAsync(user3, new[] { new Claim("Department", "Sales") });
                await userStore.UpdateAsync(user3);
            }

            await WaitForIndexing(store);

            // Act: Find users with Department=Engineering claim
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var claim = new Claim("Department", "Engineering");
                var users = await userStore.GetUsersForClaimAsync(claim);

                // Assert: Only users with Engineering department are returned
                Assert.Equal(2, users.Count);
                Assert.Contains(users, u => u.UserName == "alice");
                Assert.Contains(users, u => u.UserName == "bob");
                Assert.DoesNotContain(users, u => u.UserName == "charlie");
            }
        }

        /// <summary>
        /// Test: Find users by claim returns empty when no matches
        ///
        /// Verifies:
        /// - Empty list returned when no users have the claim
        /// - No exception is thrown
        /// </summary>
        [Fact(DisplayName = "User: Find users by claim with no matches")]
        public async Task GetUsersForClaim_NoMatches_ReturnsEmpty()
        {
            using var store = GetDocumentStore();

            // Arrange: Create user with different claim
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
                await userStore.AddClaimsAsync(user, new[] { new Claim("Department", "Engineering") });
                await userStore.UpdateAsync(user);
            }

            await WaitForIndexing(store);

            // Act: Find users with non-existent claim
            using (var session = store.OpenAsyncSession())
            {
                var userStore = new RavenUserStore<TestUser>(session);
                var claim = new Claim("Department", "Marketing");
                var users = await userStore.GetUsersForClaimAsync(claim);

                // Assert: Empty list
                Assert.Empty(users);
            }
        }

        #endregion
    }
}
