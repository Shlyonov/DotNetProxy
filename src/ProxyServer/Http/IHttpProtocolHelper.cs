using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using ProxyServer.Http.Request;

namespace ProxyServer.Http
{
    internal interface IHttpProtocolHelper
    {
        Task<HttpRequestHeaders> PeekRequestHeaderAsync(PipeReader reader,
            CancellationToken cancellationToken = default);

        Task SkipToEndAsync(PipeReader reader, CancellationToken cancellationToken = default);
        ValueTask WriteConnectionOkAsync(PipeWriter writer, CancellationToken cancellationToken = default);
        ValueTask WriteBadRequestAsync(PipeWriter writer, CancellationToken cancellationToken = default);
        ValueTask WriteBadGatewayAsync(PipeWriter writer, CancellationToken cancellationToken = default);
        Task StartTcpTunnelAsync(IDuplexPipe local, IDuplexPipe remote, CancellationToken cancellationToken = default);
    }
}