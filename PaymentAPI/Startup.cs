using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure;
using Infrastructure.DataProtection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Logging;
using PaymentAPI.Middleware;
using Serilog;

namespace PaymentAPI
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
        public void ConfigureServices(IServiceCollection services)
        {
            if (_environment.EnvironmentName != "Offline")
                services.AddDataProtectionWithSqlServerForPaymentApi(_configuration);

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opt =>
            {
                opt.Authority = _configuration["openid:authority"];
                opt.Audience = "paymentapi";

                opt.MapInboundClaims = false;
                opt.TokenValidationParameters.RoleClaimType = "roles";
                opt.TokenValidationParameters.NameClaimType = "name";
                opt.IncludeErrorDetails = true;


                opt.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(0);

                // IdentityServer emits a type header by default, recommended extra check
                opt.TokenValidationParameters.ValidTypes = new[] { "at+jwt" };


                opt.BackchannelHttpHandler = new BackChannelListener();
                opt.BackchannelTimeout = TimeSpan.FromSeconds(5);
            });

            services.AddHsts(opts =>
            {
                opts.IncludeSubDomains = true;
                opts.MaxAge = TimeSpan.FromSeconds(15768000);
            });

            services.AddControllersWithViews();

            //Add the listener to the ETW system
            //var listener = new IdentityModelEventListener();
            //IdentityModelEventSource.Logger.LogLevel = System.Diagnostics.Tracing.EventLevel.Warning;
            //IdentityModelEventSource.ShowPII = true;
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
            }

            app.UseSerilogRequestLogging();

            app.UseWaitForIdentityServer(new WaitForIdentityServerOptions
            { Authority = _configuration["openid:authority"] });

            app.UseHttpsRedirection();
            app.UseSecurityHeaders();

            app.UseRouting();

            app.UseRequestLocalization(
            new RequestLocalizationOptions()
                .SetDefaultCulture("se-SE"));

            //Wait for IdentityServer to startup


            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");

            });
        }
    }
}
