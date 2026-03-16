# RavenDB.AspNetCore.IdentityCore

RavenDB stores for ASP.NET Core Identity. A drop-in replacement for `Microsoft.AspNetCore.Identity.EntityFrameworkCore`.

[![NuGet Version](https://img.shields.io/nuget/v/RavenDB.AspNetCore.IdentityCore.svg?style=flat)](https://www.nuget.org/packages/RavenDB.AspNetCore.IdentityCore/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/RavenDB.AspNetCore.IdentityCore.svg?style=flat)](https://www.nuget.org/packages/RavenDB.AspNetCore.IdentityCore/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## Table of Contents

- [Features](#features)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Custom User Model](#custom-user-model)
- [Registration Modes](#registration-modes)
- [Configuration](#configuration)
- [Extensibility](#extensibility)
- [Static Indexes](#static-indexes)
- [Compare-Exchange Uniqueness](#compare-exchange-uniqueness)
- [Management Package](#management-package)
- [Samples](#samples)
- [Related Projects](#related-projects)
- [Development Setup](#development-setup)
- [Contributing](#contributing)
- [License](#license)

## Features

- Full ASP.NET Core Identity compatibility (`UserManager`, `SignInManager`, `RoleManager`)
- Drop-in replacement for `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
- **Fully modular** -- every component (validators, query handlers, stores, indexes) can be replaced with your own implementation
- Four registration modes for different application types (MVC, API, minimal, user-only)
- Atomic uniqueness constraints via RavenDB Compare-Exchange (usernames, emails, role names)
- Static and dynamic index support for query performance tuning
- Audit timestamps on all entities (`CreatedOn`, `UpdatedOn`, `ConfirmationOn`)
- Management package for diagnosing and repairing Compare-Exchange reservations
- Supports **.NET 8**, **.NET 9**, and **.NET 10**
- Compatible with **RavenDB 6.x** and **7.x**

## Installation

```shell
dotnet add package RavenDB.AspNetCore.IdentityCore
```

Optional management and diagnostics package:

```shell
dotnet add package RavenDB.AspNetCore.IdentityCore.Management
```

## Quick Start

The fastest way to get started is using the built-in default types (`RavenIdentityUser` and `RavenIdentityRole`). No custom classes, no indexes -- just register RavenDB and add Identity:

```csharp
// Register RavenDB: singleton store + scoped session per request
builder.Services.AddSingleton<IDocumentStore>(store);
builder.Services.AddScoped<IAsyncDocumentSession>(sp =>
    sp.GetRequiredService<IDocumentStore>().OpenAsyncSession());

// That's it -- uses built-in RavenIdentityUser and RavenIdentityRole with auto-indexes
builder.Services
    .AddRavenIdentity()
    .AddRavenStores()
    .AddDefaultTokenProviders();
```

This gives you a fully working Identity setup with cookie authentication, roles, and auto-generated indexes.

> **Note:** `AddRavenStores()` resolves `IAsyncDocumentSession` from the DI container by default. For multi-tenancy or custom session logic, you can pass a factory: `.AddRavenStores(provider => GetSessionForTenant(provider))`.

> **Tip:** Instead of manually registering `IDocumentStore` and `IAsyncDocumentSession`, you can use [RavenDB.AspNetCore.DependencyInjection](https://github.com/FriendlyAgent/RavenDB.AspNetCore.DependencyInjection) to handle session and store lifecycle for you. See [Related Projects](#related-projects) for more details.

> The repository includes complete working examples for both MVC and API setups in the `samples/` directory.

## Custom User Model

Create a custom user class that extends `RavenIdentityUser`:

```csharp
using RavenDB.AspNetCore.IdentityCore.Entities;

public class ApplicationUser : RavenIdentityUser
{
    public ApplicationUser() { }

    public ApplicationUser(string userName) : base(userName) { }

    public ApplicationUser(string userName, string email) : base(userName, email) { }

    // Add your custom properties
    public string FirstName { get; set; }
    public string LastName { get; set; }
}
```

That's all you need -- the library will generate auto-indexes for your custom type automatically. If you want to use static indexes for better production performance, see the [Static Indexes](#static-indexes) section.

## Registration Modes

| Method | Auth Scheme | Roles | Use Case |
|--------|-------------|-------|----------|
| `AddRavenIdentity<TUser, TRole>()` | Cookie | Yes | Traditional MVC / Razor Pages |
| `AddRavenIdentityCore<TUser>()` | None | No | Custom auth schemes (JWT, etc.) |
| `AddRavenIdentityUserOnly<TUser>()` | Cookie | No | Simple apps without roles |
| `AddRavenIdentityApiEndpoints<TUser>()` | Bearer + Cookie | No | REST APIs, SPAs, mobile apps |

All modes support chaining `.AddRavenStores()` and `.AddDefaultUserQueryHandlerWithCustomIndex<T>()`.

Roles can be added to any mode by chaining `.AddRoles<RavenIdentityRole>()` on the builder.

## Configuration

### Store Options

Configure the user store:

```csharp
builder.Services.ConfigureRavenIdentityUserStore<ApplicationUser, IAsyncDocumentSession>(opts =>
{
    opts.UseStaticIndexes = true;            // Use pre-deployed indexes (default: false)
    opts.AutoSaveChanges = true;             // Auto-persist after Create/Update/Delete (default: true)
    opts.EnforceUniqueConstraints = true;    // Atomic uniqueness via Compare-Exchange (default: true)
    opts.ReservationReleaseRetryCount = 1;   // Retry count for releasing reservations (default: 1)
});
```

Configure the role store (same options):

```csharp
builder.Services.ConfigureRavenIdentityRoleStore<RavenIdentityRole, IAsyncDocumentSession>(opts =>
{
    opts.UseStaticIndexes = true;
});
```

### Validator Options

Set minimum length requirements for usernames and role names:

```csharp
builder.Services.ConfigureRavenUserValidator(opts =>
{
    opts.RequiredLength = 3;
});

builder.Services.ConfigureRavenRoleValidator(opts =>
{
    opts.RequiredLength = 2;
});
```

## Extensibility

The library is fully modular. Every component can be replaced with your own implementation, so you can customize any part of the identity pipeline without forking the library.

### Replaceable Components

| Component | Default | How to Replace |
|-----------|---------|----------------|
| **User Validator** | `RavenUserValidator<TUser>` | `builder.AddUserValidator<MyUserValidator>()` |
| **Role Validator** | `RavenRoleValidator<TRole>` | `builder.AddRoleValidator<MyRoleValidator>()` |
| **User Query Handler** | `DefaultUserQueryHandler<TUser, TSession>` | `.AddUserQueryHandler<MyQueryHandler>()` |
| **Role Query Handler** | `DefaultRoleQueryHandler<TRole, TSession>` | `.AddRoleQueryHandler<MyQueryHandler>()` |
| **Error Describer** | `IdentityErrorDescriber` | `builder.AddErrorDescriber<MyErrorDescriber>()` |
| **User Store** | `RavenUserStore` / `RavenUserOnlyStore` | Implement `IUserStore<TUser>` and register via DI |
| **Role Store** | `RavenRoleStore` | Implement `IRoleStore<TRole>` and register via DI |
| **Static Index** | `IdentityUserIndex<TUser>` | `.AddDefaultUserQueryHandlerWithCustomIndex<MyIndex>()` |

### Custom Query Handler Example

Implement `IUserQueryHandler<TUser, TSession>` to fully control how users are queried:

```csharp
using Raven.Client.Documents.Session;
using RavenDB.AspNetCore.IdentityCore.QueryHandlers;

public class MyUserQueryHandler : IUserQueryHandler<ApplicationUser, IAsyncDocumentSession>
{
    public Task<ApplicationUser> GetByNameAsync(
        IAsyncDocumentSession session, string normalizedUserName, CancellationToken ct)
    {
        // Your custom query logic
    }

    public Task<ApplicationUser> GetByEmailAsync(
        IAsyncDocumentSession session, string normalizedEmail, CancellationToken ct)
    {
        // Your custom query logic
    }

    public Task<ApplicationUser> GetByLoginAsync(
        IAsyncDocumentSession session, string loginProvider, string providerKey, CancellationToken ct)
    {
        // Your custom query logic
    }

    public Task<List<ApplicationUser>> GetUsersInRoleAsync(
        IAsyncDocumentSession session, string roleId, CancellationToken ct)
    {
        // Your custom query logic
    }

    public Task<List<ApplicationUser>> GetUsersForClaimAsync(
        IAsyncDocumentSession session, Claim claim, CancellationToken ct)
    {
        // Your custom query logic
    }
}
```

Register it:

```csharp
builder.Services
    .AddRavenIdentity<ApplicationUser, RavenIdentityRole>()
    .AddRavenStores()
    .AddUserQueryHandler<MyUserQueryHandler>();
```

## Static Indexes

**By default, the library uses RavenDB auto-indexes (dynamic).** This means you don't need to create or deploy any indexes -- RavenDB generates them automatically based on your queries. This is great for development and getting started quickly.

For production workloads, you can opt-in to static indexes for predictable performance and more control.

### Built-in Indexes

The library ships with these indexes:

| Index | RavenDB Name | Indexed Fields |
|-------|-------------|----------------|
| `IdentityUserIndex` / `IdentityUserIndex<TUser>` | `IdentityUserIndex` | `NormalizedUserName`, `Email.NormalizedEmail`, `Logins[].LoginProvider`, `Logins[].ProviderKey`, `Roles[]`, `Claims[].ClaimType`, `Claims[].ClaimValue` |
| `IdentityRoleIndex` / `IdentityRoleIndex<TRole>` | `IdentityRoleIndex` | `NormalizedRoleName` |

### With Default Types

If you're using the default `RavenIdentityUser` and `RavenIdentityRole`, no custom index classes are needed. Just enable static indexes and deploy the built-in ones:

```csharp
// Enable static indexes
builder.Services.ConfigureRavenIdentityUserStore(opts => opts.UseStaticIndexes = true);
builder.Services.ConfigureRavenIdentityRoleStore(opts => opts.UseStaticIndexes = true);

// Deploy the built-in indexes to RavenDB at startup
new IdentityUserIndex().Execute(store);
new IdentityRoleIndex().Execute(store);
```

### With Custom User Types

When you use a custom user type like `ApplicationUser`, RavenDB stores documents in a collection named `ApplicationUsers`. The built-in `IdentityUserIndex` targets the `RavenIdentityUsers` collection, so you need to create a simple inheriting index that targets the correct collection. This is just an empty class -- all the index logic is inherited:

```csharp
using RavenDB.AspNetCore.IdentityCore.Indexes;

public class ApplicationUserIndex : IdentityUserIndex<ApplicationUser> { }
```

Then enable static indexes, register the custom index, and deploy it:

```csharp
builder.Services
    .AddRavenIdentity<ApplicationUser, RavenIdentityRole>()
    .AddRavenStores()
    .AddDefaultUserQueryHandlerWithCustomIndex<ApplicationUserIndex>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureRavenIdentityUserStore<ApplicationUser, IAsyncDocumentSession>(
    opts => opts.UseStaticIndexes = true);

// Deploy at startup
new ApplicationUserIndex().Execute(store);
new IdentityRoleIndex().Execute(store);
```

### Writing a Completely Custom Index (Optional)

You can also write your own index from scratch when you need to index additional custom fields or change the index structure entirely. Your custom index must at minimum index `NormalizedUserName` and `Email.NormalizedEmail` for the identity store to function correctly:

```csharp
using Raven.Client.Documents.Indexes;

public class MyCustomUserIndex : AbstractIndexCreationTask<ApplicationUser>
{
    public override string IndexName => "MyCustomUserIndex";

    public MyCustomUserIndex()
    {
        Map = users => from user in users
                       select new
                       {
                           user.NormalizedUserName,
                           Email_NormalizedEmail = user.Email.NormalizedEmail,
                           Logins = user.Logins.Select(l => new
                           {
                               l.LoginProvider,
                               l.ProviderKey
                           }),
                           user.Roles,
                           Claims = user.Claims.Select(c => new
                           {
                               c.ClaimType,
                               c.ClaimValue
                           }),
                           // Your custom fields
                           user.FirstName,
                           user.LastName
                       };
    }
}
```

Register it with `AddDefaultUserQueryHandlerWithCustomIndex<MyCustomUserIndex>()` if it follows the same field structure as the built-in index, or implement a fully custom query handler (see [Extensibility](#extensibility)).

## Compare-Exchange Uniqueness

Username, email, and role name uniqueness is enforced using RavenDB [Compare-Exchange](https://ravendb.net/docs/article-page/latest/csharp/client-api/operations/compare-exchange/overview) operations. These are cluster-wide atomic operations that prevent race conditions in distributed environments.

**Key format:**
| Type | Key Pattern |
|------|-------------|
| Username | `identity/users/by-name/{normalizedUserName}` |
| Email | `identity/emails/by-value/{normalizedEmail}` |
| Role name | `identity/roles/by-name/{normalizedRoleName}` |

Uniqueness is enforced at the store level (not in validators), so duplicate checks are atomic even under concurrent requests. This can be disabled per store with `EnforceUniqueConstraints = false`, but this removes race-condition protection.

## Management Package

The `RavenDB.AspNetCore.IdentityCore.Management` package provides tools for diagnosing and repairing Compare-Exchange reservations.

```shell
dotnet add package RavenDB.AspNetCore.IdentityCore.Management
```

### Usage

```csharp
using RavenDB.AspNetCore.IdentityCore.Management;

var userManager = new IdentityUserReservationManager<ApplicationUser>(store);

// Get a health report
var report = await userManager.GetReportAsync();
// report.IsHealthy    -- true if no issues
// report.Healthy      -- reservations linked to existing documents
// report.Orphaned     -- reservations pointing to deleted documents
// report.Pending      -- incomplete reservations (interrupted creation)
// report.Missing      -- documents without reservations

// Repair all issues
if (!report.IsHealthy)
{
    await userManager.RepairAllAsync();
}
```

### Migration

When enabling or disabling `EnforceUniqueConstraints` on existing data:

```csharp
// Create reservations for all existing documents
await userManager.EnableConstraintsAsync();

// Remove all identity reservations
await userManager.DisableConstraintsAsync();
```

The same API is available for roles via `IdentityRoleReservationManager<TRole>`.

## Samples

The repository includes complete working examples in the `samples/` directory:

- **MVC** -- Traditional MVC app with cookie authentication, roles, custom Identity pages, and database seeding
- **Default UI** -- MVC app using `AddDefaultUI()` for scaffolded Identity pages with custom profile fields (Name, Age)
- **API Endpoints** -- Minimal API with bearer token authentication using `MapIdentityApi`

All samples demonstrate database initialization, index deployment, and custom user models. They are a good starting point for understanding how the library fits together.

## Related Projects

- **[RavenDB.AspNetCore.DependencyInjection](https://github.com/FriendlyAgent/RavenDB.AspNetCore.DependencyInjection)** -- Manages `IDocumentStore` lifecycle and provides scoped `IAsyncDocumentSession` via DI, so you don't have to wire up RavenDB services manually.
- **[RavenDB.AspNetCore.Identity.Example](https://github.com/FriendlyAgent/RavenDB.AspNetCore.Identity.Example)** -- A complete ASP.NET Core MVC application that combines both the DependencyInjection and IdentityCore packages, showing how they work together end-to-end.

## Development Setup

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download) or later
- [Docker](https://www.docker.com/get-started)

### Getting Started

1. Start RavenDB:

```shell
docker compose up -d
```

> Or without docker-compose: `docker run -p 8080:8080 -e RAVEN_Security_UnsecuredAccessAllowed=PublicNetwork ravendb/ravendb`

2. RavenDB Studio is available at [http://localhost:8080](http://localhost:8080)

3. Run a sample:

```shell
dotnet run --project samples/IdentitySample.Mvc
```

4. Run tests:

```shell
dotnet test
```

## Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
