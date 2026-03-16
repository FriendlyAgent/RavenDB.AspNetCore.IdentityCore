using Microsoft.AspNetCore.Identity;
using Raven.Client.Documents;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.Test.Shared;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace RavenDB.AspNetCore.IdentityCore.Tests.Interfaces.Role
{
    /// <summary>
    /// Tests for IRoleClaimStore&lt;TRole&gt; interface.
    ///
    /// Methods Tested:
    /// - GetClaimsAsync(TRole role, CancellationToken cancellationToken)
    /// - AddClaimAsync(TRole role, Claim claim, CancellationToken cancellationToken)
    /// - RemoveClaimAsync(TRole role, Claim claim, CancellationToken cancellationToken)
    ///
    /// Role claims allow attaching metadata/permissions to roles.
    /// These claims are inherited by all users in the role.
    /// </summary>
    public class IRoleClaimStoreTests : RavenDBTestBase
    {
        private async Task<string> CreateRoleAsync(IDocumentStore store, string roleName)
        {
            using var session = store.OpenAsyncSession();
            var roleStore = new RavenRoleStore<TestRole>(session);
            var role = new TestRole
            {
                RoleName = roleName,
                NormalizedRoleName = roleName.ToUpperInvariant()
            };
            await roleStore.CreateAsync(role);
            return role.Id;
        }

        private async Task<string> CreateRoleWithClaimsAsync(IDocumentStore store, string roleName, params Claim[] claims)
        {
            using var session = store.OpenAsyncSession();
            var roleStore = new RavenRoleStore<TestRole>(session);
            var role = new TestRole
            {
                RoleName = roleName,
                NormalizedRoleName = roleName.ToUpperInvariant()
            };
            await roleStore.CreateAsync(role);

            foreach (var claim in claims)
            {
                await roleStore.AddClaimAsync(role, claim);
            }
            await roleStore.UpdateAsync(role);

            return role.Id;
        }

        [Fact(DisplayName = "IRoleClaimStore: AddClaimAsync and GetClaimsAsync")]
        public async Task AddClaimAsync_AndGet_Success()
        {
            using var store = GetDocumentStore();
            var roleId = await CreateRoleAsync(store, "Admin");
            await WaitForIndexing(store);

            // Act: Add claim to role
            using (var session = store.OpenAsyncSession())
            {
                var roleStore = new RavenRoleStore<TestRole>(session);
                var role = await roleStore.FindByIdAsync(roleId);

                await roleStore.AddClaimAsync(role, new Claim("Permission", "ManageUsers"));
                await roleStore.UpdateAsync(role);
            }

            await WaitForIndexing(store);

            // Assert
            using (var session = store.OpenAsyncSession())
            {
                var roleStore = new RavenRoleStore<TestRole>(session);
                var role = await roleStore.FindByIdAsync(roleId);

                var claims = await roleStore.GetClaimsAsync(role);
                Assert.Single(claims);
                Assert.Contains(claims, c => c.Type == "Permission" && c.Value == "ManageUsers");
            }
        }

        [Fact(DisplayName = "IRoleClaimStore: Add multiple claims")]
        public async Task AddMultipleClaims_Success()
        {
            using var store = GetDocumentStore();
            var roleId = await CreateRoleWithClaimsAsync(store, "PowerUser",
                new Claim("Permission", "ManageUsers"),
                new Claim("Permission", "ViewReports"),
                new Claim("Department", "IT"));
            await WaitForIndexing(store);

            // Assert
            using (var session = store.OpenAsyncSession())
            {
                var roleStore = new RavenRoleStore<TestRole>(session);
                var role = await roleStore.FindByIdAsync(roleId);

                var claims = await roleStore.GetClaimsAsync(role);
                Assert.Equal(3, claims.Count);
                Assert.Contains(claims, c => c.Type == "Permission" && c.Value == "ManageUsers");
                Assert.Contains(claims, c => c.Type == "Permission" && c.Value == "ViewReports");
                Assert.Contains(claims, c => c.Type == "Department" && c.Value == "IT");
            }
        }

        [Fact(DisplayName = "IRoleClaimStore: GetClaimsAsync when none exist")]
        public async Task GetClaims_NoClaims_ReturnsEmpty()
        {
            using var store = GetDocumentStore();
            var roleId = await CreateRoleAsync(store, "Guest");
            await WaitForIndexing(store);

            using var session = store.OpenAsyncSession();
            var roleStore = new RavenRoleStore<TestRole>(session);
            var role = await roleStore.FindByIdAsync(roleId);

            var claims = await roleStore.GetClaimsAsync(role);
            Assert.Empty(claims);
        }

        [Fact(DisplayName = "IRoleClaimStore: RemoveClaimAsync")]
        public async Task RemoveClaim_Success()
        {
            using var store = GetDocumentStore();
            var roleId = await CreateRoleWithClaimsAsync(store, "Moderator",
                new Claim("Permission", "DeletePosts"),
                new Claim("Permission", "BanUsers"));
            await WaitForIndexing(store);

            // Act: Remove one claim
            using (var session = store.OpenAsyncSession())
            {
                var roleStore = new RavenRoleStore<TestRole>(session);
                var role = await roleStore.FindByIdAsync(roleId);

                await roleStore.RemoveClaimAsync(role, new Claim("Permission", "DeletePosts"));
                await roleStore.UpdateAsync(role);
            }

            await WaitForIndexing(store);

            // Assert
            using (var session = store.OpenAsyncSession())
            {
                var roleStore = new RavenRoleStore<TestRole>(session);
                var role = await roleStore.FindByIdAsync(roleId);

                var claims = await roleStore.GetClaimsAsync(role);
                Assert.Single(claims);
                Assert.Contains(claims, c => c.Type == "Permission" && c.Value == "BanUsers");
                Assert.DoesNotContain(claims, c => c.Value == "DeletePosts");
            }
        }

        [Fact(DisplayName = "IRoleClaimStore: RemoveClaimAsync - All claims")]
        public async Task RemoveAllClaims_Success()
        {
            using var store = GetDocumentStore();
            var roleId = await CreateRoleWithClaimsAsync(store, "Developer",
                new Claim("Permission", "DeployCode"),
                new Claim("Permission", "AccessDatabase"),
                new Claim("Department", "Engineering"));
            await WaitForIndexing(store);

            // Act: Remove all claims
            using (var session = store.OpenAsyncSession())
            {
                var roleStore = new RavenRoleStore<TestRole>(session);
                var role = await roleStore.FindByIdAsync(roleId);

                var allClaims = await roleStore.GetClaimsAsync(role);
                foreach (var claim in allClaims)
                {
                    await roleStore.RemoveClaimAsync(role, claim);
                }
                await roleStore.UpdateAsync(role);
            }

            await WaitForIndexing(store);

            // Assert
            using (var session = store.OpenAsyncSession())
            {
                var roleStore = new RavenRoleStore<TestRole>(session);
                var role = await roleStore.FindByIdAsync(roleId);

                var claims = await roleStore.GetClaimsAsync(role);
                Assert.Empty(claims);
            }
        }

        [Fact(DisplayName = "IRoleClaimStore: AddClaimAsync - Duplicate throws exception")]
        public async Task AddDuplicateClaim_ThrowsException()
        {
            using var store = GetDocumentStore();
            var roleId = await CreateRoleWithClaimsAsync(store, "Manager",
                new Claim("Permission", "ApproveExpenses"));
            await WaitForIndexing(store);

            // Act & Assert
            using var session = store.OpenAsyncSession();
            var roleStore = new RavenRoleStore<TestRole>(session);
            var role = await roleStore.FindByIdAsync(roleId);

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await roleStore.AddClaimAsync(role, new Claim("Permission", "ApproveExpenses"));
            });
        }
    }
}
