using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityModel;
using Infrastructure;
using Infrastructure.DataProtection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using Polly;
using Serilog;
using SessionStore;

namespace Client
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
            services.AddTransient<SerilogHttpMessageHandler>();

            // adds user and client access token management
            services.AddAccessTokenManagement(options =>
            {
                options.User.RefreshBeforeExpiration = TimeSpan.FromSeconds(5);
            })
                .ConfigureBackchannelHttpClient()
                    .AddTransientHttpErrorPolicy(policy => policy.WaitAndRetryAsync(new[]
                    {
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(3)
                    }));


            //services.AddHttpClient("paymentapi", client =>
            //{
            //    client.BaseAddress = new Uri(_configuration["paymentApiUrl"]);
            //    client.Timeout = TimeSpan.FromSeconds(5);
            //    client.DefaultRequestHeaders.Add("Accept", "application/json");
            //}).AddHttpMessageHandler<SerilogHttpMessageHandler>();

            // registers HTTP client that uses the managed user access token
            services.AddUserAccessTokenHttpClient("paymentapi", configureClient: client =>
            {
                client.BaseAddress = new Uri(_configuration["paymentApiUrl"]);
                client.Timeout = TimeSpan.FromSeconds(5);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            })
                .AddHttpMessageHandler<SerilogHttpMessageHandler>();


            if (_environment.EnvironmentName != "Offline")
                services.AddDataProtectionWithSqlServerForClient(_configuration);

            services.AddControllersWithViews();

            services.AddHsts(opts =>
            {
                opts.IncludeSubDomains = true;
                opts.MaxAge = TimeSpan.FromSeconds(15768000);
            });

            services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie("Cookies", options =>
            {
                options.LogoutPath = "/User/Logout";
                options.AccessDeniedPath = "/User/AccessDenied";
                options.SessionStore = new MySessionStore();

                options.Events.OnSigningOut = async e =>
                {
                    // revoke refresh token on sign-out
                    await e.HttpContext.RevokeUserRefreshTokenAsync();
                };
            })
            .AddOpenIdConnect("OpenIdConnect", options =>
            {
                options.Authority = _configuration["openid:authority"];
                options.ClientId = _configuration["openid:clientid"];
                options.ClientSecret = "mysecret";
                options.ResponseType = "code";

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.Scope.Add("offline_access");
                options.Scope.Add("employee");
                options.Scope.Add("payment");

                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;

                options.Prompt = "consent";

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = JwtClaimTypes.Name,
                    RoleClaimType = JwtClaimTypes.Role
                };

                options.AccessDeniedPath = "/User/AccessDenied";

                options.BackchannelHttpHandler = new BackChannelListener();
                options.BackchannelTimeout = TimeSpan.FromSeconds(5);
            });

            //Add the listener to the ETW system
            //IdentityModelEventSource.Logger.LogLevel = System.Diagnostics.Tracing.EventLevel.Verbose;

            //var listener = new IdentityModelEventListener();
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

            app.UseHttpsRedirection();
            app.UseSecurityHeaders();

            app.UseStaticFiles();

            app.UseRouting();

            app.UseRequestLocalization(
                new RequestLocalizationOptions()
                    .SetDefaultCulture("se-SE"));

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
