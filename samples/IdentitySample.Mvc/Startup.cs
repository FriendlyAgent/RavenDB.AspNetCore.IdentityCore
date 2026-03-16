using IdentitySample.Data;
using IdentitySample.Indexes;
using IdentitySample.Entities;
using IdentitySample.Services;
using Microsoft.AspNetCore.Identity;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.AspNetCore.IdentityCore.Extensions;

namespace IdentitySample;

public class Startup
{
    public Startup(IWebHostEnvironment env)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(env.ContentRootPath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

        builder.AddEnvironmentVariables();
        Configuration = builder.Build();
    }

    public IConfigurationRoot Configuration { get; set; }

    public void ConfigureServices(IServiceCollection services)
    {
        // Configure RavenDB - uses the same Docker container as tests (docker-compose up -d)
        var ravenSection = Configuration.GetSection("RavenDB");
        var store = new DocumentStore
        {
            Urls = ravenSection.GetSection("Urls").Get<string[]>(),
            Database = ravenSection["DatabaseName"]
        };
        store.Initialize();

        // Register the document store as a singleton and open a new async session per request
        services.AddSingleton<IDocumentStore>(store);
        services.AddScoped<IAsyncDocumentSession>(sp =>
            sp.GetRequiredService<IDocumentStore>().OpenAsyncSession());

        services.AddMvc();

        // Add RavenDB Identity with full role support and custom index
        services.AddRavenIdentity<ApplicationUser, RavenIdentityRole>()
            .AddRavenStores()
            .AddDefaultUserQueryHandlerWithCustomIndex<ApplicationUserIndex>()
            .AddDefaultTokenProviders();

        // Enable static indexes for both user and role stores
        services.ConfigureRavenIdentityUserStore<ApplicationUser, IAsyncDocumentSession>(
            opts => opts.UseStaticIndexes = true);

        services.ConfigureRavenIdentityRoleStore<RavenIdentityRole, IAsyncDocumentSession>(
            opts => opts.UseStaticIndexes = true);

        // Add application services.
        services.AddTransient<IEmailSender, AuthMessageSender>();
        services.AddTransient<ISmsSender, AuthMessageSender>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();

            // Initialize database: create DB, deploy indexes, seed test users
            var store = app.ApplicationServices.GetRequiredService<IDocumentStore>();
            using (var scope = app.ApplicationServices.CreateScope())
            {
                DatabaseInitializer
                    .InitializeAsync(store, scope.ServiceProvider)
                    .GetAwaiter()
                    .GetResult();
            }
        }
        else
        {
            app.UseExceptionHandler("/Home/Error");
        }

        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapDefaultControllerRoute();
            endpoints.MapRazorPages();
        });
    }
}
