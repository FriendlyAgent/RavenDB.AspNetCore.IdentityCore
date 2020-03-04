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
        /// Adds an RavenDB implementation of identity information stores.
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
        /// Adds an RavenDB implementation of identity information stores.
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
            var IdentityUserType = FindGenericBaseType(userType, typeof(RavenIdentityUser<,,>));
            if (IdentityUserType == null)
                throw new InvalidOperationException();

            if (roleType != null)
            {
                var identityRoleType = FindGenericBaseType(roleType, typeof(RavenIdentityRole<>));
                if (identityRoleType == null)
                    throw new InvalidOperationException();

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

                        return Activator.CreateInstance(
                            genericUserType,
                            new object[] {
                                    session,
                                    identityErrorDescriber,
                                    option,
                                    userOptions,
                                    roleOptions
                            });
                    });

                    services.TryAddScoped(
                    typeof(IRoleStore<>).MakeGenericType(roleType),
                    provider =>
                    {
                        var identityErrorDescriber = provider.GetService<IdentityErrorDescriber>();
                        var session = getSession(provider);
                        var roleOptions = GetRavenIdentityRoleOptions<TSession>(provider, roleType);

                        return Activator.CreateInstance(
                                genericRoleType,
                                new object[] {
                                    session,
                                    identityErrorDescriber,
                                    roleOptions
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

                        return Activator.CreateInstance(
                            genericUserType,
                            new object[] {
                                    session,
                                    identityErrorDescriber,
                                    option,
                                    userOptions
                            });
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