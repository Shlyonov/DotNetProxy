using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace ProxyServer.Sockets.Tunnel
{
    internal interface ITcpTunnel
    {
        //Task StartTunnel(TcpClient local, TcpClient remote, CancellationToken cancellationToken = default);

        Task StartTunnelAsync(IDuplexPipe local, IDuplexPipe remote, CancellationToken cancellationToken = default);
    }
}