using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Moq;
using RavenDB.Test.Shared;
using System;
using Xunit;

namespace RavenDB.AspNetCore.IdentityCore.Tests
{
    public class UserStoreTests
        : RavenDBTestBase
    {
        private void CreateUserWithRole(
            RavenUserStore<TestUser, TestRole> userStore,
            RavenRoleStore<TestRole> roleStore,
            TestUser user,
            TestRole role)
        {
            var resultUserCreate = userStore.CreateAsync(user).Result;
            IdentityResultAssert.IsSuccess(resultUserCreate);

            var resultRole = roleStore.CreateAsync(role).Result;
            IdentityResultAssert.IsSuccess(resultRole);

            userStore.AddToRoleAsync(user, role.NormalizedRoleName).Wait();

            var resultUserUpdate = userStore.UpdateAsync(user).Result;
            IdentityResultAssert.IsSuccess(resultUserUpdate);

            var retrievedUser = userStore.FindByIdAsync(user.Id).Result;
            Assert.NotNull(retrievedUser);
            Assert.Contains(role.Id, retrievedUser.Roles, StringComparer.OrdinalIgnoreCase);
        }

        [Theory(DisplayName = "User: Add role to user")]
        [InlineData("Test", "Admin")]
        public void UserAddToRoleAsync(string username, string rolename)
        {
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenAsyncSession())
                {
                    var options = new Mock<IOptions<IdentityOptions>>();
                    var identityOptions = new IdentityOptions()
                    {
                        User = new UserOptions()
                        {
                            RequireUniqueEmail = false
                        }
                    };
                    options.Setup(a => a.Value).Returns(identityOptions);

                    var userStore = new RavenUserStore<TestUser, TestRole>(session, optionsAccessor: options.Object);
                    var roleStore = new RavenRoleStore<TestRole>(session);

                    var user = new TestUser()
                    {
                        UserName = username
                    };

                    var role = new TestRole()
                    {
                        RoleName = rolename,
                        NormalizedRoleName = rolename.ToLower()
                    };

                    CreateUserWithRole(userStore, roleStore, user, role);
                }
            }
        }

        [Theory(DisplayName = "User: Remove role from user")]
        [InlineData("Test", "Admin")]
        public void RemoveFromRoleAsync(string username, string rolename)
        {
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenAsyncSession())
                {
                    var options = new Mock<IOptions<IdentityOptions>>();
                    var identityOptions = new IdentityOptions()
                    {
                        User = new UserOptions()
                        {
                            RequireUniqueEmail = false
                        }
                    };
                    options.Setup(a => a.Value).Returns(identityOptions);

                    var userStore = new RavenUserStore<TestUser, TestRole>(session, optionsAccessor: options.Object);
                    var roleStore = new RavenRoleStore<TestRole>(session);

                    var user = new TestUser()
                    {
                        UserName = username
                    };

                    var role = new TestRole()
                    {
                        RoleName = rolename,
                        NormalizedRoleName = rolename.ToLower()
                    };

                    CreateUserWithRole(userStore, roleStore, user, role);

                    userStore.RemoveFromRoleAsync(user, role.NormalizedRoleName).Wait();

                    var resultUserUpdate = userStore.UpdateAsync(user).Result;
                    IdentityResultAssert.IsSuccess(resultUserUpdate);

                    var retrievedUser = userStore.FindByIdAsync(user.Id).Result;
                    Assert.NotNull(retrievedUser);
                    Assert.DoesNotContain(role.Id, retrievedUser.Roles, StringComparer.OrdinalIgnoreCase);
                }
            }
        }
    }
}
