using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.AspNetCore.IdentityCore.Validators;
using System;

namespace RavenDB.AspNetCore.IdentityCore.Extensions
{
    /// <summary>
    /// Contains extension methods to <see cref="IServiceCollection"/> for configuring identity services.
    /// </summary>
    public static class RavenIdentityServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the default identity system configuration for the standart User.
        /// </summary>
        /// <param name="services">The services available in the application.</param>
        /// <param name="setupAction">An action to configure the <see cref="IdentityOptions"/>.</param>
        /// <returns>An <see cref="IdentityBuilder"/> for creating and configuring the identity system.</returns>
        public static IdentityBuilder AddRavenIdentityUserOnly(
            this IServiceCollection services,
            Action<IdentityOptions> setupAction = null)
        {
            return services.AddRavenIdentityUserOnly<RavenIdentityUser>(setupAction);
        }

        /// <summary>
        /// Adds the default identity system configuration for the specified User.
        /// </summary>
        /// <typeparam name="TUser">The type representing a User in the system.</typeparam>
        /// <param name="services">The services available in the application.</param>
        /// <param name="setupAction">An action to configure the <see cref="IdentityOptions"/>.</param>
        /// <returns>An <see cref="IdentityBuilder"/> for creating and configuring the identity system.</returns>
        public static IdentityBuilder AddRavenIdentityUserOnly<TUser>(
            this IServiceCollection services,
            Action<IdentityOptions> setupAction = null)
            where TUser : RavenIdentityUser
        {
            AddDefaultIdentity<TUser>(services, setupAction);
            services.TryAddScoped<IUserClaimsPrincipalFactory<TUser>, UserClaimsPrincipalFactory<TUser>>();

            return new IdentityBuilder(typeof(TUser), services);
        }

        /// <summary>
        /// Adds the default identity system configuration for the standart User and Role.
        /// </summary>
        /// <param name="services">The services available in the application.</param>
        /// <param name="setupAction">An action to configure the <see cref="IdentityOptions"/>.</param>
        /// <returns>An <see cref="IdentityBuilder"/> for creating and configuring the identity system.</returns>
        public static IdentityBuilder AddRavenIdentity(
            this IServiceCollection services,
            Action<IdentityOptions> setupAction = null)
        {
            return services.AddRavenIdentity<RavenIdentityUser, RavenIdentityRole>(setupAction);
        }

        /// <summary>
        /// Adds the default identity system configuration for the specified User.
        /// </summary>
        /// <typeparam name="TUser">The type representing a User in the system.</typeparam>
        /// <typeparam name="TRole">The type representing a Role in the system.</typeparam>
        /// <param name="services">The services available in the application.</param>
        /// <param name="setupAction">An action to configure the <see cref="IdentityOptions"/>.</param>
        /// <returns>An <see cref="IdentityBuilder"/> for creating and configuring the identity system.</returns>
        public static IdentityBuilder AddRavenIdentity<TUser, TRole>(
            this IServiceCollection services,
            Action<IdentityOptions> setupAction = null)
            where TUser : RavenIdentityUser
            where TRole : RavenIdentityRole
        {
            AddDefaultIdentity<TUser>(services, setupAction);

            services.TryAddScoped<IRoleValidator<TRole>, RavenRoleValidator<TRole>>();
            services.TryAddScoped<IUserClaimsPrincipalFactory<TUser>, UserClaimsPrincipalFactory<TUser, TRole>>();
            services.TryAddScoped<RoleManager<TRole>, RoleManager<TRole>>();

            return new IdentityBuilder(typeof(TUser), typeof(TRole), services);
        }

        private static void AddDefaultIdentity<TUser>(
            IServiceCollection services,
            Action<IdentityOptions> setupAction = null)
            where TUser : RavenIdentityUser
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
                options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddCookie(IdentityConstants.ApplicationScheme, o =>
            {
                o.LoginPath = new PathString("/Account/Login");
                o.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync
                };
            })
            .AddCookie(IdentityConstants.ExternalScheme, o =>
            {
                o.Cookie.Name = IdentityConstants.ExternalScheme;
                o.ExpireTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddCookie(IdentityConstants.TwoFactorRememberMeScheme,
                o => o.Cookie.Name = IdentityConstants.TwoFactorRememberMeScheme)
            .AddCookie(IdentityConstants.TwoFactorUserIdScheme, o =>
            {
                o.Cookie.Name = IdentityConstants.TwoFactorUserIdScheme;
                o.ExpireTimeSpan = TimeSpan.FromMinutes(5);
            });

            // Hosting doesn't add IHttpContextAccessor by default
            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            // Identity services
            services.TryAddScoped<IUserValidator<TUser>, RavenUserValidator<TUser>>();
            services.TryAddScoped<IPasswordValidator<TUser>, PasswordValidator<TUser>>();
            services.TryAddScoped<IPasswordHasher<TUser>, PasswordHasher<TUser>>();
            services.TryAddScoped<ILookupNormalizer, UpperInvariantLookupNormalizer>();

            // No interface for the error describer so we can add errors without rev'ing the interface
            services.TryAddScoped<IdentityErrorDescriber>();
            services.TryAddScoped<ISecurityStampValidator, SecurityStampValidator<TUser>>();

            services.TryAddScoped<UserManager<TUser>, UserManager<TUser>>();
            services.TryAddScoped<SignInManager<TUser>, SignInManager<TUser>>();

            var dataProtectionProviderType = typeof(DataProtectorTokenProvider<>)
                .MakeGenericType(typeof(TUser));

            var phoneNumberProviderType = typeof(PhoneNumberTokenProvider<>)
                .MakeGenericType(typeof(TUser));

            var emailTokenProviderType = typeof(EmailTokenProvider<>)
                .MakeGenericType(typeof(TUser));

            AddTokenProvider(services, TokenOptions.DefaultProvider, dataProtectionProviderType);
            AddTokenProvider(services, TokenOptions.DefaultEmailProvider, emailTokenProviderType);
            AddTokenProvider(services, TokenOptions.DefaultPhoneProvider, phoneNumberProviderType);

            if (setupAction != null)
            {
                services.Configure(setupAction);
            }
        }

        private static void AddTokenProvider(IServiceCollection services, string providerName, Type provider)
        {
            services.Configure<IdentityOptions>(options =>
            {
                options.Tokens.ProviderMap[providerName] = new TokenProviderDescriptor(provider);
            });

            services.AddSingleton(provider);
        }
    }
}