using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProxyServer;
using ProxyServer.DependencyInjection;
using Serilog;

namespace ConsoleHost
{
    internal static class Program
    {
        static void Main()
        {
            var host = AppStartup();

            var proxyServer = host.Services.GetService<INetProxyServer>();

            if (proxyServer is null)
                throw new NullReferenceException(nameof(proxyServer));

            var serverTask = Task.Run(async () => await proxyServer.StartAsync(10800));

            while (true)
            {
                var input = Console.ReadLine()?.Trim();

                if (input is not null && input == "stop")
                {
                    proxyServer.Stop();
                    break;
                }
            }
        }

        private static IHost AppStartup()
        {
            var builder = new ConfigurationBuilder();
            
            ConfigureSetup(builder);

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Build())
                .CreateLogger();

            var host = Host.CreateDefaultBuilder()
                .UseProxyServer()
                .UseSerilog()
                .Build();

            return host;
        }
        
        private static void ConfigureSetup(IConfigurationBuilder builder)
        {
            builder.SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables();
        }
    }
}