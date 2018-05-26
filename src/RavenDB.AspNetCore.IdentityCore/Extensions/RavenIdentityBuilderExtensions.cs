using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
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
             Func<IServiceProvider, IAsyncDocumentSession> getSession = null)
        {
            return builder.AddRavenStores<IAsyncDocumentSession>(getSession);
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
             Func<IServiceProvider, TSession> getSession = null)
            where TSession : IAsyncDocumentSession
        {
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
            var identityUserType = FindGenericBaseType(userType, typeof(IdentityUser<,,>));
            if (identityUserType == null)
                throw new InvalidOperationException();

            if (roleType != null)
            {
                var identityRoleType = FindGenericBaseType(roleType, typeof(IdentityRole<>));
                if (identityRoleType == null)
                    throw new InvalidOperationException();

                var genericUserType = typeof(UserStore<,,,,,,>).MakeGenericType(
                    userType,
                    roleType,
                    typeof(TSession),
                    identityUserType.GenericTypeArguments[0],
                    identityUserType.GenericTypeArguments[1],
                    identityUserType.GenericTypeArguments[2],
                    identityRoleType.GenericTypeArguments[0]);

                var genericRoleType = typeof(RoleStore<,,>).MakeGenericType(
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

                        return Activator.CreateInstance(
                            genericUserType,
                            new object[] {
                                    session,
                                    identityErrorDescriber,
                                    option
                            });
                    });

                    services.TryAddScoped(
                    typeof(IRoleStore<>).MakeGenericType(roleType),
                    provider =>
                    {
                        var identityErrorDescriber = provider.GetService<IdentityErrorDescriber>();
                        var session = getSession(provider);

                        return Activator.CreateInstance(
                                genericRoleType,
                                new object[] {
                                    session,
                                    identityErrorDescriber
                                });
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
                throw new NotImplementedException();
            }
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