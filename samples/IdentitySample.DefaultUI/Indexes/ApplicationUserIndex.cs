using IdentitySample.DefaultUI.Entities;
using RavenDB.AspNetCore.IdentityCore.Indexes;

namespace IdentitySample.DefaultUI.Indexes;

/// <summary>
/// Custom user index for <see cref="ApplicationUser"/>.
/// Extends the library's generic index to target the ApplicationUsers collection
/// instead of the default RavenIdentityUsers collection.
/// </summary>
public class ApplicationUserIndex
    : IdentityUserIndex<ApplicationUser>
{
}
