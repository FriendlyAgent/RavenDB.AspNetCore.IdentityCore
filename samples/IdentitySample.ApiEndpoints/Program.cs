using System.Security.Claims;
using IdentitySample.ApiEndpoints.Data;
using IdentitySample.ApiEndpoints.Indexes;
using IdentitySample.ApiEndpoints.Entities;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using RavenDB.AspNetCore.IdentityCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

// RavenDB
var ravenSection = builder.Configuration.GetSection("RavenDB");
var store = new DocumentStore
{
    Urls = ravenSection.GetSection("Urls").Get<string[]>(),
    Database = ravenSection["DatabaseName"]
};
store.Initialize();

builder.Services.AddSingleton<IDocumentStore>(store);
builder.Services.AddScoped<IAsyncDocumentSession>(sp =>
    sp.GetRequiredService<IDocumentStore>().OpenAsyncSession());

builder.Services.AddAuthorization();

builder.Services
    .AddRavenIdentityApiEndpoints<ApplicationUser>()
    .AddRavenStores()
    .AddDefaultUserQueryHandlerWithCustomIndex<ApplicationUserIndex>();

builder.Services.ConfigureRavenIdentityUserStore<ApplicationUser, IAsyncDocumentSession>(
    opts => opts.UseStaticIndexes = true);

var app = builder.Build();

// Initialize database in development
if (app.Environment.IsDevelopment())
{
    DatabaseInitializer.Initialize(store);
}

app.MapGet("/", () => "Hello, World!");
app.MapGet("/requires-auth", (ClaimsPrincipal user) => $"Hello, {user.Identity?.Name}!").RequireAuthorization();

app.MapGroup("/identity").MapIdentityApi<ApplicationUser>();

app.Run();
