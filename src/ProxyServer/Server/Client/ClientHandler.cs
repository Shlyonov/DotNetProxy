using System;
using System.Net.Sockets;
using ProxyServer.Http.Request;
using ProxyServer.Sockets;

namespace ProxyServer.Server.Client
{
    internal sealed class ClientHandler : IClientHandler
    {
        private readonly ISocketOptions _socketOptions;

        private PipeTcpClient _client;
        private PipeTcpClient _remote;
        private HttpRequestContext _httpRequestContext;
        private bool _hasError;

        public ClientHandler(ISocketOptions socketOptions)
        {
            _socketOptions = socketOptions ?? throw new ArgumentNullException(nameof(socketOptions));


            _hasError = false;
        }

        public ISocketConnection Client => _client;
        public ISocketConnection Remote => _remote ??= CreateNewTcpClient();
        public bool HasError => _hasError;

        public void SetClientForRequestProcessing(PipeTcpClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _httpRequestContext = new HttpRequestContext();
            _httpRequestContext.ClientInfo = _client.Client.RemoteEndPoint?.ToString();
        }

        public HttpRequestContext GetRequestContext()
        {
            return _httpRequestContext;
        }

        public void SetError()
        {
            _hasError = true;
        }

        public void Clean()
        {
            _client?.Dispose();
            _client = null;
            
            _hasError = false;
            _httpRequestContext = null;
        }

        public void Dispose()
        {
            Clean();
            
            _remote?.Dispose();
        }

        private PipeTcpClient CreateNewTcpClient()
        {
            var newClient = new PipeTcpClient(_socketOptions);
            newClient.LingerState = new LingerOption(false, 0);
            return newClient;
        }
    }
}