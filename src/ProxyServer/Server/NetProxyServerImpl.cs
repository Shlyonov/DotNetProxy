using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.ObjectPool;
using ProxyServer.Server.Client;
using ProxyServer.Server.Proxy;
using ProxyServer.Sockets;

namespace ProxyServer.Server
{
    internal sealed class NetProxyServerImpl
    {
        private readonly ProxyServerOptions _serverOptions;
        private readonly IProxyRequestHandler _proxyRequestHandler;
        private readonly ObjectPool<IClientHandler> _clientHandlerObjectPool;
        private readonly ILogger _logger;
        private volatile int _currentClientsCount;
        private PipeTcpListener _tcpListener;
        private CancellationTokenSource _cancellationTokenSource;
        public bool Active { get; private set; }

        public NetProxyServerImpl(ProxyServerOptions serverOptions, IProxyRequestHandler proxyRequestHandler,
            ObjectPool<IClientHandler> clientHandlerPool, ILogger<NetProxyServerImpl> logger)
        {
            _serverOptions = serverOptions ?? throw new ArgumentNullException(nameof(serverOptions));
            _proxyRequestHandler = proxyRequestHandler ?? throw new ArgumentNullException(nameof(proxyRequestHandler));
            _clientHandlerObjectPool = clientHandlerPool ?? throw new ArgumentNullException(nameof(clientHandlerPool));
            _logger = logger != null ? logger : NullLogger.Instance;

            ServicePointManager.DefaultConnectionLimit = _serverOptions.ConnectionLimit;
        }

        public async Task StartAsync(int port)
        {
            if (port <= 0) throw new ArgumentOutOfRangeException(nameof(port));

            _logger.LogInformation("--> Starting proxy server on port: {port}", port);

            if (Active)
                throw new InvalidOperationException("Proxy server has already started!");

            try
            {
                _tcpListener = new PipeTcpListener(IPAddress.Parse("0.0.0.0"), port)
                {
                    ExclusiveAddressUse = false
                };

                _tcpListener.Start(_serverOptions.Backlog);

                Active = true;

                _logger.LogInformation($"--> Proxy server success started!");

                _cancellationTokenSource = new CancellationTokenSource();

                await ProcessListenLoop(_tcpListener, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                // it's ok
            }
            catch (SocketException se) when (se.SocketErrorCode == SocketError.OperationAborted)
            {
                // it's ok server stopped
            }
            catch (Exception e)
            {
                _logger.LogError(e, "!!! Starting tcp server error!");
            }
            finally
            {
                _cancellationTokenSource.Dispose();
            }
        }

        public void Stop()
        {
            if (!Active)
                throw new InvalidOperationException("Server didn't start!");

            _cancellationTokenSource.Cancel();
            _tcpListener.Stop();
            _logger.LogInformation("--> Proxy server stopped!");
        }

        private async Task ProcessListenLoop(PipeTcpListener tcpListener, CancellationToken cancellationToken)
        {
            while (true)
            {
                if (cancellationToken.CanBeCanceled && cancellationToken.IsCancellationRequested)
                    break;

                var client = await tcpListener.AcceptPipeTcpClientAsync(_serverOptions).ConfigureAwait(false);

                // ReSharper disable once HeapView.ClosureAllocation
                var clientHandler = _clientHandlerObjectPool.Get();
                clientHandler.SetClientForRequestProcessing(client);

                ClientConnected(clientHandler);

                var unused = Task.Run(async () =>
                {
                    try
                    {
                        await _proxyRequestHandler.ProcessProxyRequestAsync(clientHandler, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Client request processing error!");
                    }
                    finally
                    {
                        ClientDisconnected(clientHandler);
                        _clientHandlerObjectPool.Return(clientHandler);
                    }
                }, cancellationToken);
            }
        }

        private void ClientConnected(IClientHandler clientHandler)
        {
            Interlocked.Increment(ref _currentClientsCount);
            _logger.LogInformation("--> Client {client}: connected, total connected clients: {clientsCount}"
                , clientHandler?.GetRequestContext()?.ClientInfo, _currentClientsCount.ToString());
        }

        private void ClientDisconnected(IClientHandler clientHandler)
        {
            Interlocked.Decrement(ref _currentClientsCount);
            _logger.LogInformation("--> Client {client}: session ended OK",
                clientHandler?.GetRequestContext()?.ClientInfo);
        }
    }
}