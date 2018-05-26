using Microsoft.AspNetCore.Identity;
using Xunit;

namespace RavenDB.Test.Shared
{
    public static class IdentityResultAssert
    {
        /// <summary>
        /// Asserts that the result has Succeeded.
        /// </summary>
        /// <param name="result"></param>
        public static void IsSuccess(IdentityResult result)
        {
            Assert.NotNull(result);
            Assert.True(result.Succeeded);
        }

        /// <summary>
        /// Asserts that the result has not Succeeded.
        /// </summary>
        public static void IsFailure(IdentityResult result)
        {
            Assert.NotNull(result);
            Assert.False(result.Succeeded);
        }
    }
}
