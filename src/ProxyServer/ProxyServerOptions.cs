using ProxyServer.Constants;
using ProxyServer.Sockets;
using ProxyServer.Sockets.Tunnel;

namespace ProxyServer
{
    public class ProxyServerOptions: ISocketOptions, ITunnelOptions
    {
        /// <summary>
        /// Settings for ServicePointManager.DefaultConnectionLimit
        /// </summary>
        public int ConnectionLimit { get; set; } = ProxyServerOptionsConstants.DefaultConnectionLimit;
        /// <summary>
        /// Server listener backlog
        /// </summary>
        public int Backlog { get; set; }= ProxyServerOptionsConstants.DefaultBacklog;
        /// <summary>
        /// Tunnel keep-alive timeout (ms)
        /// </summary>
        public int KeepAliveTimeout { get; set; }= ProxyServerOptionsConstants.DefaultKeepAliveTimeout;
        /// <summary>
        /// Socket ConnectTimeout (ms)
        /// </summary>
        public int ConnectTimeout { get; set; }= ProxyServerOptionsConstants.DefaultConnectTimeout;
        /// <summary>
        /// Socket SendTimeout (ms)
        /// </summary>
        public int SendTimeout { get; set; }= ProxyServerOptionsConstants.DefaultSendTimeout;
        /// <summary>
        /// Socket ReceiveTimeout (ms)
        /// </summary>
        public int ReceiveTimeout { get; set; }= ProxyServerOptionsConstants.DefaultReceiveTimeout;
    }
}