using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ProxyServer.Sockets
{
    /// <summary>
    /// Subclassed from TcpListener, main reason is to have a AcceptPipeTcpClientAsync
    /// Which returns PipeTcpClient instead of TcpClient.
    /// </summary>
    internal sealed class PipeTcpListener : TcpListener
    {
        public PipeTcpListener(IPAddress localAddress, int port) : base(localAddress, port)
        {
        }
        
        /// <summary>
        /// Accepts a pending connection request as an asynchronous operation.
        /// </summary>
        /// <returns>
        /// The task object representing the asynchronous operation.
        /// The Result property on the task object returns a PipeTcpClient used to send and receive data.
        /// </returns>
        public Task<PipeTcpClient> AcceptPipeTcpClientAsync(ISocketOptions socketOptions)
        {
            return WaitAndWrap(AcceptSocketAsync(), socketOptions);

            static async Task<PipeTcpClient> WaitAndWrap(Task<Socket> task, ISocketOptions options) =>
                new(await task.ConfigureAwait(false), options);
        }
    }
}