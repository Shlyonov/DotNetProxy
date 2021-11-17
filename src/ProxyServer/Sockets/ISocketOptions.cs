namespace ProxyServer.Sockets
{
    public interface ISocketOptions
    {
        int ConnectTimeout { get; }
        int SendTimeout { get; }
        int ReceiveTimeout { get; }
    }
}