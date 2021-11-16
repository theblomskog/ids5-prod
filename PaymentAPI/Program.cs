using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PaymentAPI
{
    /// <summary>
    /// This is the starter-kit that we use in the training course IdentityServer in Production  
    /// by https://www.tn-data.se
    /// </summary>
    public class Program
    {
        private static readonly string _applicationName = "PaymentAPI";

        public static void Main(string[] args)
        {
            Settings.StartupTime = DateTime.Now;
            Console.Title = _applicationName;

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            Console.WriteLine($"ASPNETCORE_ENVIRONMENT='{env}'");

            switch (env)
            {
                case "Production":
                    Console.WriteLine("Configuring Kestrel for production");

                    //Use Azure KeyVault for config + Https certificate in key vault
                    return Host.CreateDefaultBuilder(args)
                        .ConfigureAppConfiguration(config =>
                        {
                            config.AddAzureKeyVaultSupport();
                        })
                        .ConfigureWebHostDefaults(webBuilder =>
                        {
                            webBuilder.AddHttpsSupport(certName: "secure-nu-certificate");

                            webBuilder.UseStartup<Startup>()
                                .UseUrls("http://*:80;https://*:443");
                        });
                case "Offline":
                    Console.WriteLine("Configuring Kestrel for offline");

                    //Use local configuration and the built in localhost developer certificate
                    return Host.CreateDefaultBuilder(args)
                        .ConfigureWebHostDefaults(webBuilder =>
                        {
                            webBuilder.UseStartup<Startup>();
                        });

                default:
                    //Development
                    Console.WriteLine("Configuring Kestrel for development");

                    //Use Azure Key Vault for config and the built in localhost developer certificate
                    return Host.CreateDefaultBuilder(args)
                        .ConfigureAppConfiguration(config =>
                        {
                            config.AddAzureKeyVaultSupport();
                        })
                        .ConfigureWebHostDefaults(webBuilder =>
                        {
                            webBuilder.UseStartup<Startup>();
                        });
            }
        }
    }
}
