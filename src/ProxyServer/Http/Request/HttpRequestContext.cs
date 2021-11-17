namespace ProxyServer.Http.Request
{
    internal class HttpRequestContext
    {
        public string ClientInfo { get; set; }
        public string RequestUrl { get; set; }
        public string RequestEndPointStr { get; set; }
    }
}