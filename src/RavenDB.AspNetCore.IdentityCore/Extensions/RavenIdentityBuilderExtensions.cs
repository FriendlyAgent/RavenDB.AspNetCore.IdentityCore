using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Raven.Client.Documents.Session;
using RavenDB.AspNetCore.IdentityCore.Entities;
using System;
using System.Reflection;

namespace RavenDB.AspNetCore.IdentityCore.Extensions
{
    /// <summary>
    /// Contains extension methods to <see cref="IdentityBuilder"/> for adding ravendb compatibel stores.
    /// </summary>
    public static class RavenIdentityBuilderExtensions
    {
        /// <summary>
        /// Adds an ravendb implementation of identity information stores.
        /// </summary>
        /// <param name="builder">The <see cref="IdentityBuilder"/> instance this method extends.</param>
        /// <param name="getSession">The action used to get the desired session.</param>
        /// <returns>The <see cref="IdentityBuilder"/> instance this method extends.</returns>
        public static IdentityBuilder AddRavenStores(
            this IdentityBuilder builder,
            Func<IServiceProvider, IAsyncDocumentSession> getSession)
        {
            return builder.AddRavenStores<IAsyncDocumentSession>(getSession);
        }

        /// <summary>
        /// Adds an ravendb implementation of identity information stores.
        /// </summary>
        /// <param name="builder">The <see cref="IdentityBuilder"/> instance this method extends.</param>
        /// <returns>The <see cref="IdentityBuilder"/> instance this method extends.</returns>
        public static IdentityBuilder AddRavenStores(
            this IdentityBuilder builder)
        {
            return builder.AddRavenStores<IAsyncDocumentSession>();
        }

        /// <summary>
        /// Adds an ravendb implementation of identity information stores.
        /// </summary>
        /// <typeparam name="TSession">The type of the data context class used to access the session.</typeparam>
        /// <param name="builder">The <see cref="IdentityBuilder"/> instance this method extends.</param>
        /// <param name="getSession">The action used to get the desired session.</param>
        /// <returns>The <see cref="IdentityBuilder"/> instance this method extends.</returns>
        public static IdentityBuilder AddRavenStores<TSession>(
            this IdentityBuilder builder,
             Func<IServiceProvider, TSession> getSession)
            where TSession : IAsyncDocumentSession
        {
            AddStores(builder.Services, builder.UserType, builder.RoleType, getSession);
            return builder;
        }

        /// <summary>
        /// Adds an ravendb implementation of identity information stores.
        /// </summary>
        /// <typeparam name="TSession">The type of the data context class used to access the session.</typeparam>
        /// <param name="builder">The <see cref="IdentityBuilder"/> instance this method extends.</param>
        /// <returns>The <see cref="IdentityBuilder"/> instance this method extends.</returns>
        public static IdentityBuilder AddRavenStores<TSession>(
            this IdentityBuilder builder)
            where TSession : IAsyncDocumentSession
        {
            AddStores<TSession>(builder.Services, builder.UserType, builder.RoleType);
            return builder;
        }

        private static void AddStores<TSession>(
            IServiceCollection services,
            Type userType,
            Type roleType,
            Func<IServiceProvider, TSession> getSession)
                where TSession : IAsyncDocumentSession
        {
            var identityUserType = FindGenericBaseType(userType, typeof(IdentityUser<,,>));
            if (identityUserType == null)
                throw new InvalidOperationException();

            var identityRoleType = FindGenericBaseType(roleType, typeof(IdentityRole<>));
            if (identityRoleType == null)
                throw new InvalidOperationException();

            services.TryAddScoped(
                typeof(IUserStore<>).MakeGenericType(userType),
                provider =>
                {
                    var identityErrorDescriber = provider.GetService<IdentityErrorDescriber>();
                    var session = getSession(provider);

                    return Activator.CreateInstance(
                        typeof(UserStore<,,,,,,>).MakeGenericType(
                            userType,
                            roleType,
                            typeof(TSession),
                            identityUserType.GenericTypeArguments[0],
                            identityUserType.GenericTypeArguments[1],
                            identityUserType.GenericTypeArguments[2],
                            identityRoleType.GenericTypeArguments[0]),
                        new object[] {
                        session,
                        identityErrorDescriber
                    });
                });

            services.TryAddScoped(
                typeof(IRoleStore<>).MakeGenericType(roleType),
                provider =>
                {
                    var identityErrorDescriber = provider.GetService<IdentityErrorDescriber>();
                    var session = getSession(provider);

                    return Activator.CreateInstance(typeof(
                        RoleStore<,,>).MakeGenericType(
                            roleType,
                            typeof(TSession),
                            identityRoleType.GenericTypeArguments[0]),
                        new object[] {
                        session,
                        identityErrorDescriber
                    });
                });
        }

        private static void AddStores<TSession>(
            IServiceCollection services,
            Type userType,
            Type roleType)
            where TSession : IAsyncDocumentSession
        {
            var identityUserType = FindGenericBaseType(userType, typeof(IdentityUser<,,>));
            if (identityUserType == null)
                throw new InvalidOperationException();

            var identityRoleType = FindGenericBaseType(roleType, typeof(IdentityRole<>));
            if (identityRoleType == null)
                throw new InvalidOperationException();

            services.TryAddScoped(
                typeof(IUserStore<>).MakeGenericType(userType),
                typeof(UserStore<,,,,,,>).MakeGenericType(
                    userType,
                    roleType,
                    typeof(TSession),
                    identityUserType.GenericTypeArguments[0],
                    identityUserType.GenericTypeArguments[1],
                    identityUserType.GenericTypeArguments[2],
                    identityRoleType.GenericTypeArguments[0]));

            services.TryAddScoped(
                typeof(IRoleStore<>).MakeGenericType(roleType),
                typeof(RoleStore<,,>).MakeGenericType(
                    roleType,
                    typeof(TSession),
                    identityRoleType.GenericTypeArguments[0]));
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
    }
}