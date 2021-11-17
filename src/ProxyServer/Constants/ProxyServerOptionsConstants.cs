namespace ProxyServer.Constants
{
    internal static class ProxyServerOptionsConstants
    {
        public const int DefaultConnectionLimit = 10000;
        public const int DefaultBacklog = 5000;
        public const int DefaultKeepAliveTimeout = 10000;
        public const int DefaultConnectTimeout = 5000;
        public const int DefaultSendTimeout = 30000;
        public const int DefaultReceiveTimeout = 30000;
    }
}