# RavenDB.AspNetCore.IdentityCore
Identity Core package for using RavenDB with ASP.NET Core Identity.

[![Nuget Version](https://img.shields.io/nuget/v/RavenDB.AspNetCore.IdentityCore.svg?style=flat)](https://www.nuget.org/packages/RavenDB.AspNetCore.IdentityCore/)
[![Nuget Version Pre](https://img.shields.io/nuget/vpre/RavenDB.AspNetCore.IdentityCore.svg?style=flat)](https://www.nuget.org/packages/RavenDB.AspNetCore.IdentityCore/)

This package is used as a replacement for the EntityFrameworkCore package and makes it possible to use RavenDB as your database for storing users and roles, while being fully compatible with the Identity framework.

## Getting Started:
Install the [RavenDB.AspNetCore.IdentityCore](https://www.nuget.org/packages/RavenDB.AspNetCore.IdentityCore/) library through [NuGet](https://nuget.org).
```
    Install-Package RavenDB.AspNetCore.IdentityCore
    
    Or
    
    Install-Package RavenDB.AspNetCore.IdentityCore -Pre
```    

## Usage:   
Add this to your Startup.cs:
>**Note:** You need to have IAsyncDocumentSession injected as a service [Check Out](https://github.com/FriendlyAgent/RavenDB.AspNetCore.DependencyInjection).
```csharp
public IServiceProvider ConfigureServices(IServiceCollection services)
{
...

	var store = new DocumentStore
	{
      Url = "{server url}",
      DefaultDatabase = "Main"
	}.Initialize();

	// Ravendb framework setup.
	services.AddScoped<IAsyncDocumentSession, IAsyncDocumentSession>(provider =>
	{
      return store.OpenAsyncSession();
	});

    services.AddRavenIdentity<IdentityUser, IdentityRole>()
      .AddRavenStores();
    
...
}
```

Optional if you want to implement your own user/role model:
>**Note:** Don't forget to change IdentityUser to ApplicationUser in the Startup.cs.
```csharp
public class ApplicationUser
        : IdentityUser
    {
        public ApplicationUser(
            string userName, 
            string email) 
            : base(userName, email)
        {
        }

        /// <summary>
        /// User's first name
        /// </summary>
        public string FirstName { get; set; }

        /// <summary>
        /// User's last name
        /// </summary>
        public string LastName { get; set; }
    }
}
```

# User Feedback

## Issues

If you have any problems with or questions about this image, please contact us through a [GitHub issue](https://github.com/FriendlyAgent/RavenDB.AspNetCore.IdentityCore/issues).
