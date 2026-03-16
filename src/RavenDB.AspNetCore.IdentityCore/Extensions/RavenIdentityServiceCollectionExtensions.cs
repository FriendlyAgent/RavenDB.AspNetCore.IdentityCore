using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Raven.Client.Documents.Session;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.AspNetCore.IdentityCore.Validators;
using System;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace RavenDB.AspNetCore.IdentityCore.Extensions
{
    /// <summary>
    /// Contains extension methods to <see cref="IServiceCollection"/> for configuring RavenDB identity services.
    /// This implementation follows the .NET Core Identity pattern.
    /// </summary>
    public static class RavenIdentityServiceCollectionExtensions
    {
        /// <summary>
        /// The scheme used to identify combination of Bearer and Application schemes.
        /// Note: This is internal in Microsoft.AspNetCore.Identity.IdentityConstants, so we define it here.
        /// </summary>
        internal const string BearerAndApplicationScheme = "Identity.BearerAndApplication";

        #region AddRavenIdentity (Full Identity with Roles)

        /// <summary>
        /// Adds the default RavenDB identity system configuration for the specified User and Role types.
        /// </summary>
        /// <typeparam name="TUser">The type representing a User in the system.</typeparam>
        /// <typeparam name="TRole">The type representing a Role in the system.</typeparam>
        /// <param name="services">The services available in the application.</param>
        /// <returns>An <see cref="IdentityBuilder"/> for creating and configuring the identity system.</returns>
        public static IdentityBuilder AddRavenIdentity<TUser, TRole>(
            this IServiceCollection services)
            where TUser : RavenIdentityUser
            where TRole : RavenIdentityRole
            => services.AddRavenIdentity<TUser, TRole>(setupAction: null);

        /// <summary>
        /// Adds and configures the RavenDB identity system for the specified User and Role types.
        /// </summary>
        /// <typeparam name="TUser">The type representing a User in the system.</typeparam>
        /// <typeparam name="TRole">The type representing a Role in the system.</typeparam>
        /// <param name="services">The services available in the application.</param>
        /// <param name="setupAction">An action to configure the <see cref="IdentityOptions"/>.</param>
        /// <returns>An <see cref="IdentityBuilder"/> for creating and configuring the identity system.</returns>
        /// <remarks>
        /// This method configures ASP.NET Core Identity with RavenDB using custom user and role types.
        /// </remarks>
        public static IdentityBuilder AddRavenIdentity<TUser, TRole>(
            this IServiceCollection services,
            Action<IdentityOptions> setupAction)
            where TUser : RavenIdentityUser
            where TRole : RavenIdentityRole
        {
            // Services used by identity - Authentication
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
            .AddCookie(IdentityConstants.TwoFactorRememberMeScheme, o =>
            {
                o.Cookie.Name = IdentityConstants.TwoFactorRememberMeScheme;
                o.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = SecurityStampValidator.ValidateAsync<ITwoFactorSecurityStampValidator>
                };
            })
            .AddCookie(IdentityConstants.TwoFactorUserIdScheme, o =>
            {
                o.Cookie.Name = IdentityConstants.TwoFactorUserIdScheme;
                o.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToReturnUrl = _ => Task.CompletedTask
                };
                o.ExpireTimeSpan = TimeSpan.FromMinutes(5);
            });

            // Hosting doesn't add IHttpContextAccessor by default
            services.AddHttpContextAccessor();

            // Services identity depends on
            services.AddLogging();
            services.AddMetrics();

            // Identity services
            services.TryAddScoped<IUserValidator<TUser>, RavenUserValidator<TUser>>();
            services.TryAddScoped<IPasswordValidator<TUser>, PasswordValidator<TUser>>();
            services.TryAddScoped<IPasswordHasher<TUser>, PasswordHasher<TUser>>();
            services.TryAddScoped<ILookupNormalizer, UpperInvariantLookupNormalizer>();
            services.TryAddScoped<IRoleValidator<TRole>, RavenRoleValidator<TRole>>();

            // No interface for the error describer so we can add errors without rev'ing the interface
            services.TryAddScoped<IdentityErrorDescriber>();
            services.TryAddScoped<ISecurityStampValidator, SecurityStampValidator<TUser>>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<SecurityStampValidatorOptions>, PostConfigureSecurityStampValidatorOptions>());
            services.TryAddScoped<ITwoFactorSecurityStampValidator, TwoFactorSecurityStampValidator<TUser>>();
            services.TryAddScoped<IUserClaimsPrincipalFactory<TUser>, UserClaimsPrincipalFactory<TUser, TRole>>();
            services.TryAddScoped<IUserConfirmation<TUser>, DefaultUserConfirmation<TUser>>();

            services.TryAddScoped<UserManager<TUser>>();
            services.TryAddScoped<SignInManager<TUser>>();
            services.TryAddScoped<RoleManager<TRole>>();

            if (setupAction != null)
            {
                services.Configure(setupAction);
            }

            return new IdentityBuilder(typeof(TUser), typeof(TRole), services);
        }

        #endregion

        #region AddRavenIdentityCore (Core Identity - No Authentication)

        /// <summary>
        /// Adds the minimal RavenDB identity system configuration for the specified User type.
        /// Authentication services are not added. Use this for scenarios where you need user management
        /// without cookie/bearer authentication (e.g., custom authentication schemes).
        /// </summary>
        /// <typeparam name="TUser">The type representing a User in the system.</typeparam>
        /// <param name="services">The services available in the application.</param>
        /// <returns>An <see cref="IdentityBuilder"/> for creating and configuring the identity system.</returns>
        /// <remarks>
        /// This is the minimal RavenDB Identity configuration. It does NOT add authentication services,
        /// SignInManager, or role services.
        /// </remarks>
        public static IdentityBuilder AddRavenIdentityCore<TUser>(
            this IServiceCollection services)
            where TUser : RavenIdentityUser
            => services.AddRavenIdentityCore<TUser>(o => { });

        /// <summary>
        /// Adds and configures the minimal RavenDB identity system for the specified User type.
        /// Authentication services are not added. Use this for scenarios where you need user management
        /// without cookie/bearer authentication (e.g., custom authentication schemes).
        /// </summary>
        /// <typeparam name="TUser">The type representing a User in the system.</typeparam>
        /// <param name="services">The services available in the application.</param>
        /// <param name="setupAction">An action to configure the <see cref="IdentityOptions"/>.</param>
        /// <returns>An <see cref="IdentityBuilder"/> for creating and configuring the identity system.</returns>
        /// <remarks>
        /// This is the minimal RavenDB Identity configuration. It does NOT add authentication services,
        /// SignInManager, or role services.
        /// </remarks>
        public static IdentityBuilder AddRavenIdentityCore<TUser>(
            this IServiceCollection services,
            Action<IdentityOptions> setupAction)
            where TUser : RavenIdentityUser
        {
            // Services identity depends on
            services.AddOptions().AddLogging();
            services.AddMetrics();

            // Core identity services (RavenDB-specific validators)
            services.TryAddScoped<IUserValidator<TUser>, RavenUserValidator<TUser>>();
            services.TryAddScoped<IPasswordValidator<TUser>, PasswordValidator<TUser>>();
            services.TryAddScoped<IPasswordHasher<TUser>, PasswordHasher<TUser>>();
            services.TryAddScoped<ILookupNormalizer, UpperInvariantLookupNormalizer>();
            services.TryAddScoped<IUserConfirmation<TUser>, DefaultUserConfirmation<TUser>>();

            // No interface for the error describer so we can add errors without rev'ing the interface
            services.TryAddScoped<IdentityErrorDescriber>();
            services.TryAddScoped<IUserClaimsPrincipalFactory<TUser>, UserClaimsPrincipalFactory<TUser>>();
            services.TryAddScoped<UserManager<TUser>>();

            if (setupAction != null)
            {
                services.Configure(setupAction);
            }

            return new IdentityBuilder(typeof(TUser), services);
        }

        #endregion

        #region AddRavenIdentityUserOnly (No Roles)

        /// <summary>
        /// Adds the default RavenDB identity system configuration for users only (no roles).
        /// </summary>
        /// <typeparam name="TUser">The type representing a User in the system.</typeparam>
        /// <param name="services">The services available in the application.</param>
        /// <returns>An <see cref="IdentityBuilder"/> for creating and configuring the identity system.</returns>
        public static IdentityBuilder AddRavenIdentityUserOnly<TUser>(
            this IServiceCollection services)
            where TUser : RavenIdentityUser
            => services.AddRavenIdentityUserOnly<TUser>(setupAction: null);

        /// <summary>
        /// Adds and configures the RavenDB identity system for users only (no roles).
        /// </summary>
        /// <typeparam name="TUser">The type representing a User in the system.</typeparam>
        /// <param name="services">The services available in the application.</param>
        /// <param name="setupAction">An action to configure the <see cref="IdentityOptions"/>.</param>
        /// <returns>An <see cref="IdentityBuilder"/> for creating and configuring the identity system.</returns>
        /// <remarks>
        /// This method configures ASP.NET Core Identity with RavenDB for user management without roles.
        /// </remarks>
        public static IdentityBuilder AddRavenIdentityUserOnly<TUser>(
            this IServiceCollection services,
            Action<IdentityOptions> setupAction)
            where TUser : RavenIdentityUser
        {
            // Services used by identity - Authentication
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
            .AddCookie(IdentityConstants.TwoFactorRememberMeScheme, o =>
            {
                o.Cookie.Name = IdentityConstants.TwoFactorRememberMeScheme;
                o.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = SecurityStampValidator.ValidateAsync<ITwoFactorSecurityStampValidator>
                };
            })
            .AddCookie(IdentityConstants.TwoFactorUserIdScheme, o =>
            {
                o.Cookie.Name = IdentityConstants.TwoFactorUserIdScheme;
                o.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToReturnUrl = _ => Task.CompletedTask
                };
                o.ExpireTimeSpan = TimeSpan.FromMinutes(5);
            });

            // Hosting doesn't add IHttpContextAccessor by default
            services.AddHttpContextAccessor();

            // Services identity depends on
            services.AddLogging();
            services.AddMetrics();

            // Identity services (no role services)
            services.TryAddScoped<IUserValidator<TUser>, RavenUserValidator<TUser>>();
            services.TryAddScoped<IPasswordValidator<TUser>, PasswordValidator<TUser>>();
            services.TryAddScoped<IPasswordHasher<TUser>, PasswordHasher<TUser>>();
            services.TryAddScoped<ILookupNormalizer, UpperInvariantLookupNormalizer>();

            // No interface for the error describer so we can add errors without rev'ing the interface
            services.TryAddScoped<IdentityErrorDescriber>();
            services.TryAddScoped<ISecurityStampValidator, SecurityStampValidator<TUser>>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<SecurityStampValidatorOptions>, PostConfigureSecurityStampValidatorOptions>());
            services.TryAddScoped<ITwoFactorSecurityStampValidator, TwoFactorSecurityStampValidator<TUser>>();
            services.TryAddScoped<IUserClaimsPrincipalFactory<TUser>, UserClaimsPrincipalFactory<TUser>>();
            services.TryAddScoped<IUserConfirmation<TUser>, DefaultUserConfirmation<TUser>>();

            services.TryAddScoped<UserManager<TUser>>();
            services.TryAddScoped<SignInManager<TUser>>();

            if (setupAction != null)
            {
                services.Configure(setupAction);
            }

            return new IdentityBuilder(typeof(TUser), services);
        }

        #endregion

        #region AddRavenIdentityApiEndpoints (API/Bearer Token Support)

        /// <summary>
        /// Adds a set of common RavenDB identity services to the application to support identity API endpoints
        /// and configures authentication to support identity bearer tokens and cookies.
        /// </summary>
        /// <typeparam name="TUser">The type representing a User in the system.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <returns>The <see cref="IdentityBuilder"/>.</returns>
        /// <remarks>
        /// Configures ASP.NET Core Identity with RavenDB for API scenarios using bearer tokens.
        /// Suitable for REST APIs, SPAs, and mobile applications.
        /// </remarks>
        public static IdentityBuilder AddRavenIdentityApiEndpoints<TUser>(this IServiceCollection services)
            where TUser : RavenIdentityUser, new()
            => services.AddRavenIdentityApiEndpoints<TUser>(_ => { });

        /// <summary>
        /// Adds a set of common RavenDB identity services to the application to support identity API endpoints
        /// and configures authentication to support identity bearer tokens and cookies.
        /// </summary>
        /// <typeparam name="TUser">The type representing a User in the system.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="configure">Configures the <see cref="IdentityOptions"/>.</param>
        /// <returns>The <see cref="IdentityBuilder"/>.</returns>
        /// <remarks>
        /// Configures ASP.NET Core Identity with RavenDB for API scenarios using bearer tokens.
        /// Suitable for REST APIs, SPAs, and mobile applications.
        /// </remarks>
        public static IdentityBuilder AddRavenIdentityApiEndpoints<TUser>(
            this IServiceCollection services,
            Action<IdentityOptions> configure)
            where TUser : RavenIdentityUser, new()
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            services
                .AddAuthentication(BearerAndApplicationScheme)
                .AddScheme<AuthenticationSchemeOptions, CompositeIdentityHandler>(BearerAndApplicationScheme, null, compositeOptions =>
                {
                    compositeOptions.ForwardDefault = IdentityConstants.BearerScheme;
                    compositeOptions.ForwardAuthenticate = BearerAndApplicationScheme;
                })
                .AddBearerToken(IdentityConstants.BearerScheme)
                .AddIdentityCookies();

            return services.AddRavenIdentityCore<TUser>(configure)
                .AddApiEndpoints();
        }

        #endregion

        #region Default Overloads (Standard Types)

        /// <summary>
        /// Adds the default RavenDB identity system configuration using standard RavenIdentityUser and RavenIdentityRole types.
        /// </summary>
        /// <param name="services">The services available in the application.</param>
        /// <returns>An <see cref="IdentityBuilder"/> for creating and configuring the identity system.</returns>
        public static IdentityBuilder AddRavenIdentity(this IServiceCollection services)
            => services.AddRavenIdentity<RavenIdentityUser, RavenIdentityRole>();

        /// <summary>
        /// Adds the default RavenDB identity system configuration using standard RavenIdentityUser and RavenIdentityRole types.
        /// </summary>
        /// <param name="services">The services available in the application.</param>
        /// <param name="setupAction">An action to configure the <see cref="IdentityOptions"/>.</param>
        /// <returns>An <see cref="IdentityBuilder"/> for creating and configuring the identity system.</returns>
        public static IdentityBuilder AddRavenIdentity(
            this IServiceCollection services,
            Action<IdentityOptions> setupAction)
            => services.AddRavenIdentity<RavenIdentityUser, RavenIdentityRole>(setupAction);

        /// <summary>
        /// Adds the default RavenDB identity system configuration for users only using standard RavenIdentityUser type.
        /// </summary>
        /// <param name="services">The services available in the application.</param>
        /// <returns>An <see cref="IdentityBuilder"/> for creating and configuring the identity system.</returns>
        public static IdentityBuilder AddRavenIdentityUserOnly(this IServiceCollection services)
            => services.AddRavenIdentityUserOnly<RavenIdentityUser>();

        /// <summary>
        /// Adds the default RavenDB identity system configuration for users only using standard RavenIdentityUser type.
        /// </summary>
        /// <param name="services">The services available in the application.</param>
        /// <param name="setupAction">An action to configure the <see cref="IdentityOptions"/>.</param>
        /// <returns>An <see cref="IdentityBuilder"/> for creating and configuring the identity system.</returns>
        public static IdentityBuilder AddRavenIdentityUserOnly(
            this IServiceCollection services,
            Action<IdentityOptions> setupAction)
            => services.AddRavenIdentityUserOnly<RavenIdentityUser>(setupAction);

        #endregion

        #region Cookie Configuration

        /// <summary>
        /// Configures the application cookie.
        /// </summary>
        /// <param name="services">The services available in the application.</param>
        /// <param name="configure">An action to configure the <see cref="CookieAuthenticationOptions"/>.</param>
        /// <returns>The services.</returns>
        public static IServiceCollection ConfigureApplicationCookie(
            this IServiceCollection services,
            Action<CookieAuthenticationOptions> configure)
            => services.Configure(IdentityConstants.ApplicationScheme, configure);

        /// <summary>
        /// Configures the external cookie.
        /// </summary>
        /// <param name="services">The services available in the application.</param>
        /// <param name="configure">An action to configure the <see cref="CookieAuthenticationOptions"/>.</param>
        /// <returns>The services.</returns>
        public static IServiceCollection ConfigureExternalCookie(
            this IServiceCollection services,
            Action<CookieAuthenticationOptions> configure)
            => services.Configure(IdentityConstants.ExternalScheme, configure);

        #endregion

        #region Helper Classes

        /// <summary>
        /// Post-configures SecurityStampValidatorOptions.
        /// </summary>
        private sealed class PostConfigureSecurityStampValidatorOptions : IPostConfigureOptions<SecurityStampValidatorOptions>
        {
            public PostConfigureSecurityStampValidatorOptions(TimeProvider? timeProvider = null)
            {
                // We could assign this to "timeProvider ?? TimeProvider.System", but
                // SecurityStampValidator already has system clock fallback logic.
                TimeProvider = timeProvider;
            }

            private TimeProvider? TimeProvider { get; }

            public void PostConfigure(string? name, SecurityStampValidatorOptions options)
            {
                options.TimeProvider ??= TimeProvider;
            }
        }

        /// <summary>
        /// Composite authentication handler that tries bearer token first, then falls back to application cookie.
        /// This allows API endpoints to accept both bearer tokens and cookie authentication.
        /// </summary>
        private sealed class CompositeIdentityHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : SignInAuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
        {
            protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
            {
                var bearerResult = await Context.AuthenticateAsync(IdentityConstants.BearerScheme);

                // Only try to authenticate with the application cookie if there is no bearer token.
                if (!bearerResult.None)
                {
                    return bearerResult;
                }

                // Cookie auth will return AuthenticateResult.NoResult() like bearer auth just did if there is no cookie.
                return await Context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            }

            protected override Task HandleSignInAsync(ClaimsPrincipal user, AuthenticationProperties? properties)
            {
                throw new NotImplementedException();
            }

            protected override Task HandleSignOutAsync(AuthenticationProperties? properties)
            {
                throw new NotImplementedException();
            }
        }

        #endregion

        #region Validator Configuration

        /// <summary>
        /// Configures optional validation options for RavenUserValidator to enforce username requirements.
        /// </summary>
        /// <param name="services">The services available in the application.</param>
        /// <param name="configure">An action to configure the <see cref="RavenUserValidatorOptions"/>.</param>
        /// <returns>The services.</returns>
        /// <remarks>
        /// Use this method to configure additional username validation rules.
        /// This extends Identity's built-in validation without replacing it.
        /// </remarks>
        public static IServiceCollection ConfigureRavenUserValidator(
            this IServiceCollection services,
            Action<RavenUserValidatorOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            services.Configure(configure);
            return services;
        }

        /// <summary>
        /// Configures optional validation options for RavenRoleValidator to enforce reserved role name checking.
        /// </summary>
        /// <param name="services">The services available in the application.</param>
        /// <param name="configure">An action to configure the <see cref="RavenRoleValidatorOptions"/>.</param>
        /// <returns>The services.</returns>
        /// <remarks>
        /// Use this method to configure blacklist/whitelist validation for role names.
        /// This extends Identity's built-in validation without replacing it.
        /// </remarks>
        public static IServiceCollection ConfigureRavenRoleValidator(
            this IServiceCollection services,
            Action<RavenRoleValidatorOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            services.Configure(configure);
            return services;
        }

        #endregion

        #region Store Options Configuration

        /// <summary>
        /// Configures RavenDB-specific user store options.
        /// </summary>
        /// <param name="services">The services available in the application.</param>
        /// <param name="configure">An action to configure the <see cref="RavenIdentityUserOptions"/>.</param>
        /// <returns>The services.</returns>
        /// <remarks>
        /// Configures RavenDB-specific user store behavior including UseStaticIndexes,
        /// AutoSaveChanges, and EnforceUniqueConstraints.
        /// </remarks>
        public static IServiceCollection ConfigureRavenIdentityUserStore(
            this IServiceCollection services,
            Action<RavenIdentityUserOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            services.Configure(configure);
            return services;
        }

        /// <summary>
        /// Configures RavenDB-specific user store options for a custom user type.
        /// </summary>
        /// <typeparam name="TUser">The type representing a user.</typeparam>
        /// <param name="services">The services available in the application.</param>
        /// <param name="configure">An action to configure the <see cref="RavenIdentityUserOptions{TUser}"/>.</param>
        /// <returns>The services.</returns>
        public static IServiceCollection ConfigureRavenIdentityUserStore<TUser>(
            this IServiceCollection services,
            Action<RavenIdentityUserOptions<TUser>> configure)
            where TUser : RavenIdentityUser
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            services.Configure(configure);
            return services;
        }

        /// <summary>
        /// Configures RavenDB-specific user store options for custom user and session types.
        /// </summary>
        /// <typeparam name="TUser">The type representing a user.</typeparam>
        /// <typeparam name="TSession">The type of the data context class used to access the session.</typeparam>
        /// <param name="services">The services available in the application.</param>
        /// <param name="configure">An action to configure the <see cref="RavenIdentityUserOptions{TUser, TSession}"/>.</param>
        /// <returns>The services.</returns>
        public static IServiceCollection ConfigureRavenIdentityUserStore<TUser, TSession>(
            this IServiceCollection services,
            Action<RavenIdentityUserOptions<TUser, TSession>> configure)
            where TUser : RavenIdentityUser
            where TSession : IAsyncDocumentSession
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            services.Configure<RavenIdentityUserOptions<TUser, TSession>>(configure);
            return services;
        }

        /// <summary>
        /// Configures RavenDB-specific role store options.
        /// </summary>
        /// <param name="services">The services available in the application.</param>
        /// <param name="configure">An action to configure the <see cref="RavenIdentityRoleOptions"/>.</param>
        /// <returns>The services.</returns>
        /// <remarks>
        /// Configures RavenDB-specific role store behavior including UseStaticIndexes,
        /// AutoSaveChanges, and EnforceUniqueConstraints.
        /// </remarks>
        public static IServiceCollection ConfigureRavenIdentityRoleStore(
            this IServiceCollection services,
            Action<RavenIdentityRoleOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            services.Configure(configure);
            return services;
        }

        /// <summary>
        /// Configures RavenDB-specific role store options for a custom role type.
        /// </summary>
        /// <typeparam name="TRole">The type representing a role.</typeparam>
        /// <param name="services">The services available in the application.</param>
        /// <param name="configure">An action to configure the <see cref="RavenIdentityRoleOptions{TRole}"/>.</param>
        /// <returns>The services.</returns>
        public static IServiceCollection ConfigureRavenIdentityRoleStore<TRole>(
            this IServiceCollection services,
            Action<RavenIdentityRoleOptions<TRole>> configure)
            where TRole : RavenIdentityRole
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            services.Configure(configure);
            return services;
        }

        /// <summary>
        /// Configures RavenDB-specific role store options for custom role and session types.
        /// </summary>
        /// <typeparam name="TRole">The type representing a role.</typeparam>
        /// <typeparam name="TSession">The type of the data context class used to access the session.</typeparam>
        /// <param name="services">The services available in the application.</param>
        /// <param name="configure">An action to configure the <see cref="RavenIdentityRoleOptions{TRole, TSession}"/>.</param>
        /// <returns>The services.</returns>
        public static IServiceCollection ConfigureRavenIdentityRoleStore<TRole, TSession>(
            this IServiceCollection services,
            Action<RavenIdentityRoleOptions<TRole, TSession>> configure)
            where TRole : RavenIdentityRole
            where TSession : IAsyncDocumentSession
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            services.Configure<RavenIdentityRoleOptions<TRole, TSession>>(configure);
            return services;
        }

        #endregion
    }
}
