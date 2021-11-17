using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProxyServer.DependencyInjection;

namespace ProxyServer.Tests.Fixtures
{
    public class NetProxyServerFixture
    {
        public INetProxyServer GetProxyServer(Action<ProxyServerOptions> options = null)
        {
            var hostBuilder = Host.CreateDefaultBuilder();

            if (options != null)
                hostBuilder.UseProxyServer(options);
            else
                hostBuilder.UseProxyServer();

            var host = hostBuilder.Build();

            return host.Services.GetService<INetProxyServer>();
        }
    }
}