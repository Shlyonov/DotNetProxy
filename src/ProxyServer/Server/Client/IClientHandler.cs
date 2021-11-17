using System;
using ProxyServer.Http.Request;
using ProxyServer.Sockets;

namespace ProxyServer.Server.Client
{
    internal interface IClientHandler : IDisposable
    {
        public ISocketConnection Client { get; }
        public ISocketConnection Remote { get; }
        bool HasError { get;}
        void SetClientForRequestProcessing(PipeTcpClient client);
        HttpRequestContext GetRequestContext();
        void SetError();
        void Clean();
    }
}