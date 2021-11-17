using System.Threading;
using System.Threading.Tasks;
using ProxyServer.Server.Client;

namespace ProxyServer.Server.Proxy
{
    internal interface IProxyRequestHandler
    {
        Task ProcessProxyRequestAsync(IClientHandler clientHandler, CancellationToken cancellationToken = default);
    }
}