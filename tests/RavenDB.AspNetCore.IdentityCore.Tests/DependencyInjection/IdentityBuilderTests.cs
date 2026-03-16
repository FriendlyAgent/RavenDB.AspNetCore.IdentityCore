using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.AspNetCore.IdentityCore.Extensions;
using RavenDB.Test.Shared;
using System;
using Xunit;

namespace RavenDB.AspNetCore.IdentityCore.Tests.DependencyInjection
{
    /// <summary>
    /// Tests for IdentityBuilder pattern and fluent API.
    ///
    /// These tests verify:
    /// - IdentityBuilder is returned correctly
    /// - Fluent API chaining works
    /// - AddRavenStores works with the builder
    /// - AddDefaultTokenProviders works
    /// - Custom configurations can be chained
    /// </summary>
    public class IdentityBuilderTests
    {
        #region IdentityBuilder Return Tests

        /// <summary>
        /// Test: AddRavenIdentity returns IdentityBuilder with correct types
        ///
        /// Verifies the method returns a valid IdentityBuilder with correct user and role types
        /// </summary>
        [Fact(DisplayName = "Builder: AddRavenIdentity returns builder with correct types")]
        public void AddRavenIdentity_ReturnsIdentityBuilder()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var builder = services.AddRavenIdentity<TestUser, TestRole>();

            // Assert
            Assert.NotNull(builder);
            Assert.IsType<IdentityBuilder>(builder);
            Assert.Equal(typeof(TestUser), builder.UserType);
            Assert.Equal(typeof(TestRole), builder.RoleType);
        }

        /// <summary>
        /// Test: AddRavenIdentityUserOnly returns IdentityBuilder with correct user type
        ///
        /// Verifies the method returns a valid IdentityBuilder for user-only scenarios
        /// </summary>
        [Fact(DisplayName = "Builder: AddRavenIdentityUserOnly returns builder")]
        public void AddRavenIdentityUserOnly_ReturnsIdentityBuilder()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var builder = services.AddRavenIdentityUserOnly<TestUser>();

            // Assert
            Assert.NotNull(builder);
            Assert.IsType<IdentityBuilder>(builder);
            Assert.Equal(typeof(TestUser), builder.UserType);
        }

        #endregion

        #region Fluent API Chaining Tests

        /// <summary>
        /// Test: Can chain AddRavenStores and verify registration
        ///
        /// Verifies fluent API chaining with AddRavenStores and that stores are registered
        /// </summary>
        [Fact(DisplayName = "Builder: Can chain AddRavenStores")]
        public void FluentAPI_CanChainAddRavenStores()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var builder = services
                .AddRavenIdentity<TestUser, TestRole>()
                .AddRavenStores();

            // Assert
            Assert.NotNull(builder);
            Assert.IsType<IdentityBuilder>(builder);

            // Verify stores are registered
            Assert.Contains(services, s => s.ServiceType == typeof(IUserStore<TestUser>));
            Assert.Contains(services, s => s.ServiceType == typeof(IRoleStore<TestRole>));
        }

        /// <summary>
        /// Test: Can chain multiple calls with options
        ///
        /// Verifies complex fluent API chaining with options configuration
        /// </summary>
        [Fact(DisplayName = "Builder: Can chain multiple calls with options")]
        public void FluentAPI_CanChainMultipleCalls()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var builder = services
                .AddRavenIdentity<TestUser, TestRole>(options =>
                {
                    options.Password.RequiredLength = 8;
                })
                .AddRavenStores()
                .AddDefaultTokenProviders();

            // Assert
            Assert.NotNull(builder);
            Assert.IsType<IdentityBuilder>(builder);

            // Verify all key services registered
            Assert.Contains(services, s => s.ServiceType == typeof(UserManager<TestUser>));
            Assert.Contains(services, s => s.ServiceType == typeof(RoleManager<TestRole>));
            Assert.Contains(services, s => s.ServiceType == typeof(IUserStore<TestUser>));
            Assert.Contains(services, s => s.ServiceType == typeof(IRoleStore<TestRole>));
        }

        /// <summary>
        /// Test: UserOnly can chain AddRavenStores
        ///
        /// Verifies UserOnly variant supports fluent API
        /// </summary>
        [Fact(DisplayName = "Builder: UserOnly can chain AddRavenStores")]
        public void FluentAPI_UserOnly_CanChainAddRavenStores()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var builder = services
                .AddRavenIdentityUserOnly<TestUser>()
                .AddRavenStores();

            // Assert
            Assert.NotNull(builder);
            Assert.Contains(services, s => s.ServiceType == typeof(IUserStore<TestUser>));
        }

        #endregion

        #region AddRavenStores Tests

        /// <summary>
        /// Test: AddRavenStores with custom session factory
        ///
        /// Verifies AddRavenStores can accept custom session factory
        /// </summary>
        [Fact(DisplayName = "Builder: AddRavenStores with custom session factory")]
        public void AddRavenStores_WithCustomSessionFactory_RegistersCorrectly()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var builder = services
                .AddRavenIdentity<TestUser, TestRole>()
                .AddRavenStores(provider => null); // Custom factory function

            // Assert
            Assert.NotNull(builder);
            Assert.Contains(services, s => s.ServiceType == typeof(IUserStore<TestUser>));
        }

        /// <summary>
        /// Test: AddRavenStores with UserOnly only registers user store
        ///
        /// Verifies role store is not registered for UserOnly variant
        /// </summary>
        [Fact(DisplayName = "Builder: UserOnly doesn't register role store")]
        public void AddRavenStores_UserOnly_OnlyRegistersUserStore()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddRavenIdentityUserOnly<TestUser>()
                .AddRavenStores();

            // Assert
            Assert.Contains(services, s => s.ServiceType == typeof(IUserStore<TestUser>));
            Assert.DoesNotContain(services, s => s.ServiceType == typeof(IRoleStore<TestRole>));
        }

        #endregion

        #region Standard Types Tests

        /// <summary>
        /// Test: Default AddRavenIdentity uses standard types
        ///
        /// Verifies default overload uses RavenIdentityUser and RavenIdentityRole
        /// </summary>
        [Fact(DisplayName = "Builder: Default AddRavenIdentity uses standard types")]
        public void AddRavenIdentity_Default_UsesStandardTypes()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var builder = services.AddRavenIdentity();

            // Assert
            Assert.Equal(typeof(RavenIdentityUser), builder.UserType);
            Assert.Equal(typeof(RavenIdentityRole), builder.RoleType);
        }

        /// <summary>
        /// Test: Default AddRavenIdentityUserOnly uses standard user type
        ///
        /// Verifies default overload uses RavenIdentityUser
        /// </summary>
        [Fact(DisplayName = "Builder: Default AddRavenIdentityUserOnly uses standard type")]
        public void AddRavenIdentityUserOnly_Default_UsesStandardType()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var builder = services.AddRavenIdentityUserOnly();

            // Assert
            Assert.Equal(typeof(RavenIdentityUser), builder.UserType);
        }

        #endregion

        #region Options Configuration Tests

        /// <summary>
        /// Test: Builder preserves options configuration
        ///
        /// Verifies options passed to AddRavenIdentity are preserved
        /// </summary>
        [Fact(DisplayName = "Builder: Preserves options configuration")]
        public void Builder_PreservesOptionsConfiguration()
        {
            // Arrange
            var services = new ServiceCollection();
            const int expectedLength = 12;

            // Act
            services.AddRavenIdentity<TestUser, TestRole>(options =>
            {
                options.Password.RequiredLength = expectedLength;
            });

            var provider = services.BuildServiceProvider();
            var options = provider.GetService<Microsoft.Extensions.Options.IOptions<IdentityOptions>>();

            // Assert
            Assert.NotNull(options);
            Assert.Equal(expectedLength, options.Value.Password.RequiredLength);
        }

        /// <summary>
        /// Test: Builder can be configured after creation
        ///
        /// Verifies additional configuration can be added via builder
        /// </summary>
        [Fact(DisplayName = "Builder: Can be configured after creation")]
        public void Builder_CanBeConfiguredAfterCreation()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddRavenIdentity<TestUser, TestRole>()
                .AddRavenStores()
                .AddDefaultTokenProviders()
                .AddPasswordValidator<PasswordValidator<TestUser>>();

            // Assert
            Assert.Contains(services, s => s.ServiceType == typeof(IPasswordValidator<TestUser>));
        }

        #endregion

        #region AddApiEndpoints Tests

        /// <summary>
        /// Test: AddRavenIdentityApiEndpoints returns IdentityBuilder
        ///
        /// Verifies API endpoints method returns builder
        /// </summary>
        [Fact(DisplayName = "Builder: AddRavenIdentityApiEndpoints returns IdentityBuilder")]
        public void AddRavenIdentityApiEndpoints_ReturnsIdentityBuilder()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var builder = services.AddRavenIdentityApiEndpoints<TestUser>();

            // Assert
            Assert.NotNull(builder);
            Assert.IsType<IdentityBuilder>(builder);
            Assert.Equal(typeof(TestUser), builder.UserType);
        }

        /// <summary>
        /// Test: AddRavenIdentityApiEndpoints can chain AddRavenStores
        ///
        /// Verifies API endpoints can be chained
        /// </summary>
        [Fact(DisplayName = "Builder: AddRavenIdentityApiEndpoints can chain stores")]
        public void AddRavenIdentityApiEndpoints_CanChainStores()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var builder = services
                .AddRavenIdentityApiEndpoints<TestUser>()
                .AddRavenStores();

            // Assert
            Assert.NotNull(builder);
            Assert.Contains(services, s => s.ServiceType == typeof(IUserStore<TestUser>));
        }

        #endregion
    }
}
