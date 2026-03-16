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
    /// Tests for RavenUserValidator with RavenUserValidatorOptions.
    ///
    /// Validates:
    /// - Minimum length requirement
    /// - Integration with standard Identity validation
    /// </summary>
    public class RavenUserValidatorTests
    {
        /// <summary>
        /// Minimal mock user store for validator testing - validators don't need full store functionality.
        /// </summary>
        private class MockUserStore : IUserStore<TestUser>
        {
            public Task<IdentityResult> CreateAsync(TestUser user, CancellationToken cancellationToken) =>
                Task.FromResult(IdentityResult.Success);

            public Task<IdentityResult> DeleteAsync(TestUser user, CancellationToken cancellationToken) =>
                Task.FromResult(IdentityResult.Success);

            public Task<TestUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
                Task.FromResult<TestUser?>(null);

            public Task<TestUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
                Task.FromResult<TestUser?>(null);

            public Task<string?> GetNormalizedUserNameAsync(TestUser user, CancellationToken cancellationToken) =>
                Task.FromResult(user.NormalizedUserName);

            public Task<string> GetUserIdAsync(TestUser user, CancellationToken cancellationToken) =>
                Task.FromResult(user.Id);

            public Task<string?> GetUserNameAsync(TestUser user, CancellationToken cancellationToken) =>
                Task.FromResult(user.UserName);

            public Task SetNormalizedUserNameAsync(TestUser user, string? normalizedName, CancellationToken cancellationToken)
            {
                user.NormalizedUserName = normalizedName;
                return Task.CompletedTask;
            }

            public Task SetUserNameAsync(TestUser user, string? userName, CancellationToken cancellationToken)
            {
                user.UserName = userName;
                return Task.CompletedTask;
            }

            public Task<IdentityResult> UpdateAsync(TestUser user, CancellationToken cancellationToken) =>
                Task.FromResult(IdentityResult.Success);

            public void Dispose() { }
        }

        private UserManager<TestUser> CreateUserManager(RavenUserValidatorOptions options = null)
        {
            var store = new MockUserStore();
            var userValidator = options != null
                ? new RavenUserValidator<TestUser>(
                    new IdentityErrorDescriber(),
                    Options.Create(options))
                : new RavenUserValidator<TestUser>();

            var userManager = new UserManager<TestUser>(
                store,
                null,
                new PasswordHasher<TestUser>(),
                new[] { userValidator },
                null,
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                null,
                null);

            return userManager;
        }

        #region Required Length Tests

        [Theory(DisplayName = "RavenUserValidator: Minimum length is enforced")]
        [InlineData(3, "ab", false)]
        [InlineData(3, "abc", true)]
        [InlineData(3, "abcd", true)]
        [InlineData(5, "test", false)]
        [InlineData(5, "tests", true)]
        public async Task ValidateAsync_RequiredLength_Enforced(int requiredLength, string username, bool shouldSucceed)
        {
            // Arrange
            var options = new RavenUserValidatorOptions
            {
                RequiredLength = requiredLength
            };

            var userManager = CreateUserManager(options);
            var user = new TestUser { UserName = username };

            // Act
            var result = await userManager.UserValidators[0].ValidateAsync(userManager, user);

            // Assert
            Assert.Equal(shouldSucceed, result.Succeeded);
            if (!shouldSucceed)
            {
                Assert.Contains(result.Errors, e => e.Code == "UserNameTooShort");
            }
        }

        [Fact(DisplayName = "RavenUserValidator: No length requirement when RequiredLength is null")]
        public async Task ValidateAsync_NoRequiredLength_NoEnforcement()
        {
            // Arrange
            var options = new RavenUserValidatorOptions
            {
                RequiredLength = null // Explicitly null
            };

            var userManager = CreateUserManager(options);
            var user = new TestUser { UserName = "a" }; // Very short

            // Act
            var result = await userManager.UserValidators[0].ValidateAsync(userManager, user);

            // Assert
            Assert.True(result.Succeeded);
        }

        #endregion

        #region General Tests

        [Fact(DisplayName = "RavenUserValidator: No options configured allows all valid usernames")]
        public async Task ValidateAsync_NoOptionsConfigured_AllowsAllValidUsernames()
        {
            // Arrange - No options configured
            var userManager = CreateUserManager(null);
            var user = new TestUser { UserName = "anyusername" };

            // Act
            var result = await userManager.UserValidators[0].ValidateAsync(userManager, user);

            // Assert
            Assert.True(result.Succeeded);
        }

        #endregion

        #region Standard Identity Validation Integration

        [Fact(DisplayName = "RavenUserValidator: Empty username is rejected")]
        public async Task ValidateAsync_EmptyUsername_Rejected()
        {
            // Arrange
            var userManager = CreateUserManager();
            var user = new TestUser { UserName = "" };

            // Act
            var result = await userManager.UserValidators[0].ValidateAsync(userManager, user);

            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.Code == "InvalidUserName");
        }

        [Fact(DisplayName = "RavenUserValidator: Null username is rejected")]
        public async Task ValidateAsync_NullUsername_Rejected()
        {
            // Arrange
            var userManager = CreateUserManager();
            var user = new TestUser { UserName = null };

            // Act
            var result = await userManager.UserValidators[0].ValidateAsync(userManager, user);

            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.Code == "InvalidUserName");
        }

        [Fact(DisplayName = "RavenUserValidator: Invalid characters are rejected")]
        public async Task ValidateAsync_InvalidCharacters_Rejected()
        {
            // Arrange
            var userManager = CreateUserManager();
            userManager.Options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._";
            var user = new TestUser { UserName = "user@name!" }; // @ and ! not allowed

            // Act
            var result = await userManager.UserValidators[0].ValidateAsync(userManager, user);

            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.Code == "InvalidUserName");
        }

        #endregion
    }
}
