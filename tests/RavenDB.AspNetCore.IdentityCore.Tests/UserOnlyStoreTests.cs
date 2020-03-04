using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Moq;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.Test.Shared;
using Xunit;

namespace RavenDB.AspNetCore.IdentityCore.Tests
{
    public class UserOnlyStoreTests
        : RavenDBTestBase
    {
        [Theory(DisplayName = "User: Create a user")]
        [InlineData("Admin", null, false)]
        [InlineData("Admin", "test@test.com", false)]
        [InlineData("Admin", "test@test.com", true)]
        public void CreateAsync(string username, string email, bool emailRequired)
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
                            RequireUniqueEmail = emailRequired
                        }
                    };
                    options.Setup(a => a.Value).Returns(identityOptions);

                    var userStore = new RavenUserOnlyStore<TestUser>(session, optionsAccessor: options.Object);
                    var user = new TestUser()
                    {
                        UserName = username
                    };

                    if (email != null)
                        user.Email = new RavenIdentityUserEmail(email);

                    var result = userStore.CreateAsync(user).Result;
                    IdentityResultAssert.IsSuccess(result);

                    var retrievedUser = userStore.FindByIdAsync(user.Id).Result;
                    Assert.NotNull(retrievedUser);
                }
            }
        }

        [Theory(DisplayName = "User: Create multiple users with the same username")]
        [InlineData("Admin")]
        public void CreateAsync_Same_Username(string username)
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

                    var userStore = new RavenUserOnlyStore<TestUser>(session, optionsAccessor: options.Object);
                    var userA = new TestUser()
                    {
                        UserName = username
                    };
                    var resultA = userStore.CreateAsync(userA).Result;
                    IdentityResultAssert.IsSuccess(resultA);

                    var userB = new TestUser()
                    {
                        UserName = username
                    };
                    var resultB = userStore.CreateAsync(userB).Result;
                    IdentityResultAssert.IsFailure(resultB);

                    var retrievedUser = userStore.FindByIdAsync(userA.Id).Result;
                    Assert.NotNull(retrievedUser);
                }
            }
        }

        [Theory(DisplayName = "User: Create multiple users with the same email")]
        [InlineData("Admin", "Test", "test@test.com")]
        public void CreateAsync_Same_Email(string usernameA, string usernameB, string email)
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
                            RequireUniqueEmail = true
                        }
                    };
                    options.Setup(a => a.Value).Returns(identityOptions);

                    var userStore = new RavenUserOnlyStore<TestUser>(session, optionsAccessor: options.Object);
                    var userA = new TestUser()
                    {
                        UserName = usernameA,
                        Email = new RavenIdentityUserEmail(email)
                    };
                    var resultA = userStore.CreateAsync(userA).Result;
                    IdentityResultAssert.IsSuccess(resultA);

                    var userB = new TestUser()
                    {
                        UserName = usernameB,
                        Email = new RavenIdentityUserEmail(email)
                    };
                    var resultB = userStore.CreateAsync(userB).Result;
                    IdentityResultAssert.IsFailure(resultB);

                    var retrievedUser = userStore.FindByIdAsync(userA.Id).Result;
                    Assert.NotNull(retrievedUser);
                }
            }
        }

        [Theory(DisplayName = "User: Set Password Hash")]
        [InlineData("test@test.com", "sOmEhAsHbAsE64")]
        public void UserSetPasswordHashAsync(string username, string passwordHash)
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

                    var userStore = new RavenUserOnlyStore<TestUser>(session, optionsAccessor: options.Object);
                    var user = new TestUser()
                    {
                        UserName = username
                    };

                    var resultCreate = userStore.CreateAsync(user).Result;
                    IdentityResultAssert.IsSuccess(resultCreate);

                    userStore.SetPasswordHashAsync(user, passwordHash).Wait();
                    Assert.True(user.PasswordHash == passwordHash);

                    var resultUpdate = userStore.UpdateAsync(user).Result;
                    Assert.True(resultUpdate.Succeeded);

                    var retrievedUser = userStore.FindByIdAsync(user.Id).Result;

                    Assert.NotNull(retrievedUser);
                    Assert.Equal(passwordHash, retrievedUser.PasswordHash);
                }
            }
        }
    }
}
