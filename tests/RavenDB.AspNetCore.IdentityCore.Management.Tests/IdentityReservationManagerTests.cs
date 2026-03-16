using Raven.Client.Documents;
using Raven.Client.Documents.Operations.CompareExchange;
using RavenDB.AspNetCore.IdentityCore;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.AspNetCore.IdentityCore.Management;
using RavenDB.AspNetCore.IdentityCore.Management.Models;
using RavenDB.Test.Shared;
using Xunit;

namespace RavenDB.AspNetCore.IdentityCore.Management.Tests
{
    public class IdentityUserReservationManagerTests : RavenDBTestBase
    {
        // ==================== HELPER METHODS ====================

        private async Task<TestUser> CreateUserWithReservationsAsync(
            IDocumentStore store,
            string userName,
            string email)
        {
            var user = new TestUser
            {
                UserName = userName,
                NormalizedUserName = userName.ToUpperInvariant(),
                Email = new RavenIdentityUserEmail(email)
            };
            user.Email.NormalizedEmail = email.ToUpperInvariant();

            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(user);
                await session.SaveChangesAsync();
            }

            await store.Operations.SendAsync(
                new PutCompareExchangeValueOperation<string>(
                    CompareExchangeKeys.ForUserName(user.NormalizedUserName), user.Id, 0));

            await store.Operations.SendAsync(
                new PutCompareExchangeValueOperation<string>(
                    CompareExchangeKeys.ForEmail(user.Email.NormalizedEmail), user.Id, 0));

            return user;
        }

        private async Task CreateOrphanedReservationAsync(IDocumentStore store, string ceKey, string fakeDocId)
        {
            await store.Operations.SendAsync(
                new PutCompareExchangeValueOperation<string>(ceKey, fakeDocId, 0));
        }

        private async Task CreatePendingReservationAsync(IDocumentStore store, string ceKey)
        {
            await store.Operations.SendAsync(
                new PutCompareExchangeValueOperation<string>(ceKey, "pending", 0));
        }

        // ==================== DIAGNOSTICS ====================

        [Fact(DisplayName = "User: Empty database returns empty healthy report")]
        public async Task GetReportAsync_EmptyDatabase_ReturnsEmptyReport()
        {
            using var store = GetDocumentStore();
            var manager = new IdentityUserReservationManager<TestUser>(store);

            var report = await manager.GetReportAsync();

            Assert.True(report.IsHealthy);
            Assert.Empty(report.Healthy);
            Assert.Empty(report.Orphaned);
            Assert.Empty(report.Pending);
            Assert.Empty(report.Missing);
            Assert.Equal(0, report.TotalReservations);
            Assert.Equal(0, report.TotalDocuments);
        }

        [Fact(DisplayName = "User: Correct reservations are healthy")]
        public async Task GetReportAsync_CorrectReservations_AllHealthy()
        {
            using var store = GetDocumentStore();
            await CreateUserWithReservationsAsync(store, "alice", "alice@example.com");
            await WaitForIndexing(store);

            var manager = new IdentityUserReservationManager<TestUser>(store);
            var report = await manager.GetReportAsync();

            Assert.True(report.IsHealthy);
            Assert.Equal(2, report.Healthy.Count); // username + email
            Assert.Empty(report.Orphaned);
            Assert.Empty(report.Pending);
            Assert.Empty(report.Missing);
        }

        [Fact(DisplayName = "User: Detects orphaned reservations")]
        public async Task GetReportAsync_OrphanedReservation_Detected()
        {
            using var store = GetDocumentStore();

            await CreateOrphanedReservationAsync(store,
                CompareExchangeKeys.ForUserName("GHOST"), "TestUsers/999");

            var manager = new IdentityUserReservationManager<TestUser>(store);
            var report = await manager.GetReportAsync();

            Assert.False(report.IsHealthy);
            Assert.Single(report.Orphaned);
            Assert.Equal(ReservationType.UserName, report.Orphaned[0].Type);
            Assert.Equal("ghost", report.Orphaned[0].ReservedValue);
            Assert.Equal("TestUsers/999", report.Orphaned[0].OwnerId);
        }

        [Fact(DisplayName = "User: Detects pending reservations")]
        public async Task GetReportAsync_PendingReservation_Detected()
        {
            using var store = GetDocumentStore();

            await CreatePendingReservationAsync(store,
                CompareExchangeKeys.ForEmail("PENDING@EXAMPLE.COM"));

            var manager = new IdentityUserReservationManager<TestUser>(store);
            var report = await manager.GetReportAsync();

            Assert.False(report.IsHealthy);
            Assert.Single(report.Pending);
            Assert.Equal(ReservationType.Email, report.Pending[0].Type);
            Assert.Equal("pending", report.Pending[0].OwnerId);
        }

        [Fact(DisplayName = "User: Pending reservation with matching document gets document ID")]
        public async Task GetReportAsync_PendingWithDocument_DetectedWithDocId()
        {
            using var store = GetDocumentStore();

            var user = new TestUser
            {
                UserName = "bob",
                NormalizedUserName = "BOB",
                Email = new RavenIdentityUserEmail("bob@example.com")
            };
            user.Email.NormalizedEmail = "BOB@EXAMPLE.COM";

            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(user);
                await session.SaveChangesAsync();
            }

            await CreatePendingReservationAsync(store, CompareExchangeKeys.ForUserName("BOB"));
            await WaitForIndexing(store);

            var manager = new IdentityUserReservationManager<TestUser>(store);
            var report = await manager.GetReportAsync();

            Assert.Single(report.Pending);
            Assert.Equal(user.Id, report.Pending[0].DocumentId);
        }

        [Fact(DisplayName = "User: Detects missing reservations")]
        public async Task GetReportAsync_MissingReservation_Detected()
        {
            using var store = GetDocumentStore();

            var user = new TestUser
            {
                UserName = "charlie",
                NormalizedUserName = "CHARLIE",
                Email = new RavenIdentityUserEmail("charlie@example.com")
            };
            user.Email.NormalizedEmail = "CHARLIE@EXAMPLE.COM";

            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(user);
                await session.SaveChangesAsync();
            }

            await WaitForIndexing(store);

            var manager = new IdentityUserReservationManager<TestUser>(store);
            var report = await manager.GetReportAsync();

            Assert.False(report.IsHealthy);
            Assert.Equal(2, report.Missing.Count); // username + email
            Assert.Contains(report.Missing, e => e.Type == ReservationType.UserName);
            Assert.Contains(report.Missing, e => e.Type == ReservationType.Email);
        }

        [Fact(DisplayName = "User: Mixed state with all categories")]
        public async Task GetReportAsync_MixedState_AllCategoriesPresent()
        {
            using var store = GetDocumentStore();

            // Healthy
            await CreateUserWithReservationsAsync(store, "healthy", "healthy@example.com");

            // Orphaned
            await CreateOrphanedReservationAsync(store,
                CompareExchangeKeys.ForUserName("DELETED"), "TestUsers/deleted");

            // Pending
            await CreatePendingReservationAsync(store,
                CompareExchangeKeys.ForEmail("STUCK@EXAMPLE.COM"));

            // Missing
            using (var session = store.OpenAsyncSession())
            {
                var missing = new TestUser
                {
                    UserName = "noreservation",
                    NormalizedUserName = "NORESERVATION",
                    Email = new RavenIdentityUserEmail("noreservation@example.com")
                };
                missing.Email.NormalizedEmail = "NORESERVATION@EXAMPLE.COM";
                await session.StoreAsync(missing);
                await session.SaveChangesAsync();
            }

            await WaitForIndexing(store);

            var manager = new IdentityUserReservationManager<TestUser>(store);
            var report = await manager.GetReportAsync();

            Assert.False(report.IsHealthy);
            Assert.Equal(2, report.Healthy.Count);
            Assert.Single(report.Orphaned);
            Assert.Single(report.Pending);
            Assert.Equal(2, report.Missing.Count);
        }

        // ==================== REPAIR ====================

        [Fact(DisplayName = "User: Removes orphaned reservations")]
        public async Task RemoveOrphanedReservationsAsync_RemovesOrphans()
        {
            using var store = GetDocumentStore();

            await CreateOrphanedReservationAsync(store,
                CompareExchangeKeys.ForUserName("ORPHAN1"), "TestUsers/gone1");
            await CreateOrphanedReservationAsync(store,
                CompareExchangeKeys.ForEmail("ORPHAN@GONE.COM"), "TestUsers/gone2");

            var manager = new IdentityUserReservationManager<TestUser>(store);
            var removed = await manager.RemoveOrphanedReservationsAsync();

            Assert.Equal(2, removed);

            var report = await manager.GetReportAsync();
            Assert.Empty(report.Orphaned);
        }

        [Fact(DisplayName = "User: Repairs pending — updates with document, removes without")]
        public async Task RepairPendingReservationsAsync_FixesPending()
        {
            using var store = GetDocumentStore();

            var user = new TestUser
            {
                UserName = "pendinguser",
                NormalizedUserName = "PENDINGUSER"
            };

            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(user);
                await session.SaveChangesAsync();
            }

            await CreatePendingReservationAsync(store, CompareExchangeKeys.ForUserName("PENDINGUSER"));
            await CreatePendingReservationAsync(store, CompareExchangeKeys.ForEmail("NOBODY@EXAMPLE.COM"));
            await WaitForIndexing(store);

            var manager = new IdentityUserReservationManager<TestUser>(store);
            var repaired = await manager.RepairPendingReservationsAsync();

            Assert.Equal(2, repaired);

            var usernameReservation = await store.Operations.SendAsync(
                new GetCompareExchangeValueOperation<string>(
                    CompareExchangeKeys.ForUserName("PENDINGUSER")));
            Assert.NotNull(usernameReservation);
            Assert.Equal(user.Id, usernameReservation.Value);

            var emailReservation = await store.Operations.SendAsync(
                new GetCompareExchangeValueOperation<string>(
                    CompareExchangeKeys.ForEmail("NOBODY@EXAMPLE.COM")));
            Assert.Null(emailReservation);
        }

        [Fact(DisplayName = "User: Creates missing reservations")]
        public async Task CreateMissingReservationsAsync_CreatesReservations()
        {
            using var store = GetDocumentStore();

            var user = new TestUser
            {
                UserName = "norsvp",
                NormalizedUserName = "NORSVP",
                Email = new RavenIdentityUserEmail("norsvp@example.com")
            };
            user.Email.NormalizedEmail = "NORSVP@EXAMPLE.COM";

            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(user);
                await session.SaveChangesAsync();
            }

            await WaitForIndexing(store);

            var manager = new IdentityUserReservationManager<TestUser>(store);
            var created = await manager.CreateMissingReservationsAsync();

            Assert.Equal(2, created);

            var usernameRes = await store.Operations.SendAsync(
                new GetCompareExchangeValueOperation<string>(
                    CompareExchangeKeys.ForUserName("NORSVP")));
            Assert.NotNull(usernameRes);
            Assert.Equal(user.Id, usernameRes.Value);

            var emailRes = await store.Operations.SendAsync(
                new GetCompareExchangeValueOperation<string>(
                    CompareExchangeKeys.ForEmail("NORSVP@EXAMPLE.COM")));
            Assert.NotNull(emailRes);
            Assert.Equal(user.Id, emailRes.Value);
        }

        [Fact(DisplayName = "User: RepairAll fixes all issues")]
        public async Task RepairAllAsync_FixesEverything()
        {
            using var store = GetDocumentStore();

            await CreateOrphanedReservationAsync(store,
                CompareExchangeKeys.ForUserName("ORPHAN"), "TestUsers/gone");

            await CreatePendingReservationAsync(store,
                CompareExchangeKeys.ForEmail("STALE@EXAMPLE.COM"));

            var user = new TestUser
            {
                UserName = "fixme",
                NormalizedUserName = "FIXME",
                Email = new RavenIdentityUserEmail("fixme@example.com")
            };
            user.Email.NormalizedEmail = "FIXME@EXAMPLE.COM";

            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(user);
                await session.SaveChangesAsync();
            }

            await WaitForIndexing(store);

            var manager = new IdentityUserReservationManager<TestUser>(store);
            var report = await manager.RepairAllAsync();

            Assert.Single(report.Orphaned);
            Assert.Single(report.Pending);
            Assert.Equal(2, report.Missing.Count);

            var afterReport = await manager.GetReportAsync();
            Assert.True(afterReport.IsHealthy);
            Assert.Equal(2, afterReport.Healthy.Count);
        }

        // ==================== MIGRATION ====================

        [Fact(DisplayName = "User: EnableConstraints creates all reservations")]
        public async Task EnableConstraintsAsync_CreatesAllReservations()
        {
            using var store = GetDocumentStore();

            using (var session = store.OpenAsyncSession())
            {
                var user = new TestUser
                {
                    UserName = "migrateuser",
                    NormalizedUserName = "MIGRATEUSER",
                    Email = new RavenIdentityUserEmail("migrate@example.com")
                };
                user.Email.NormalizedEmail = "MIGRATE@EXAMPLE.COM";
                await session.StoreAsync(user);
                await session.SaveChangesAsync();
            }

            await WaitForIndexing(store);

            var manager = new IdentityUserReservationManager<TestUser>(store);
            var report = await manager.EnableConstraintsAsync();

            Assert.Equal(2, report.Missing.Count); // username + email

            var afterReport = await manager.GetReportAsync();
            Assert.True(afterReport.IsHealthy);
            Assert.Equal(2, afterReport.Healthy.Count);
        }

        [Fact(DisplayName = "User: DisableConstraints removes all user reservations")]
        public async Task DisableConstraintsAsync_RemovesAllReservations()
        {
            using var store = GetDocumentStore();

            await CreateUserWithReservationsAsync(store, "removeuser", "remove@example.com");
            await WaitForIndexing(store);

            var manager = new IdentityUserReservationManager<TestUser>(store);

            var before = await manager.GetReportAsync();
            Assert.Equal(2, before.TotalReservations);

            var removed = await manager.DisableConstraintsAsync();
            Assert.Equal(2, removed);

            var after = await manager.GetReportAsync();
            Assert.Equal(0, after.TotalReservations);
            Assert.Equal(2, after.Missing.Count);
        }

        // ==================== ISOLATION ====================

        [Fact(DisplayName = "User: Ignores role reservations completely")]
        public async Task UserManager_IgnoresRoleReservations()
        {
            using var store = GetDocumentStore();

            await CreateUserWithReservationsAsync(store, "useronly", "useronly@example.com");

            // Create role reservation — should be invisible to user manager
            var role = new TestRole
            {
                RoleName = "IgnoredRole",
                NormalizedRoleName = "IGNOREDROLE"
            };

            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(role);
                await session.SaveChangesAsync();
            }

            await store.Operations.SendAsync(
                new PutCompareExchangeValueOperation<string>(
                    CompareExchangeKeys.ForRoleName(role.NormalizedRoleName), role.Id, 0));

            await WaitForIndexing(store);

            var manager = new IdentityUserReservationManager<TestUser>(store);
            var report = await manager.GetReportAsync();

            Assert.True(report.IsHealthy);
            Assert.Equal(2, report.Healthy.Count);
            Assert.DoesNotContain(report.Healthy, e => e.Type == ReservationType.RoleName);
        }

        [Fact(DisplayName = "User: DisableConstraints does not touch role reservations")]
        public async Task UserManager_DisableConstraints_KeepsRoleReservations()
        {
            using var store = GetDocumentStore();

            await CreateUserWithReservationsAsync(store, "u1", "u1@example.com");

            await store.Operations.SendAsync(
                new PutCompareExchangeValueOperation<string>(
                    CompareExchangeKeys.ForRoleName("ROLETOKEEP"), "TestRoles/1", 0));

            var manager = new IdentityUserReservationManager<TestUser>(store);
            var removed = await manager.DisableConstraintsAsync();
            Assert.Equal(2, removed);

            // Role reservation untouched
            var roleReservation = await store.Operations.SendAsync(
                new GetCompareExchangeValueOperation<string>(
                    CompareExchangeKeys.ForRoleName("ROLETOKEEP")));
            Assert.NotNull(roleReservation);
        }

        // ==================== EDGE CASES ====================

        [Fact(DisplayName = "User: Without email only has username reservation")]
        public async Task GetReportAsync_UserWithoutEmail_OnlyUsernameExpected()
        {
            using var store = GetDocumentStore();

            using (var session = store.OpenAsyncSession())
            {
                var user = new TestUser
                {
                    UserName = "noemail",
                    NormalizedUserName = "NOEMAIL"
                };
                await session.StoreAsync(user);
                await session.SaveChangesAsync();
            }

            await WaitForIndexing(store);

            var manager = new IdentityUserReservationManager<TestUser>(store);
            var report = await manager.GetReportAsync();

            Assert.Single(report.Missing);
            Assert.Equal(ReservationType.UserName, report.Missing[0].Type);
        }

        [Fact(DisplayName = "User: CreateMissing is idempotent")]
        public async Task CreateMissingReservationsAsync_Idempotent()
        {
            using var store = GetDocumentStore();

            var user = await CreateUserWithReservationsAsync(store, "existing", "existing@example.com");
            await WaitForIndexing(store);

            var before = await store.Operations.SendAsync(
                new GetCompareExchangeValueOperation<string>(
                    CompareExchangeKeys.ForUserName("EXISTING")));

            var manager = new IdentityUserReservationManager<TestUser>(store);
            var created = await manager.CreateMissingReservationsAsync();

            Assert.Equal(0, created);

            var after = await store.Operations.SendAsync(
                new GetCompareExchangeValueOperation<string>(
                    CompareExchangeKeys.ForUserName("EXISTING")));
            Assert.Equal(before.Index, after.Index);
        }
    }

    public class IdentityRoleReservationManagerTests : RavenDBTestBase
    {
        // ==================== HELPER METHODS ====================

        private async Task<TestRole> CreateRoleWithReservationAsync(
            IDocumentStore store,
            string roleName)
        {
            var role = new TestRole
            {
                RoleName = roleName,
                NormalizedRoleName = roleName.ToUpperInvariant()
            };

            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(role);
                await session.SaveChangesAsync();
            }

            await store.Operations.SendAsync(
                new PutCompareExchangeValueOperation<string>(
                    CompareExchangeKeys.ForRoleName(role.NormalizedRoleName), role.Id, 0));

            return role;
        }

        private async Task CreateOrphanedReservationAsync(IDocumentStore store, string ceKey, string fakeDocId)
        {
            await store.Operations.SendAsync(
                new PutCompareExchangeValueOperation<string>(ceKey, fakeDocId, 0));
        }

        private async Task CreatePendingReservationAsync(IDocumentStore store, string ceKey)
        {
            await store.Operations.SendAsync(
                new PutCompareExchangeValueOperation<string>(ceKey, "pending", 0));
        }

        // ==================== DIAGNOSTICS ====================

        [Fact(DisplayName = "Role: Empty database returns empty healthy report")]
        public async Task GetReportAsync_EmptyDatabase_ReturnsEmptyReport()
        {
            using var store = GetDocumentStore();
            var manager = new IdentityRoleReservationManager<TestRole>(store);

            var report = await manager.GetReportAsync();

            Assert.True(report.IsHealthy);
            Assert.Empty(report.Healthy);
            Assert.Equal(0, report.TotalReservations);
        }

        [Fact(DisplayName = "Role: Correct reservations are healthy")]
        public async Task GetReportAsync_CorrectReservations_Healthy()
        {
            using var store = GetDocumentStore();
            await CreateRoleWithReservationAsync(store, "Admin");
            await WaitForIndexing(store);

            var manager = new IdentityRoleReservationManager<TestRole>(store);
            var report = await manager.GetReportAsync();

            Assert.True(report.IsHealthy);
            Assert.Single(report.Healthy);
            Assert.Equal(ReservationType.RoleName, report.Healthy[0].Type);
        }

        [Fact(DisplayName = "Role: Detects orphaned reservation")]
        public async Task GetReportAsync_OrphanedReservation_Detected()
        {
            using var store = GetDocumentStore();

            await CreateOrphanedReservationAsync(store,
                CompareExchangeKeys.ForRoleName("GHOSTROLE"), "TestRoles/999");

            var manager = new IdentityRoleReservationManager<TestRole>(store);
            var report = await manager.GetReportAsync();

            Assert.False(report.IsHealthy);
            Assert.Single(report.Orphaned);
            Assert.Equal(ReservationType.RoleName, report.Orphaned[0].Type);
        }

        [Fact(DisplayName = "Role: Detects missing reservation")]
        public async Task GetReportAsync_MissingReservation_Detected()
        {
            using var store = GetDocumentStore();

            using (var session = store.OpenAsyncSession())
            {
                var role = new TestRole
                {
                    RoleName = "NoReservation",
                    NormalizedRoleName = "NORESERVATION"
                };
                await session.StoreAsync(role);
                await session.SaveChangesAsync();
            }

            await WaitForIndexing(store);

            var manager = new IdentityRoleReservationManager<TestRole>(store);
            var report = await manager.GetReportAsync();

            Assert.False(report.IsHealthy);
            Assert.Single(report.Missing);
            Assert.Equal(ReservationType.RoleName, report.Missing[0].Type);
        }

        // ==================== REPAIR ====================

        [Fact(DisplayName = "Role: RepairAll fixes all issues")]
        public async Task RepairAllAsync_FixesEverything()
        {
            using var store = GetDocumentStore();

            // Orphaned
            await CreateOrphanedReservationAsync(store,
                CompareExchangeKeys.ForRoleName("DEADROLE"), "TestRoles/gone");

            // Missing
            using (var session = store.OpenAsyncSession())
            {
                var role = new TestRole
                {
                    RoleName = "FixRole",
                    NormalizedRoleName = "FIXROLE"
                };
                await session.StoreAsync(role);
                await session.SaveChangesAsync();
            }

            await WaitForIndexing(store);

            var manager = new IdentityRoleReservationManager<TestRole>(store);
            var report = await manager.RepairAllAsync();

            Assert.Single(report.Orphaned);
            Assert.Single(report.Missing);

            var afterReport = await manager.GetReportAsync();
            Assert.True(afterReport.IsHealthy);
            Assert.Single(afterReport.Healthy);
        }

        // ==================== MIGRATION ====================

        [Fact(DisplayName = "Role: EnableConstraints creates all reservations")]
        public async Task EnableConstraintsAsync_CreatesReservations()
        {
            using var store = GetDocumentStore();

            using (var session = store.OpenAsyncSession())
            {
                var role = new TestRole
                {
                    RoleName = "MigrateRole",
                    NormalizedRoleName = "MIGRATEROLE"
                };
                await session.StoreAsync(role);
                await session.SaveChangesAsync();
            }

            await WaitForIndexing(store);

            var manager = new IdentityRoleReservationManager<TestRole>(store);
            var report = await manager.EnableConstraintsAsync();

            Assert.Single(report.Missing);

            var afterReport = await manager.GetReportAsync();
            Assert.True(afterReport.IsHealthy);
        }

        [Fact(DisplayName = "Role: DisableConstraints removes all role reservations")]
        public async Task DisableConstraintsAsync_RemovesAllReservations()
        {
            using var store = GetDocumentStore();

            await CreateRoleWithReservationAsync(store, "RemoveRole");
            await WaitForIndexing(store);

            var manager = new IdentityRoleReservationManager<TestRole>(store);
            var removed = await manager.DisableConstraintsAsync();
            Assert.Equal(1, removed);

            var after = await manager.GetReportAsync();
            Assert.Equal(0, after.TotalReservations);
        }

        // ==================== ISOLATION ====================

        [Fact(DisplayName = "Role: Ignores user reservations completely")]
        public async Task RoleManager_IgnoresUserReservations()
        {
            using var store = GetDocumentStore();

            // Create user reservation — should be invisible to role manager
            await store.Operations.SendAsync(
                new PutCompareExchangeValueOperation<string>(
                    CompareExchangeKeys.ForUserName("SOMEUSER"), "TestUsers/1", 0));

            await CreateRoleWithReservationAsync(store, "VisibleRole");
            await WaitForIndexing(store);

            var manager = new IdentityRoleReservationManager<TestRole>(store);
            var report = await manager.GetReportAsync();

            Assert.True(report.IsHealthy);
            Assert.Single(report.Healthy);
            Assert.Equal(ReservationType.RoleName, report.Healthy[0].Type);
        }

        [Fact(DisplayName = "Role: DisableConstraints does not touch user reservations")]
        public async Task RoleManager_DisableConstraints_KeepsUserReservations()
        {
            using var store = GetDocumentStore();

            await CreateRoleWithReservationAsync(store, "RemoveMe");

            await store.Operations.SendAsync(
                new PutCompareExchangeValueOperation<string>(
                    CompareExchangeKeys.ForUserName("KEEPME"), "TestUsers/1", 0));

            var manager = new IdentityRoleReservationManager<TestRole>(store);
            var removed = await manager.DisableConstraintsAsync();
            Assert.Equal(1, removed);

            // User reservation untouched
            var userReservation = await store.Operations.SendAsync(
                new GetCompareExchangeValueOperation<string>(
                    CompareExchangeKeys.ForUserName("KEEPME")));
            Assert.NotNull(userReservation);
        }
    }
}
