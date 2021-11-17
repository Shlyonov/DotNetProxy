using System.Net.Http;
using ProxyServer.Utils;

namespace ProxyServer.Http.Request
{
    internal class HttpRequestHeaders
    {
        public HttpMethod HttpMethod { get; init; }
        public string RequestUrl { get; init; }
        public EndPointContainer RequestEndPoint{ get; init; }
        public string Protocol { get; init; }
    }
}