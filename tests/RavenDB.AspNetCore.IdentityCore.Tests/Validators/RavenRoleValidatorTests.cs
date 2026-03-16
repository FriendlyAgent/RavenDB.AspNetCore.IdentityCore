using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.AspNetCore.IdentityCore.Validators;
using RavenDB.Test.Shared;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RavenDB.AspNetCore.IdentityCore.Tests.Validators
{
    /// <summary>
    /// Tests for RavenRoleValidator with RavenRoleValidatorOptions.
    ///
    /// Validates:
    /// - Minimum length requirement
    /// - Integration with standard Identity validation
    /// </summary>
    public class RavenRoleValidatorTests
    {
        /// <summary>
        /// Minimal mock role store for validator testing - validators don't need full store functionality.
        /// </summary>
        private class MockRoleStore : IRoleStore<TestRole>
        {
            public Task<IdentityResult> CreateAsync(TestRole role, CancellationToken cancellationToken) =>
                Task.FromResult(IdentityResult.Success);

            public Task<IdentityResult> DeleteAsync(TestRole role, CancellationToken cancellationToken) =>
                Task.FromResult(IdentityResult.Success);

            public Task<TestRole?> FindByIdAsync(string roleId, CancellationToken cancellationToken) =>
                Task.FromResult<TestRole?>(null);

            public Task<TestRole?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken) =>
                Task.FromResult<TestRole?>(null);

            public Task<string?> GetNormalizedRoleNameAsync(TestRole role, CancellationToken cancellationToken) =>
                Task.FromResult(role.NormalizedRoleName);

            public Task<string> GetRoleIdAsync(TestRole role, CancellationToken cancellationToken) =>
                Task.FromResult(role.Id);

            public Task<string?> GetRoleNameAsync(TestRole role, CancellationToken cancellationToken) =>
                Task.FromResult(role.RoleName);

            public Task SetNormalizedRoleNameAsync(TestRole role, string? normalizedName, CancellationToken cancellationToken)
            {
                role.NormalizedRoleName = normalizedName;
                return Task.CompletedTask;
            }

            public Task SetRoleNameAsync(TestRole role, string? roleName, CancellationToken cancellationToken)
            {
                role.RoleName = roleName;
                return Task.CompletedTask;
            }

            public Task<IdentityResult> UpdateAsync(TestRole role, CancellationToken cancellationToken) =>
                Task.FromResult(IdentityResult.Success);

            public void Dispose() { }
        }

        private RoleManager<TestRole> CreateRoleManager(RavenRoleValidatorOptions options = null)
        {
            var store = new MockRoleStore();
            var roleValidator = options != null
                ? new RavenRoleValidator<TestRole>(
                    new IdentityErrorDescriber(),
                    Options.Create(options))
                : new RavenRoleValidator<TestRole>();

            var roleManager = new RoleManager<TestRole>(
                store,
                new[] { roleValidator },
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                null);

            return roleManager;
        }

        #region Required Length Tests

        [Theory(DisplayName = "RavenRoleValidator: Minimum length is enforced")]
        [InlineData(3, "ab", false)]
        [InlineData(3, "abc", true)]
        [InlineData(3, "abcd", true)]
        [InlineData(5, "test", false)]
        [InlineData(5, "tests", true)]
        public async Task ValidateAsync_RequiredLength_Enforced(int requiredLength, string roleName, bool shouldSucceed)
        {
            // Arrange
            var options = new RavenRoleValidatorOptions
            {
                RequiredLength = requiredLength
            };

            var roleManager = CreateRoleManager(options);
            var role = new TestRole { RoleName = roleName };

            // Act
            var result = await roleManager.RoleValidators[0].ValidateAsync(roleManager, role);

            // Assert
            Assert.Equal(shouldSucceed, result.Succeeded);
            if (!shouldSucceed)
            {
                Assert.Contains(result.Errors, e => e.Code == "RoleNameTooShort");
            }
        }

        [Fact(DisplayName = "RavenRoleValidator: No length requirement when RequiredLength is null")]
        public async Task ValidateAsync_NoRequiredLength_NoEnforcement()
        {
            // Arrange
            var options = new RavenRoleValidatorOptions
            {
                RequiredLength = null // Explicitly null
            };

            var roleManager = CreateRoleManager(options);
            var role = new TestRole { RoleName = "a" }; // Very short

            // Act
            var result = await roleManager.RoleValidators[0].ValidateAsync(roleManager, role);

            // Assert
            Assert.True(result.Succeeded);
        }

        #endregion

        #region General Tests

        [Fact(DisplayName = "RavenRoleValidator: No options configured allows all valid role names")]
        public async Task ValidateAsync_NoOptionsConfigured_AllowsAllValidRoleNames()
        {
            // Arrange - No options configured
            var roleManager = CreateRoleManager(null);
            var role = new TestRole { RoleName = "AnyRoleName" };

            // Act
            var result = await roleManager.RoleValidators[0].ValidateAsync(roleManager, role);

            // Assert
            Assert.True(result.Succeeded);
        }

        #endregion

        #region Standard Identity Validation Integration

        [Fact(DisplayName = "RavenRoleValidator: Empty role name is rejected")]
        public async Task ValidateAsync_EmptyRoleName_Rejected()
        {
            // Arrange
            var roleManager = CreateRoleManager();
            var role = new TestRole { RoleName = "" };

            // Act
            var result = await roleManager.RoleValidators[0].ValidateAsync(roleManager, role);

            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.Code == "InvalidRoleName");
        }

        [Fact(DisplayName = "RavenRoleValidator: Null role name is rejected")]
        public async Task ValidateAsync_NullRoleName_Rejected()
        {
            // Arrange
            var roleManager = CreateRoleManager();
            var role = new TestRole { RoleName = null };

            // Act
            var result = await roleManager.RoleValidators[0].ValidateAsync(roleManager, role);

            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.Code == "InvalidRoleName");
        }

        #endregion
    }
}
