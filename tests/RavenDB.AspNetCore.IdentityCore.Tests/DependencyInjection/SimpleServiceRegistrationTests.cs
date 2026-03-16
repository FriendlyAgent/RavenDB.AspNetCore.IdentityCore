using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.AspNetCore.IdentityCore.Extensions;
using RavenDB.Test.Shared;
using System;
using System.Linq;
using Xunit;

namespace RavenDB.AspNetCore.IdentityCore.Tests.DependencyInjection
{
    /// <summary>
    /// Simple tests to verify service registration without requiring RavenDB server.
    ///
    /// These tests verify:
    /// - Services are registered with the correct lifetime
    /// - No missing dependencies
    /// - Configuration is applied correctly
    /// </summary>
    public class SimpleServiceRegistrationTests
    {
        #region Registration Tests

        /// <summary>
        /// Test: AddRavenIdentityUserOnly registers expected services
        ///
        /// Verifies services are registered in the DI container
        /// </summary>
        [Fact(DisplayName = "DI: AddRavenIdentityUserOnly - Services registered")]
        public void AddRavenIdentityUserOnly_ServicesRegistered()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddRavenIdentityUserOnly<TestUser>();

            // Assert: Check that key services are registered
            Assert.Contains(services, s => s.ServiceType == typeof(UserManager<TestUser>));
            Assert.Contains(services, s => s.ServiceType == typeof(SignInManager<TestUser>));
            Assert.Contains(services, s => s.ServiceType == typeof(IUserValidator<TestUser>));
            Assert.Contains(services, s => s.ServiceType == typeof(IPasswordValidator<TestUser>));
            Assert.Contains(services, s => s.ServiceType == typeof(IPasswordHasher<TestUser>));
            Assert.Contains(services, s => s.ServiceType == typeof(ILookupNormalizer));
            Assert.Contains(services, s => s.ServiceType == typeof(IdentityErrorDescriber));
        }

        /// <summary>
        /// Test: AddRavenIdentity registers expected services including roles
        ///
        /// Verifies services are registered in the DI container
        /// </summary>
        [Fact(DisplayName = "DI: AddRavenIdentity - Services registered with roles")]
        public void AddRavenIdentity_ServicesRegisteredWithRoles()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddRavenIdentity<TestUser, TestRole>();

            // Assert: Check that key services are registered
            Assert.Contains(services, s => s.ServiceType == typeof(UserManager<TestUser>));
            Assert.Contains(services, s => s.ServiceType == typeof(SignInManager<TestUser>));
            Assert.Contains(services, s => s.ServiceType == typeof(RoleManager<TestRole>));
            Assert.Contains(services, s => s.ServiceType == typeof(IUserValidator<TestUser>));
            Assert.Contains(services, s => s.ServiceType == typeof(IRoleValidator<TestRole>));
            Assert.Contains(services, s => s.ServiceType == typeof(IPasswordHasher<TestUser>));
            Assert.Contains(services, s => s.ServiceType == typeof(ILookupNormalizer));
        }

        /// <summary>
        /// Test: Services have correct lifetime (Scoped)
        ///
        /// Verifies UserManager and SignInManager are scoped
        /// </summary>
        [Fact(DisplayName = "DI: Services have correct lifetime")]
        public void Services_HaveCorrectLifetime()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddRavenIdentity<TestUser, TestRole>();

            // Assert
            var userManagerDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(UserManager<TestUser>));
            Assert.NotNull(userManagerDescriptor);
            Assert.Equal(ServiceLifetime.Scoped, userManagerDescriptor.Lifetime);

            var signInManagerDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(SignInManager<TestUser>));
            Assert.NotNull(signInManagerDescriptor);
            Assert.Equal(ServiceLifetime.Scoped, signInManagerDescriptor.Lifetime);

            var roleManagerDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(RoleManager<TestRole>));
            Assert.NotNull(roleManagerDescriptor);
            Assert.Equal(ServiceLifetime.Scoped, roleManagerDescriptor.Lifetime);
        }

        /// <summary>
        /// Test: Identity options configuration is applied
        ///
        /// Verifies setupAction is registered
        /// </summary>
        [Fact(DisplayName = "DI: Options configuration registered")]
        public void OptionsConfiguration_Registered()
        {
            // Arrange
            var services = new ServiceCollection();
            bool configCalled = false;

            // Act
            services.AddRavenIdentity<TestUser, TestRole>(options =>
            {
                configCalled = true;
                options.Password.RequiredLength = 12;
            });

            // Build provider to trigger configuration
            var provider = services.BuildServiceProvider();
            var options = provider.GetService<Microsoft.Extensions.Options.IOptions<IdentityOptions>>();

            // Assert: Configuration was registered (note: it's lazily evaluated)
            Assert.NotNull(options);

            // Access the value to trigger configuration
            var value = options.Value;
            Assert.Equal(12, value.Password.RequiredLength);
        }

        /// <summary>
        /// Test: AddRavenStores registers user store
        ///
        /// Verifies user store service is registered
        /// </summary>
        [Fact(DisplayName = "DI: AddRavenStores - UserStore registered")]
        public void AddRavenStores_UserStoreRegistered()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddRavenIdentity<TestUser, TestRole>()
                .AddRavenStores();

            // Assert
            Assert.Contains(services, s => s.ServiceType == typeof(IUserStore<TestUser>));
            Assert.Contains(services, s => s.ServiceType == typeof(IRoleStore<TestRole>));
        }

        /// <summary>
        /// Test: Multiple calls don't duplicate services (TryAdd behavior)
        ///
        /// Verifies TryAdd only registers once
        /// </summary>
        [Fact(DisplayName = "DI: Multiple calls don't duplicate")]
        public void MultipleCalls_DontDuplicate()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act: Call twice
            services.AddRavenIdentity<TestUser, TestRole>();
            services.AddRavenIdentity<TestUser, TestRole>();

            // Assert: Should only have one registration of each service
            var userManagerCount = services.Count(s => s.ServiceType == typeof(UserManager<TestUser>));
            Assert.Equal(1, userManagerCount);

            var signInManagerCount = services.Count(s => s.ServiceType == typeof(SignInManager<TestUser>));
            Assert.Equal(1, signInManagerCount);
        }

        #endregion

        #region IdentityBuilder Return Tests

        /// <summary>
        /// Test: Methods return IdentityBuilder for fluent API
        ///
        /// Verifies builder pattern is maintained
        /// </summary>
        [Fact(DisplayName = "DI: Methods return IdentityBuilder")]
        public void Methods_ReturnIdentityBuilder()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var builder1 = services.AddRavenIdentityUserOnly<TestUser>();
            var builder2 = services.AddRavenIdentity<TestUser, TestRole>();

            // Assert
            Assert.NotNull(builder1);
            Assert.IsType<IdentityBuilder>(builder1);

            Assert.NotNull(builder2);
            Assert.IsType<IdentityBuilder>(builder2);
        }

        /// <summary>
        /// Test: IdentityBuilder can chain AddRavenStores
        ///
        /// Verifies fluent API works
        /// </summary>
        [Fact(DisplayName = "DI: Fluent API works")]
        public void FluentAPI_Works()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act: Chain calls
            var builder = services
                .AddRavenIdentity<TestUser, TestRole>(options =>
                {
                    options.Password.RequiredLength = 10;
                })
                .AddRavenStores();

            // Assert
            Assert.NotNull(builder);
            Assert.Contains(services, s => s.ServiceType == typeof(IUserStore<TestUser>));
        }

        #endregion
    }
}
