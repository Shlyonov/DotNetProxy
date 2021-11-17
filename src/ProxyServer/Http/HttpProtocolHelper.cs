using System;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ProxyServer.Exceptions;
using ProxyServer.Http.Request;
using ProxyServer.Pipelines;
using ProxyServer.Sockets.Tunnel;

namespace ProxyServer.Http
{
    internal class HttpProtocolHelper : IHttpProtocolHelper
    {
        private readonly ITcpTunnel _tcpTunnel;
        private const string SupportedProtocol = "HTTP/1.1";

        private static readonly Memory<byte> ConnectionOkResponse;

        private static readonly Memory<byte>[] BadRequestResponse;

        private static readonly Memory<byte>[] BadGatewayResponse;

        private static readonly Memory<byte> EmptyString = Array.Empty<byte>();

        static HttpProtocolHelper()
        {
            ConnectionOkResponse = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established");
            
            BadRequestResponse = new Memory<byte>[] {
                Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request")
                , Encoding.ASCII.GetBytes("Connection: close")
            };
            
            BadGatewayResponse = new Memory<byte>[] {
                Encoding.ASCII.GetBytes("HTTP/1.1 502 Bad Gateway")
                ,Encoding.ASCII.GetBytes("Connection: close")
            };
        }

        public HttpProtocolHelper(ITcpTunnel tcpTunnel)
        {
            _tcpTunnel = tcpTunnel ?? throw new ArgumentNullException(nameof(tcpTunnel));
        }

        public async Task<HttpRequestHeaders> PeekRequestHeaderAsync(PipeReader reader,
            CancellationToken cancellationToken = default)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            string requestHeaderStr;

            using (var requestHeader = await reader.PeekLineAsync(cancellationToken).ConfigureAwait(false))
            {
                if (requestHeader.MemoryOwner == default)
                {
                    throw new BadRequestException("No data!");
                }

                requestHeaderStr =
                    Encoding.ASCII.GetString(requestHeader.MemoryOwner.Memory[..requestHeader.Length].Span);
            }

            var requestHeaders =  HttpRequestParser.ParseRequestHeader(requestHeaderStr);

            if (requestHeaders.Protocol != SupportedProtocol)
            {
                throw new BadRequestException($"Unknown protocol: {requestHeaders.Protocol}");
            }

            return requestHeaders;
        }

        public async Task SkipToEndAsync(PipeReader reader, CancellationToken cancellationToken = default)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            
            int read;

            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var readLine = await reader.ReadLineAsync(cancellationToken);

                read = readLine.Length;
            } while (read > 0);
        }

        public async ValueTask WriteConnectionOkAsync(PipeWriter writer, CancellationToken cancellationToken = default)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            
            await writer.WriteLineAsync(ConnectionOkResponse, cancellationToken);
            await writer.WriteLineAsync(EmptyString, cancellationToken);
        }

        public async ValueTask WriteBadRequestAsync(PipeWriter writer, CancellationToken cancellationToken = default)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            
            await writer.WriteLineAsync(BadRequestResponse[0], cancellationToken);
            await writer.WriteLineAsync(BadRequestResponse[1], cancellationToken);
            await writer.WriteLineAsync(EmptyString, cancellationToken);
        }

        public async ValueTask WriteBadGatewayAsync(PipeWriter writer, CancellationToken cancellationToken = default)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            
            await writer.WriteLineAsync(BadGatewayResponse[0], cancellationToken);
            await writer.WriteLineAsync(BadGatewayResponse[1], cancellationToken);
            await writer.WriteLineAsync(EmptyString, cancellationToken);
        }

        public Task StartTcpTunnelAsync(IDuplexPipe local, IDuplexPipe remote, CancellationToken cancellationToken = default)
        {
            if (local == null) throw new ArgumentNullException(nameof(local));
            if (remote == null) throw new ArgumentNullException(nameof(remote));

            return _tcpTunnel.StartTunnelAsync(local, remote, cancellationToken);
        }
    }
}