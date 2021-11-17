using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProxyServer.Exceptions;
using ProxyServer.Http;
using ProxyServer.Http.Request;
using ProxyServer.Server.Client;

namespace ProxyServer.Server.Proxy
{
    internal class ProxyRequestHandler : IProxyRequestHandler
    {
        private readonly IHttpProtocolHelper _httpProtocolHelper;
        private const string ConnectMethod = "CONNECT";
        private readonly ILogger _logger;

        public ProxyRequestHandler(IHttpProtocolHelper httpProtocolHelper, ILogger<ProxyRequestHandler> logger)
        {
            _httpProtocolHelper = httpProtocolHelper ?? throw new ArgumentNullException(nameof(httpProtocolHelper));
            _logger = logger != null ? logger : NullLogger.Instance;
        }

        public async Task ProcessProxyRequestAsync(IClientHandler clientHandler, CancellationToken cancellationToken = default)
        {
            var client = clientHandler.Client;
            var remote = clientHandler.Remote;
            var requestContext = clientHandler.GetRequestContext();

            try
            {
                var localPipe = client.GetTransportPipe();

                // peek request headers: METHOD URL PROTOCOL
                var requestHeaders =
                    await _httpProtocolHelper.PeekRequestHeaderAsync(localPipe.Input, cancellationToken);

                _logger.LogInformation("--> Client {client}: request {method} {request} {protocol}",
                    client.RemoteEndPoint, requestHeaders.HttpMethod,
                    requestHeaders.RequestUrl, requestHeaders.Protocol);

                requestContext.RequestUrl = requestHeaders.RequestUrl;
                requestContext.RequestEndPointStr = requestHeaders.RequestEndPoint.EndPoint.ToString();

                await remote.ConnectAsync(requestHeaders.RequestEndPoint.EndPoint, cancellationToken)
                    .ConfigureAwait(false);

                var remotePipe = remote.GetTransportPipe();

                if (requestHeaders.HttpMethod.Method != ConnectMethod)
                {
                    ///// Common http /////

                    // start tunnel
                    await _httpProtocolHelper.StartTcpTunnelAsync(localPipe, remotePipe, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    //////// SSL/TLS ///////

                    // read rest of request
                    await _httpProtocolHelper.SkipToEndAsync(localPipe.Input, cancellationToken).ConfigureAwait(false);

                    // write connection ok
                    await _httpProtocolHelper.WriteConnectionOkAsync(localPipe.Output, cancellationToken)
                        .ConfigureAwait(false);

                    // start tunnel
                    await _httpProtocolHelper.StartTcpTunnelAsync(localPipe, remotePipe, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch (BadRequestException bre)
            {
                await HandleBadRequestException(bre, clientHandler, requestContext, cancellationToken);
            }
            catch (SocketException se)
            {
                await HandleSocketException(se, clientHandler, requestContext, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // it's ok
            }
            catch (Exception e)
            {
                clientHandler.SetError();
                _logger.LogError(e, "!!! Client communication exception");
            }
            finally
            {
                if (!clientHandler.HasError && remote != null)
                {
                    remote.Disconnect(true);
                }
            }
        }

        private async Task HandleBadRequestException(BadRequestException bre, IClientHandler clientHandler,
            HttpRequestContext httpRequestContext, CancellationToken cancellationToken)
        {
            try
            {
                var client = clientHandler.Client;

                _logger.LogInformation("--> Client {client}: bad request: {text}, session ended",
                    client.RemoteEndPoint, bre.Message);

                if (client.Connected)
                {
                    var localPipe = client.GetTransportPipe();

                    // read rest of request
                    await _httpProtocolHelper.SkipToEndAsync(localPipe.Input, cancellationToken);

                    // write BadRequest
                    await _httpProtocolHelper.WriteBadRequestAsync(localPipe.Output, cancellationToken);
                }
            }
            // catch (SocketException se)
            // {
            //     await HandleSocketException(se, clientHandler, httpRequestContext, cancellationToken);
            // }
            catch (OperationCanceledException)
            {
                // it's ok
            }
            catch (InvalidOperationException)
            {
                clientHandler.SetError();
            }
            catch (Exception e)
            {
                clientHandler.SetError();
                _logger.LogError(e, "!!! BadRequest response exception");
            }
        }

        private async Task HandleSocketException(SocketException se, IClientHandler clientHandler,
            HttpRequestContext httpRequestContext, CancellationToken cancellationToken)
        {
            try
            {
                var client = clientHandler.Client;

                _logger.LogTrace(se, "!!! Socket exception: {socketErrorCode}. Request: {url}, EP: {ep}"
                    , se.SocketErrorCode.ToString(), httpRequestContext.RequestUrl,
                    httpRequestContext.RequestEndPointStr);

                switch (se.SocketErrorCode)
                {
                    case SocketError.OperationAborted:
                    {
                        break;
                    }
                    case SocketError.HostNotFound:
                    {
                        if (client.Connected)
                        {
                            var localPipe = client.GetTransportPipe();

                            // read rest of request
                            await _httpProtocolHelper.SkipToEndAsync(localPipe.Input, cancellationToken);

                            // write BadGateway
                            await _httpProtocolHelper.WriteBadGatewayAsync(localPipe.Output, cancellationToken);
                        }

                        break;
                    }
                    case SocketError.HostUnreachable:
                    {
                        if (client.Connected)
                        {
                            var localPipe = client.GetTransportPipe();

                            // read rest of request
                            await _httpProtocolHelper.SkipToEndAsync(localPipe.Input, cancellationToken);

                            // write BadGateway
                            await _httpProtocolHelper.WriteBadGatewayAsync(localPipe.Output, cancellationToken);
                        }

                        break;
                    }
                    default:
                    {
                        _logger.LogWarning(se, "!!! Socket exception: {socketErrorCode}. Request: {url}, EP: {ep}"
                            , se.SocketErrorCode.ToString(), httpRequestContext.RequestUrl,
                            httpRequestContext.RequestEndPointStr);

                        break;
                    }
                }

                clientHandler.SetError();
            }
            catch (SocketException)
            {
                clientHandler.SetError();
            }
            catch (OperationCanceledException)
            {
                // it's ok
            }
            catch (InvalidOperationException)
            {
                clientHandler.SetError();
            }
            catch (Exception e)
            {
                clientHandler.SetError();
                _logger.LogError(e, "!!! HandleSocketException Error");
            }
        }
    }
}