using Microsoft.AspNetCore.Hosting;
using RavenDB.Identity.MVC.Sample.Areas.Identity;

[assembly: HostingStartup(typeof(IdentityHostingStartup))]
namespace RavenDB.Identity.MVC.Sample.Areas.Identity
{
    public class IdentityHostingStartup : IHostingStartup
    {
        public void Configure(IWebHostBuilder builder)
        {
            builder.ConfigureServices((context, services) =>
            {
            });
        }
    }
}