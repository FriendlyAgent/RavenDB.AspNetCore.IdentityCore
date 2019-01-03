using Identity.MVC.Sample.Models;
using Identity.MVC.Sample.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Raven.Client.Documents;
using RavenDB.AspNetCore.IdentityCore.Extensions;
using System;

namespace Identity.MVC.Sample
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var store = new DocumentStore
            {
                Urls = new[]
                  {
                        "http://localhost:8080"
                    },
                Database = "ravendb-Identity"
            }.Initialize();

            //services.AddScoped<IAsyncDocumentSession, IAsyncDocumentSession>(provider =>
            //{
            //    return store.OpenAsyncSession();
            //});

            services.AddRavenIdentity<ApplicationUser>()
                .AddRavenStores(delegate (
                    IServiceProvider provider)
                {
                    return store.OpenAsyncSession();
                })
                .AddDefaultTokenProviders();

            // Add application services.
            services.AddTransient<IEmailSender, EmailSender>();

            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseAuthentication();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
