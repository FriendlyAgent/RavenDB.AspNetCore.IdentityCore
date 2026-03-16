using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Raven.Client.Documents;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.AspNetCore.IdentityCore.Extensions;
using RavenDB.Test.Shared;
using System;
using Xunit;

namespace RavenDB.AspNetCore.IdentityCore.Tests.DependencyInjection
{
    /// <summary>
    /// Tests to verify that dependency injection is configured correctly for RavenDB Identity.
    ///
    /// These tests ensure that:
    /// - All required services can be resolved from the DI container
    /// - Services have the correct lifetime (Singleton, Scoped, Transient)
    /// - No circular dependencies or missing registrations exist
    /// </summary>
    public class ServiceRegistrationTests : RavenDBTestBase
    {
        #region AddRavenIdentityUserOnly Tests

        /// <summary>
        /// Test: AddRavenIdentityUserOnly registers all required services
        ///
        /// Verifies:
        /// - UserManager can be resolved
        /// - SignInManager can be resolved
        /// - All validators are registered
        /// - Password hasher is registered
        /// - Lookup normalizer is registered
        /// </summary>
        [Fact(DisplayName = "DI: AddRavenIdentityUserOnly - All services resolve")]
        public void AddRavenIdentityUserOnly_AllServicesResolve()
        {
            // Arrange
            var services = new ServiceCollection();
            var store = GetDocumentStore();

            services.AddSingleton<IDocumentStore>(store);
            services.AddScoped(provider => store.OpenAsyncSession());

            services.AddRavenIdentityUserOnly<TestUser>()
                .AddRavenStores();

            var serviceProvider = services.BuildServiceProvider();

            // Act & Assert: Resolve core services
            using (var scope = serviceProvider.CreateScope())
            {
                var scopedProvider = scope.ServiceProvider;

                // UserManager should resolve
                var userManager = scopedProvider.GetService<UserManager<TestUser>>();
                Assert.NotNull(userManager);

                // SignInManager should resolve
                var signInManager = scopedProvider.GetService<SignInManager<TestUser>>();
                Assert.NotNull(signInManager);

                // UserStore should resolve
                var userStore = scopedProvider.GetService<IUserStore<TestUser>>();
                Assert.NotNull(userStore);

                // Password hasher should resolve
                var passwordHasher = scopedProvider.GetService<IPasswordHasher<TestUser>>();
                Assert.NotNull(passwordHasher);

                // Lookup normalizer should resolve
                var normalizer = scopedProvider.GetService<ILookupNormalizer>();
                Assert.NotNull(normalizer);

                // User validator should resolve
                var userValidator = scopedProvider.GetService<IUserValidator<TestUser>>();
                Assert.NotNull(userValidator);

                // Password validator should resolve
                var passwordValidator = scopedProvider.GetService<IPasswordValidator<TestUser>>();
                Assert.NotNull(passwordValidator);

                // Error describer should resolve
                var errorDescriber = scopedProvider.GetService<IdentityErrorDescriber>();
                Assert.NotNull(errorDescriber);
            }

            serviceProvider.Dispose();
            store.Dispose();
        }

        /// <summary>
        /// Test: Custom user type with AddRavenIdentityUserOnly
        ///
        /// Verifies:
        /// - Custom user types work with DI
        /// - UserManager is typed correctly
        /// </summary>
        [Fact(DisplayName = "DI: AddRavenIdentityUserOnly - Custom user type")]
        public void AddRavenIdentityUserOnly_CustomUserType_Resolves()
        {
            // Arrange
            var services = new ServiceCollection();
            var store = GetDocumentStore();

            services.AddSingleton<IDocumentStore>(store);
            services.AddScoped(provider => store.OpenAsyncSession());

            services.AddRavenIdentityUserOnly<TestUser>()
                .AddRavenStores();

            var serviceProvider = services.BuildServiceProvider();

            // Act & Assert
            using (var scope = serviceProvider.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetService<UserManager<TestUser>>();

                Assert.NotNull(userManager);
                Assert.IsType<UserManager<TestUser>>(userManager);
            }

            serviceProvider.Dispose();
            store.Dispose();
        }

        #endregion

        #region AddRavenIdentity Tests

        /// <summary>
        /// Test: AddRavenIdentity registers all required services
        ///
        /// Verifies:
        /// - UserManager can be resolved
        /// - SignInManager can be resolved
        /// - RoleManager can be resolved
        /// - All validators are registered
        /// - User and Role stores are registered
        /// </summary>
        [Fact(DisplayName = "DI: AddRavenIdentity - All services resolve")]
        public void AddRavenIdentity_AllServicesResolve()
        {
            // Arrange
            var services = new ServiceCollection();
            var store = GetDocumentStore();

            services.AddSingleton<IDocumentStore>(store);
            services.AddScoped(provider => store.OpenAsyncSession());

            services.AddRavenIdentity<TestUser, TestRole>()
                .AddRavenStores();

            var serviceProvider = services.BuildServiceProvider();

            // Act & Assert
            using (var scope = serviceProvider.CreateScope())
            {
                var scopedProvider = scope.ServiceProvider;

                // UserManager should resolve
                var userManager = scopedProvider.GetService<UserManager<TestUser>>();
                Assert.NotNull(userManager);

                // SignInManager should resolve
                var signInManager = scopedProvider.GetService<SignInManager<TestUser>>();
                Assert.NotNull(signInManager);

                // RoleManager should resolve
                var roleManager = scopedProvider.GetService<RoleManager<TestRole>>();
                Assert.NotNull(roleManager);

                // UserStore should resolve
                var userStore = scopedProvider.GetService<IUserStore<TestUser>>();
                Assert.NotNull(userStore);

                // RoleStore should resolve
                var roleStore = scopedProvider.GetService<IRoleStore<TestRole>>();
                Assert.NotNull(roleStore);

                // Role validator should resolve
                var roleValidator = scopedProvider.GetService<IRoleValidator<TestRole>>();
                Assert.NotNull(roleValidator);
            }

            serviceProvider.Dispose();
            store.Dispose();
        }

        /// <summary>
        /// Test: Identity options are configured
        ///
        /// Verifies:
        /// - Custom IdentityOptions are applied
        /// - Configuration action is executed
        /// </summary>
        [Fact(DisplayName = "DI: AddRavenIdentity - Options configured")]
        public void AddRavenIdentity_OptionsConfigured()
        {
            // Arrange
            var services = new ServiceCollection();
            var store = GetDocumentStore();

            services.AddSingleton<IDocumentStore>(store);
            services.AddScoped(provider => store.OpenAsyncSession());

            services.AddRavenIdentity<TestUser, TestRole>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.User.RequireUniqueEmail = true;
            })
            .AddRavenStores();

            var serviceProvider = services.BuildServiceProvider();

            // Act
            using (var scope = serviceProvider.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetService<UserManager<TestUser>>();

                // Assert: Options should be applied
                Assert.NotNull(userManager);
                Assert.Equal(12, userManager.Options.Password.RequiredLength);
                Assert.True(userManager.Options.Password.RequireDigit);
                Assert.True(userManager.Options.User.RequireUniqueEmail);
            }

            serviceProvider.Dispose();
            store.Dispose();
        }

        #endregion

        #region Service Lifetime Tests

        /// <summary>
        /// Test: Services have correct lifetime (Scoped)
        ///
        /// Verifies:
        /// - UserManager is scoped
        /// - SignInManager is scoped
        /// - Same instance within scope
        /// - Different instances across scopes
        /// </summary>
        [Fact(DisplayName = "DI: Service lifetime - Scoped services")]
        public void Services_HaveCorrectLifetime()
        {
            // Arrange
            var services = new ServiceCollection();
            var store = GetDocumentStore();

            services.AddSingleton<IDocumentStore>(store);
            services.AddScoped(provider => store.OpenAsyncSession());

            services.AddRavenIdentity<TestUser, TestRole>()
                .AddRavenStores();

            var serviceProvider = services.BuildServiceProvider();

            // Act & Assert: Same instance within scope
            using (var scope = serviceProvider.CreateScope())
            {
                var userManager1 = scope.ServiceProvider.GetService<UserManager<TestUser>>();
                var userManager2 = scope.ServiceProvider.GetService<UserManager<TestUser>>();

                Assert.Same(userManager1, userManager2);
            }

            // Act & Assert: Different instances across scopes
            UserManager<TestUser> scopedUserManager1;
            UserManager<TestUser> scopedUserManager2;

            using (var scope1 = serviceProvider.CreateScope())
            {
                scopedUserManager1 = scope1.ServiceProvider.GetService<UserManager<TestUser>>();
            }

            using (var scope2 = serviceProvider.CreateScope())
            {
                scopedUserManager2 = scope2.ServiceProvider.GetService<UserManager<TestUser>>();
            }

            Assert.NotSame(scopedUserManager1, scopedUserManager2);

            serviceProvider.Dispose();
            store.Dispose();
        }

        #endregion

        #region Missing Registration Tests

        /// <summary>
        /// Test: Missing DocumentStore throws exception
        ///
        /// Verifies:
        /// - Proper error when IDocumentStore is not registered
        /// </summary>
        [Fact(DisplayName = "DI: Missing IDocumentStore - Throws exception")]
        public void MissingDocumentStore_ThrowsException()
        {
            // Arrange
            var services = new ServiceCollection();

            // Don't register IDocumentStore or session
            services.AddRavenIdentity<TestUser, TestRole>()
                .AddRavenStores();

            var serviceProvider = services.BuildServiceProvider();

            // Act & Assert
            using (var scope = serviceProvider.CreateScope())
            {
                // Attempting to resolve UserStore should fail because session is missing
                Assert.Throws<InvalidOperationException>(() =>
                {
                    scope.ServiceProvider.GetRequiredService<IUserStore<TestUser>>();
                });
            }

            serviceProvider.Dispose();
        }

        #endregion
    }
}
