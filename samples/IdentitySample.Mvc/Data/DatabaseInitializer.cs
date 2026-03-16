using IdentitySample.Indexes;
using IdentitySample.Entities;
using Microsoft.AspNetCore.Identity;
using Raven.Client.Documents;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.AspNetCore.IdentityCore.Indexes;

namespace IdentitySample.Data;

/// <summary>
/// Initializes the RavenDB database with indexes and seed data.
/// Similar to EF Core migrations - ensures the database is ready on first run.
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// Creates the database if it doesn't exist, deploys indexes, and seeds test data.
    /// </summary>
    public static async Task InitializeAsync(IDocumentStore store, IServiceProvider services)
    {
        EnsureServerIsReachable(store);

        if (!DatabaseExists(store))
        {
            CreateDatabase(store);
            DeployIndexes(store);
            await SeedTestDataAsync(services);
        }
    }

    /// <summary>
    /// Verifies RavenDB is reachable before attempting any operations.
    /// </summary>
    private static void EnsureServerIsReachable(IDocumentStore store)
    {
        try
        {
            store.Maintenance.Server.Send(new Raven.Client.ServerWide.Operations.GetBuildNumberOperation());
        }
        catch (Exception ex) when (ex is HttpRequestException or AggregateException)
        {
            var urls = string.Join(", ", store.Urls);
            throw new InvalidOperationException(
                $"Cannot connect to RavenDB at [{urls}]. " +
                $"Make sure the Docker container is running: docker-compose up -d",
                ex);
        }
    }

    /// <summary>
    /// Checks whether the database already exists on the server.
    /// </summary>
    private static bool DatabaseExists(IDocumentStore store)
    {
        var dbNames = store.Maintenance.Server.Send(
            new Raven.Client.ServerWide.Operations.GetDatabaseNamesOperation(0, int.MaxValue));

        return dbNames.Contains(store.Database);
    }

    /// <summary>
    /// Creates the database on the server.
    /// </summary>
    private static void CreateDatabase(IDocumentStore store)
    {
        var dbRecord = new Raven.Client.ServerWide.DatabaseRecord(store.Database);
        store.Maintenance.Server.Send(
            new Raven.Client.ServerWide.Operations.CreateDatabaseOperation(dbRecord));
    }

    /// <summary>
    /// Deploys the RavenDB identity indexes to the server.
    /// </summary>
    private static void DeployIndexes(IDocumentStore store)
    {
        // Deploy custom ApplicationUserIndex instead of the default IdentityUserIndex,
        // so it targets the ApplicationUsers collection instead of RavenIdentityUsers
        new ApplicationUserIndex().Execute(store);
        new IdentityRoleIndex().Execute(store);
    }

    /// <summary>
    /// Seeds test users and roles if they don't already exist.
    /// </summary>
    private static async Task SeedTestDataAsync(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<RavenIdentityRole>>();

        // Create roles
        string[] roleNames = ["Admin", "User"];
        foreach (var roleName in roleNames)
        {
            if (await roleManager.FindByNameAsync(roleName) == null)
            {
                await roleManager.CreateAsync(new RavenIdentityRole(roleName));
            }
        }

        // Create admin user
        const string adminEmail = "admin@example.com";
        const string adminPassword = "Admin123!";

        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new ApplicationUser(adminEmail, adminEmail);
            var result = await userManager.CreateAsync(admin, adminPassword);

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
                // Auto-confirm email for test user
                var token = await userManager.GenerateEmailConfirmationTokenAsync(admin);
                await userManager.ConfirmEmailAsync(admin, token);
            }
        }

        // Create regular test user
        const string userEmail = "user@example.com";
        const string userPassword = "User123!";

        if (await userManager.FindByEmailAsync(userEmail) == null)
        {
            var user = new ApplicationUser(userEmail, userEmail);
            var result = await userManager.CreateAsync(user, userPassword);

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, "User");
                var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
                await userManager.ConfirmEmailAsync(user, token);
            }
        }
    }
}
