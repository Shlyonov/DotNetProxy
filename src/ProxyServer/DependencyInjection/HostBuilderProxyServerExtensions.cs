using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using ProxyServer.Http;
using ProxyServer.Server;
using ProxyServer.Server.Client;
using ProxyServer.Server.Proxy;
using ProxyServer.Sockets.Tunnel;
//using ProxySrv.Server.Http.Stream;

namespace ProxyServer.DependencyInjection
{
    // Di container services configuring
    public static class HostBuilderProxyServerExtensions
    {
        /// <summary>Sets Serilog as the logging provider.</summary>
        /// <param name="hostBuilder">The host builder to configure.</param>
        /// <returns>The host builder.</returns>
        /// <exception cref="ArgumentNullException">If input IHostBuilder is null</exception>
        public static IHostBuilder UseProxyServer(this IHostBuilder hostBuilder)
        {
            if (hostBuilder == null) throw new ArgumentNullException(nameof(hostBuilder));

            return ConfigureServices(hostBuilder, _ =>
                {
                }
            );
        }

        public static IHostBuilder UseProxyServer(this IHostBuilder hostBuilder, Action<ProxyServerOptions> options)
        {
            if (hostBuilder == null) throw new ArgumentNullException(nameof(hostBuilder));
            if (options == null) throw new ArgumentNullException(nameof(options));

            return ConfigureServices(hostBuilder, options);
        }

        private static IHostBuilder ConfigureServices(IHostBuilder hostBuilder, Action<ProxyServerOptions> options)
        {
            if (hostBuilder == null) throw new ArgumentNullException(nameof(hostBuilder));

            return hostBuilder.ConfigureServices((_, services) =>
            {
                services.AddSingleton<IValidateOptions<ProxyServerOptions>, ProxyServerOptionsValidator>();
                services.AddSingleton(container => container.GetService<IOptions<ProxyServerOptions>>()?.Value);

                services.AddTransient<INetProxyServer, NetProxyServer>();
                services.AddTransient<NetProxyServerImpl>();

                services.AddTransient<IClientHandler, ClientHandler>(implementationFactory =>
                    new(implementationFactory.GetRequiredService<ProxyServerOptions>()));

                services.AddSingleton<ClientHandlerPooledObjectPolicy>();
                services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
                services.AddSingleton(serviceProvider =>
                {
                    var provider = serviceProvider.GetRequiredService<ObjectPoolProvider>();
                    var policy = serviceProvider.GetRequiredService<ClientHandlerPooledObjectPolicy>();
                    return provider.Create(policy);
                });
                services.AddSingleton<IProxyRequestHandler, ProxyRequestHandler>();
                services.AddSingleton<IHttpProtocolHelper, HttpProtocolHelper>();
                services.AddSingleton<ITcpTunnel, TcpTunnel>(implementationFactory =>
                    new(implementationFactory.GetRequiredService<ProxyServerOptions>()));

                services.Configure(options);
            });
        }
    }
}