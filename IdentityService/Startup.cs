using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityServerHost.Models;
using IdentityServerHost.Quickstart.UI;
using IdentityServerInMem;
using IdentityService.Configuration;
using IdentityService.Configuration.Clients;
using IdentityService.Data;
using Infrastructure;
using Infrastructure.DataProtection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace IdentityService
{
    public class Startup
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _environment = environment;
            _configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlServer(_configuration["ConnectionString"]);
            });

            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();


            if (_environment.EnvironmentName != "Offline")
                services.AddDataProtectionWithSqlServerForIdentityService(_configuration);

            services.AddControllersWithViews();

            var builder = services.AddIdentityServer(options =>
            {
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseInformationEvents = true;
                options.Events.RaiseSuccessEvents = true;
            })
            .AddInMemoryIdentityResources(Config.IdentityResources)
            .AddInMemoryApiScopes(Config.ApiScopes)
            .AddInMemoryClients(ClientData.GetClients())
            .AddOperationalStore(options =>
            {
                options.EnableTokenCleanup = true;
                //The number of records to remove at a time. Defaults to 100.
                //options.TokenCleanupBatchSize = 100;
                //options.TokenCleanupInterval = 30; //Seconds

                options.ConfigureDbContext = options =>
                {
                    options.UseSqlServer(_configuration["ConnectionString"]);
                };
            })
            .AddAspNetIdentity<ApplicationUser>();

            if (_environment.EnvironmentName != "Offline")
                builder.AddProductionSigningCredential(_configuration);
            else
                builder.AddDeveloperSigningCredential();

            services.AddHsts(opts =>
            {
                opts.IncludeSubDomains = true;
                opts.MaxAge = TimeSpan.FromSeconds(15768000);
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (!env.IsProduction())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
                app.UseExceptionHandler("/Home/Error");
                // app.UseStatusCodePages()
            }

            app.UseSerilogRequestLogging();

            app.UseHttpsRedirection();
            

            app.UseRequestLocalization(new RequestLocalizationOptions().SetDefaultCulture("se-SE"));

            app.UseStaticFiles();

            app.UseRouting();

            app.UseIdentityServer();
            app.UseSecurityHeaders();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapDefaultControllerRoute();
            });
        }
    }
}
