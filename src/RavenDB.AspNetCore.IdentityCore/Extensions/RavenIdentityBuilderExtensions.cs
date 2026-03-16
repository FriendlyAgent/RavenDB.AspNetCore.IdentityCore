using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Session;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.AspNetCore.IdentityCore.QueryHandlers;
using Microsoft.Extensions.Logging;
using System;
using System.Reflection;

namespace RavenDB.AspNetCore.IdentityCore.Extensions
{
    /// <summary>
    /// Contains extension methods to <see cref="IdentityBuilder"/> for adding ravendb compatible stores.
    /// </summary>
    public static class RavenIdentityBuilderExtensions
    {
        /// <summary>
        /// Adds an RavenDB implementation of identity information stores.
        /// </summary>
        /// <param name="builder">The <see cref="IdentityBuilder"/> instance this method extends.</param>
        /// <param name="getSession">
        /// Optional factory function to retrieve the RavenDB session.
        /// If null, the session will be resolved from the dependency injection container.
        /// If provided, allows custom session retrieval logic such as multi-tenancy scenarios.
        /// </param>
        /// <returns>The <see cref="IdentityBuilder"/> instance this method extends.</returns>
        /// <remarks>
        /// This method registers the RavenDB user and role stores with the identity system.
        /// </remarks>
        public static IdentityBuilder AddRavenStores(
            this IdentityBuilder builder,
             Func<IServiceProvider, IAsyncDocumentSession> getSession = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            return builder.AddRavenStores<IAsyncDocumentSession>(getSession);
        }

        /// <summary>
        /// Adds an RavenDB implementation of identity information stores with a custom session type.
        /// </summary>
        /// <typeparam name="TSession">
        /// The type of the session class used to access RavenDB. Must inherit from <see cref="IAsyncDocumentSession"/>.
        /// Use this when you have a custom session wrapper or abstraction.
        /// </typeparam>
        /// <param name="builder">The <see cref="IdentityBuilder"/> instance this method extends.</param>
        /// <param name="getSession">
        /// Optional factory function to retrieve the custom RavenDB session.
        /// If null, the session will be resolved from the dependency injection container.
        /// If provided, allows custom session retrieval logic such as multi-tenancy or custom session wrappers.
        /// </param>
        /// <returns>The <see cref="IdentityBuilder"/> instance this method extends.</returns>
        /// <remarks>
        /// This generic overload allows you to use a custom session type that implements <see cref="IAsyncDocumentSession"/>.
        /// This is useful when you have additional session functionality or abstraction layers.
        /// </remarks>
        public static IdentityBuilder AddRavenStores<TSession>(
            this IdentityBuilder builder,
             Func<IServiceProvider, TSession> getSession = null)
            where TSession : IAsyncDocumentSession
        {
            ArgumentNullException.ThrowIfNull(builder);
            AddStores(builder.Services, builder.UserType, builder.RoleType, getSession);
            return builder;
        }

        private static void AddStores<TSession>(
            IServiceCollection services,
            Type userType,
            Type roleType,
            Func<IServiceProvider, TSession> getSession = null)
            where TSession : IAsyncDocumentSession
        {
            var IdentityUserType = FindGenericBaseType(userType, typeof(RavenIdentityUser<,,>))
                ?? throw new InvalidOperationException($"The user type '{userType.Name}' must derive from RavenIdentityUser<,,>.");

            // Register default query handlers (can be overridden by calling AddUserQueryHandler,
            // AddDefaultUserQueryHandlerWithCustomIndex, etc. AFTER AddRavenStores)
            var userQueryHandlerInterface = typeof(IUserQueryHandler<,>).MakeGenericType(userType, typeof(TSession));
            var defaultUserQueryHandler = typeof(DefaultUserQueryHandler<,>).MakeGenericType(userType, typeof(TSession));
            services.TryAddScoped(userQueryHandlerInterface, defaultUserQueryHandler);

            if (roleType != null)
            {
                var roleQueryHandlerInterface = typeof(IRoleQueryHandler<,>).MakeGenericType(roleType, typeof(TSession));
                var defaultRoleQueryHandler = typeof(DefaultRoleQueryHandler<,>).MakeGenericType(roleType, typeof(TSession));
                services.TryAddScoped(roleQueryHandlerInterface, defaultRoleQueryHandler);

                var identityRoleType = FindGenericBaseType(roleType, typeof(RavenIdentityRole<>))
                    ?? throw new InvalidOperationException($"The role type '{roleType.Name}' must derive from RavenIdentityRole<>.");

                var genericUserType = typeof(RavenUserStore<,,,,,,>).MakeGenericType(
                    userType,
                    roleType,
                    typeof(TSession),
                    IdentityUserType.GenericTypeArguments[0],
                    IdentityUserType.GenericTypeArguments[1],
                    IdentityUserType.GenericTypeArguments[2],
                    identityRoleType.GenericTypeArguments[0]);

                var genericRoleType = typeof(RavenRoleStore<,,>).MakeGenericType(
                    roleType,
                    typeof(TSession),
                    identityRoleType.GenericTypeArguments[0]);

                if (getSession != null)
                {
                    services.TryAddScoped(
                    typeof(IUserStore<>).MakeGenericType(userType),
                    provider =>
                    {
                        var identityErrorDescriber = provider.GetService<IdentityErrorDescriber>();
                        var option = provider.GetService<IOptions<IdentityOptions>>();
                        var session = getSession(provider);

                        var userOptions = GetRavenIdentityUserOptions<TSession>(provider, userType);
                        var roleOptions = GetRavenIdentityRoleOptions<TSession>(provider, roleType);

                        var userQueryHandler = GetUserQueryHandler<TSession>(provider, userType);
                        var roleQueryHandler = GetRoleQueryHandler<TSession>(provider, roleType);
                        var loggerFactory = provider.GetService<ILoggerFactory>();

                        return Activator.CreateInstance(
                            genericUserType,
                            [
                                    session,
                                    identityErrorDescriber,
                                    option,
                                    userOptions,
                                    roleOptions,
                                    userQueryHandler,
                                    roleQueryHandler,
                                    loggerFactory
                            ]);
                    });

                    services.TryAddScoped(
                    typeof(IRoleStore<>).MakeGenericType(roleType),
                    provider =>
                    {
                        var identityErrorDescriber = provider.GetService<IdentityErrorDescriber>();
                        var session = getSession(provider);
                        var roleOptions = GetRavenIdentityRoleOptions<TSession>(provider, roleType);
                        var roleQueryHandler = GetRoleQueryHandler<TSession>(provider, roleType);
                        var loggerFactory = provider.GetService<ILoggerFactory>();

                        return Activator.CreateInstance(
                                genericRoleType,
                                [
                                    session,
                                    identityErrorDescriber,
                                    roleOptions,
                                    roleQueryHandler,
                                    loggerFactory
                                ]);
                    });
                }
                else
                {
                    services.TryAddScoped(
                       typeof(IUserStore<>).MakeGenericType(userType),
                      genericUserType);

                    services.TryAddScoped(
                        typeof(IRoleStore<>).MakeGenericType(roleType),
                        genericRoleType);
                }
            }
            else
            {
                var genericUserType = typeof(RavenUserOnlyStore<,,,,>)
                    .MakeGenericType(
                        userType,
                        typeof(TSession),
                        IdentityUserType.GenericTypeArguments[0],
                        IdentityUserType.GenericTypeArguments[1],
                        IdentityUserType.GenericTypeArguments[2]);

                if (getSession != null)
                {
                    services.TryAddScoped(
                    typeof(IUserStore<>).MakeGenericType(userType),
                    provider =>
                    {
                        var identityErrorDescriber = provider.GetService<IdentityErrorDescriber>();
                        var option = provider.GetService<IOptions<IdentityOptions>>();
                        var session = getSession(provider);
                        var userOptions = GetRavenIdentityUserOptions<TSession>(provider, userType);
                        var userQueryHandler = GetUserQueryHandler<TSession>(provider, userType);
                        var loggerFactory = provider.GetService<ILoggerFactory>();

                        return Activator.CreateInstance(
                            genericUserType,
                            [
                                    session,
                                    identityErrorDescriber,
                                    option,
                                    userOptions,
                                    userQueryHandler,
                                    loggerFactory
                            ]);
                    });
                }
                else
                {
                    services.TryAddScoped(
                        typeof(IUserStore<>).MakeGenericType(userType),
                        genericUserType);
                }
            }
        }

        private static object GetRavenIdentityUserOptions<TSession>(IServiceProvider provider, Type userType)
            where TSession : IAsyncDocumentSession
        {
            var userOption = typeof(RavenIdentityUserOptions<,>)
                .MakeGenericType(
                    userType,
                    typeof(TSession));

            var optionType = typeof(IOptions<>)
                .MakeGenericType(userOption);

            return provider
                .GetService(optionType);
        }

        private static object GetRavenIdentityRoleOptions<TSession>(IServiceProvider provider, Type roleType)
            where TSession : IAsyncDocumentSession
        {
            var roleOptionType = typeof(RavenIdentityRoleOptions<,>)
                .MakeGenericType(
                    roleType,
                    typeof(TSession));

            var optionType = typeof(IOptions<>)
                .MakeGenericType(roleOptionType);

            return provider
                .GetService(optionType);
        }

        private static object GetUserQueryHandler<TSession>(IServiceProvider provider, Type userType)
            where TSession : IAsyncDocumentSession
        {
            var userQueryHandlerType = typeof(IUserQueryHandler<,>)
                .MakeGenericType(
                    userType,
                    typeof(TSession));

            return provider.GetService(userQueryHandlerType);
        }

        private static object GetRoleQueryHandler<TSession>(IServiceProvider provider, Type roleType)
            where TSession : IAsyncDocumentSession
        {
            var roleQueryHandlerType = typeof(IRoleQueryHandler<,>)
                .MakeGenericType(
                    roleType,
                    typeof(TSession));

            return provider.GetService(roleQueryHandlerType);
        }

        private static TypeInfo FindGenericBaseType(Type currentType, Type genericBaseType)
        {
            var type = currentType.GetTypeInfo();
            while (type.BaseType != null)
            {
                type = type.BaseType.GetTypeInfo();
                var genericType = type.IsGenericType ? type.GetGenericTypeDefinition() : null;
                if (genericType != null && genericType == genericBaseType)
                    return type;
            }
            return null;
        }

        #region Query Handler Registration

        // --- Custom handler implementations ---

        /// <summary>
        /// Adds a custom user query handler implementation.
        /// </summary>
        /// <typeparam name="TQueryHandler">The type of the custom user query handler.</typeparam>
        /// <param name="builder">The <see cref="IdentityBuilder"/> instance this method extends.</param>
        /// <returns>The <see cref="IdentityBuilder"/> instance this method extends.</returns>
        /// <remarks>
        /// Use this method to register a custom implementation of <see cref="IUserQueryHandler{TUser, TSession}"/>.
        /// If not called, the default <see cref="DefaultUserQueryHandler{TUser, TSession}"/> will be used.
        /// </remarks>
        public static IdentityBuilder AddUserQueryHandler<TQueryHandler>(this IdentityBuilder builder)
            where TQueryHandler : class
        {
            ArgumentNullException.ThrowIfNull(builder);
            var userType = builder.UserType;
            var queryHandlerInterface = typeof(IUserQueryHandler<,>).MakeGenericType(userType, typeof(IAsyncDocumentSession));

            builder.Services.Replace(ServiceDescriptor.Scoped(queryHandlerInterface, typeof(TQueryHandler)));

            return builder;
        }

        /// <summary>
        /// Adds a custom user query handler implementation with a custom session type.
        /// </summary>
        /// <typeparam name="TQueryHandler">The type of the custom user query handler.</typeparam>
        /// <typeparam name="TSession">The type of the session class used to access RavenDB.</typeparam>
        /// <param name="builder">The <see cref="IdentityBuilder"/> instance this method extends.</param>
        /// <returns>The <see cref="IdentityBuilder"/> instance this method extends.</returns>
        public static IdentityBuilder AddUserQueryHandler<TQueryHandler, TSession>(this IdentityBuilder builder)
            where TQueryHandler : class
            where TSession : IAsyncDocumentSession
        {
            ArgumentNullException.ThrowIfNull(builder);
            var userType = builder.UserType;
            var queryHandlerInterface = typeof(IUserQueryHandler<,>).MakeGenericType(userType, typeof(TSession));

            builder.Services.Replace(ServiceDescriptor.Scoped(queryHandlerInterface, typeof(TQueryHandler)));

            return builder;
        }

        /// <summary>
        /// Adds a custom role query handler implementation.
        /// </summary>
        /// <typeparam name="TQueryHandler">The type of the custom role query handler.</typeparam>
        /// <param name="builder">The <see cref="IdentityBuilder"/> instance this method extends.</param>
        /// <returns>The <see cref="IdentityBuilder"/> instance this method extends.</returns>
        /// <remarks>
        /// Use this method to register a custom implementation of <see cref="IRoleQueryHandler{TRole, TSession}"/>.
        /// If not called, the default <see cref="DefaultRoleQueryHandler{TRole, TSession}"/> will be used.
        /// </remarks>
        public static IdentityBuilder AddRoleQueryHandler<TQueryHandler>(this IdentityBuilder builder)
            where TQueryHandler : class
        {
            ArgumentNullException.ThrowIfNull(builder);
            if (builder.RoleType == null)
                throw new InvalidOperationException("Role type is not configured. Use AddRavenIdentity<TUser, TRole> to enable role support.");

            var roleType = builder.RoleType;
            var queryHandlerInterface = typeof(IRoleQueryHandler<,>).MakeGenericType(roleType, typeof(IAsyncDocumentSession));

            builder.Services.Replace(ServiceDescriptor.Scoped(queryHandlerInterface, typeof(TQueryHandler)));

            return builder;
        }

        /// <summary>
        /// Adds a custom role query handler implementation with a custom session type.
        /// </summary>
        /// <typeparam name="TQueryHandler">The type of the custom role query handler.</typeparam>
        /// <typeparam name="TSession">The type of the session class used to access RavenDB.</typeparam>
        /// <param name="builder">The <see cref="IdentityBuilder"/> instance this method extends.</param>
        /// <returns>The <see cref="IdentityBuilder"/> instance this method extends.</returns>
        public static IdentityBuilder AddRoleQueryHandler<TQueryHandler, TSession>(this IdentityBuilder builder)
            where TQueryHandler : class
            where TSession : IAsyncDocumentSession
        {
            ArgumentNullException.ThrowIfNull(builder);
            if (builder.RoleType == null)
                throw new InvalidOperationException("Role type is not configured. Use AddRavenIdentity<TUser, TRole> to enable role support.");

            var roleType = builder.RoleType;
            var queryHandlerInterface = typeof(IRoleQueryHandler<,>).MakeGenericType(roleType, typeof(TSession));

            builder.Services.Replace(ServiceDescriptor.Scoped(queryHandlerInterface, typeof(TQueryHandler)));

            return builder;
        }

        // --- Default handler with custom index ---

        /// <summary>
        /// Registers the default user query handler with a custom static index.
        /// </summary>
        /// <typeparam name="TCustomIndex">The static index type to use for user queries.</typeparam>
        /// <param name="builder">The <see cref="IdentityBuilder"/> instance this method extends.</param>
        /// <returns>The <see cref="IdentityBuilder"/> instance this method extends.</returns>
        /// <remarks>
        /// Use this when you have a custom user type (e.g. ApplicationUser) and a custom index
        /// that targets the correct document collection. The custom index should have the same
        /// fields as <see cref="Indexes.IdentityUserIndex{TUser}"/>.
        /// Requires <see cref="RavenIdentityUserOptions{TUser, TSession}.UseStaticIndexes"/> to be set to true.
        /// </remarks>
        public static IdentityBuilder AddDefaultUserQueryHandlerWithCustomIndex<TCustomIndex>(this IdentityBuilder builder)
            where TCustomIndex : AbstractCommonApiForIndexes, new()
        {
            ArgumentNullException.ThrowIfNull(builder);
            return builder.AddDefaultUserQueryHandlerWithCustomIndex<TCustomIndex, IAsyncDocumentSession>();
        }

        /// <summary>
        /// Registers the default user query handler with a custom static index and a custom session type.
        /// </summary>
        /// <typeparam name="TCustomIndex">The static index type to use for user queries.</typeparam>
        /// <typeparam name="TSession">The type of the session class used to access RavenDB.</typeparam>
        /// <param name="builder">The <see cref="IdentityBuilder"/> instance this method extends.</param>
        /// <returns>The <see cref="IdentityBuilder"/> instance this method extends.</returns>
        public static IdentityBuilder AddDefaultUserQueryHandlerWithCustomIndex<TCustomIndex, TSession>(this IdentityBuilder builder)
            where TCustomIndex : AbstractCommonApiForIndexes, new()
            where TSession : IAsyncDocumentSession
        {
            ArgumentNullException.ThrowIfNull(builder);
            var userType = builder.UserType;
            var queryHandlerInterface = typeof(IUserQueryHandler<,>).MakeGenericType(userType, typeof(TSession));
            var handlerType = typeof(DefaultUserQueryHandler<,,>).MakeGenericType(userType, typeof(TSession), typeof(TCustomIndex));

            builder.Services.Replace(ServiceDescriptor.Scoped(queryHandlerInterface, handlerType));

            return builder;
        }

        /// <summary>
        /// Registers the default role query handler with a custom static index.
        /// </summary>
        /// <typeparam name="TCustomIndex">The static index type to use for role queries.</typeparam>
        /// <param name="builder">The <see cref="IdentityBuilder"/> instance this method extends.</param>
        /// <returns>The <see cref="IdentityBuilder"/> instance this method extends.</returns>
        /// <remarks>
        /// Use this when you have a custom role type and a custom index that targets the correct
        /// document collection. The custom index should have the same fields as
        /// <see cref="Indexes.IdentityRoleIndex{TRole}"/>.
        /// Requires <see cref="RavenIdentityRoleOptions{TRole, TSession}.UseStaticIndexes"/> to be set to true.
        /// </remarks>
        public static IdentityBuilder AddDefaultRoleQueryHandlerWithCustomIndex<TCustomIndex>(this IdentityBuilder builder)
            where TCustomIndex : AbstractCommonApiForIndexes, new()
        {
            ArgumentNullException.ThrowIfNull(builder);
            return builder.AddDefaultRoleQueryHandlerWithCustomIndex<TCustomIndex, IAsyncDocumentSession>();
        }

        /// <summary>
        /// Registers the default role query handler with a custom static index and a custom session type.
        /// </summary>
        /// <typeparam name="TCustomIndex">The static index type to use for role queries.</typeparam>
        /// <typeparam name="TSession">The type of the session class used to access RavenDB.</typeparam>
        /// <param name="builder">The <see cref="IdentityBuilder"/> instance this method extends.</param>
        /// <returns>The <see cref="IdentityBuilder"/> instance this method extends.</returns>
        public static IdentityBuilder AddDefaultRoleQueryHandlerWithCustomIndex<TCustomIndex, TSession>(this IdentityBuilder builder)
            where TCustomIndex : AbstractCommonApiForIndexes, new()
            where TSession : IAsyncDocumentSession
        {
            ArgumentNullException.ThrowIfNull(builder);
            if (builder.RoleType == null)
                throw new InvalidOperationException("Role type is not configured. Use AddRavenIdentity<TUser, TRole> to enable role support.");

            var roleType = builder.RoleType;
            var queryHandlerInterface = typeof(IRoleQueryHandler<,>).MakeGenericType(roleType, typeof(TSession));
            var handlerType = typeof(DefaultRoleQueryHandler<,,>).MakeGenericType(roleType, typeof(TSession), typeof(TCustomIndex));

            builder.Services.Replace(ServiceDescriptor.Scoped(queryHandlerInterface, handlerType));

            return builder;
        }

        #endregion
    }
}