using IdentitySample.DefaultUI.Entities;
using IdentitySample.DefaultUI.Indexes;
using Microsoft.AspNetCore.Identity;
using Raven.Client.Documents;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.AspNetCore.IdentityCore.Indexes;

namespace IdentitySample.DefaultUI.Data;

/// <summary>
/// Initializes the RavenDB database with indexes and seed data.
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

    private static bool DatabaseExists(IDocumentStore store)
    {
        var dbNames = store.Maintenance.Server.Send(
            new Raven.Client.ServerWide.Operations.GetDatabaseNamesOperation(0, int.MaxValue));

        return dbNames.Contains(store.Database);
    }

    private static void CreateDatabase(IDocumentStore store)
    {
        var dbRecord = new Raven.Client.ServerWide.DatabaseRecord(store.Database);
        store.Maintenance.Server.Send(
            new Raven.Client.ServerWide.Operations.CreateDatabaseOperation(dbRecord));
    }

    private static void DeployIndexes(IDocumentStore store)
    {
        new ApplicationUserIndex().Execute(store);
        new IdentityRoleIndex().Execute(store);
    }

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
            var admin = new ApplicationUser(adminEmail, adminEmail)
            {
                Name = "Admin User",
                Age = 30
            };
            var result = await userManager.CreateAsync(admin, adminPassword);

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
                var token = await userManager.GenerateEmailConfirmationTokenAsync(admin);
                await userManager.ConfirmEmailAsync(admin, token);
            }
        }

        // Create regular test user
        const string userEmail = "user@example.com";
        const string userPassword = "User123!";

        if (await userManager.FindByEmailAsync(userEmail) == null)
        {
            var user = new ApplicationUser(userEmail, userEmail)
            {
                Name = "Test User",
                Age = 25
            };
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
