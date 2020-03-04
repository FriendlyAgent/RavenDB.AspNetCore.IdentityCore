using RavenDB.Test.Shared;
using Xunit;

namespace RavenDB.AspNetCore.IdentityCore.Tests
{
    public class RoleStoreTests
        : RavenDBTestBase
    {
        [Theory(DisplayName = "Role: Create a role")]
        [InlineData("Admin")]
        public void CreateAsync(string roleName)
        {
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenAsyncSession())
                {
                    var roleStore = new RavenRoleStore<TestRole>(session);
                    var role = new TestRole()
                    {
                        RoleName = roleName
                    };

                    var result = roleStore.CreateAsync(role).Result;
                    IdentityResultAssert.IsSuccess(result);

                    var retrievedUser = roleStore.FindByIdAsync(role.Id).Result;
                    Assert.NotNull(retrievedUser);
                }
            }
        }

        [Theory(DisplayName = "Role: Create multiple roles with the same roleName")]
        [InlineData("Admin")]
        public void CreateAsync_Same_Rolename(string roleName)
        {
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenAsyncSession())
                {
                    var roleStore = new RavenRoleStore<TestRole>(session);
                    var roleA = new TestRole()
                    {
                        RoleName = roleName
                    };
                    var resultA = roleStore.CreateAsync(roleA).Result;
                    IdentityResultAssert.IsSuccess(resultA);

                    var roleB = new TestRole()
                    {
                        RoleName = roleName
                    };
                    var resultB = roleStore.CreateAsync(roleB).Result;
                    IdentityResultAssert.IsFailure(resultB);

                    var retrievedUser = roleStore.FindByIdAsync(roleA.Id).Result;
                    Assert.NotNull(retrievedUser);
                }
            }
        }
    }
}
