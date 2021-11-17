using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ProxyServer
{
    /// <summary>
    /// 
    /// </summary>
    public interface INetProxyServer
    {
        /// <summary>
        /// Is server started
        /// </summary>
        bool Active { get; }

        /// <summary>
        /// Start server
        /// </summary>
        /// <param name="port">Server port</param>
        /// <returns>Server process task</returns>
        /// <exception cref="SocketException">If port is not available</exception>
        /// <exception cref="InvalidOperationException">Server has already started</exception>
        Task StartAsync(int port);

        /// <summary>
        /// Stop server
        /// </summary>
        /// <exception cref="InvalidOperationException">If server didn't start</exception>
        void Stop();
    }
}