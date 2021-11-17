using System;
using System.Threading.Tasks;
using ProxyServer.Server;

namespace ProxyServer
{
    /// <summary>
    /// .Net core proxy server
    /// </summary>
    internal sealed class NetProxyServer : INetProxyServer
    {
        private readonly NetProxyServerImpl _netProxyServerImpl;

        public NetProxyServer(NetProxyServerImpl netProxyServerImpl)
        {
            _netProxyServerImpl = netProxyServerImpl ?? throw new ArgumentNullException(nameof(netProxyServerImpl));
        }

        public bool Active => _netProxyServerImpl.Active;
        
        /// <inheritdoc />
        public Task StartAsync(int port)
        {
            return _netProxyServerImpl.StartAsync(port);
        }

        /// <inheritdoc />
        public void Stop()
        {
            _netProxyServerImpl.Stop();
        }
    }
}