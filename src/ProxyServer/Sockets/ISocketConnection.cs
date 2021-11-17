using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace ProxyServer.Sockets
{
    public interface ISocketConnection
    {
        EndPoint RemoteEndPoint { get; }
        bool Connected { get; }
        IDuplexPipe GetTransportPipe();
        ValueTask ConnectAsync(EndPoint ep, CancellationToken cancellationToken);
        void Disconnect(bool isReuseSocket);
    }
}