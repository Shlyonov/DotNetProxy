using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Pipelines.Sockets.Unofficial;

namespace ProxyServer.Sockets
{
    /// <summary>
    /// It's an analog of TcpClient with two main differences:
    /// 1) ctor(Socket acceptedSocket), which is internal in TcpClient,
    /// also it's a reason that we are not able to subclass from TcpClient:
    /// we can't use internal ctor is a base and default ctor of TcpClient
    /// creates excess Socket
    /// </summary>
    internal sealed class PipeTcpClient : ISocketConnection, IDisposable
    {
        // Used by the class to indicate that a connection has been made.
#pragma warning disable 414
        private bool _active;
#pragma warning restore 414
        private Socket _clientSocket;
        private IDuplexPipe _dataPipe = null!;
        private volatile int _disposed;
        private volatile int _internalClientDisposed;
        private AddressFamily _family;

        // Initializes a new instance of the System.Net.Sockets.TcpClient class.
        public PipeTcpClient(ISocketOptions socketOptions) : this(AddressFamily.Unknown, socketOptions)
        {
        }

        // Initializes a new instance of the System.Net.Sockets.TcpClient class.
        private PipeTcpClient(AddressFamily family, ISocketOptions socketOptions)
        {
            // Validate parameter
            if (family != AddressFamily.InterNetwork &&
                family != AddressFamily.InterNetworkV6 &&
                family != AddressFamily.Unknown)
                throw new ArgumentException("Protocol invalid family", nameof(family));

            _family = family;
            InitializeClientSocket();
            ConnectTimeout = socketOptions.ConnectTimeout;
            _clientSocket.ReceiveTimeout = socketOptions.ReceiveTimeout;
            _clientSocket.SendTimeout = socketOptions.SendTimeout;
        }
        
        // Used by TcpListener.Accept().
        internal PipeTcpClient(Socket acceptedSocket, ISocketOptions socketOptions)
        {
            _clientSocket = acceptedSocket;
            _clientSocket.ReceiveTimeout = socketOptions.ReceiveTimeout;
            _clientSocket.SendTimeout = socketOptions.SendTimeout;
            _family = acceptedSocket.AddressFamily;
            _active = true;
        }

        private bool Disposed => _disposed != 0 || _internalClientDisposed != 0;

        // Used by the class to provide the underlying network socket.
        public Socket Client => Disposed ? null! : _clientSocket;

        public EndPoint RemoteEndPoint => _clientSocket?.RemoteEndPoint;

        public bool Connected => Client?.Connected ?? false;

        // Gets or sets the size of the receive buffer in bytes.
        public int ReceiveBufferSize
        {
            get => (int) Client.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer)!;
            set => Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, value);
        }

        // Gets or sets the size of the send buffer in bytes.
        public int SendBufferSize
        {
            get => (int) Client.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer)!;
            set => Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, value);
        }

        // Gets or sets the connect time out value of the connection in milliseconds.
        public int ConnectTimeout { get; set; }

        // Gets or sets the receive time out value of the connection in milliseconds.
        public int ReceiveTimeout
        {
            get => (int) Client.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout)!;
            set => Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, value);
        }

        // Gets or sets the send time out value of the connection in milliseconds.
        public int SendTimeout
        {
            get => (int) Client.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout)!;
            set => Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, value);
        }

        // Gets or sets the value of the connection's linger option.
        [DisallowNull]
        public LingerOption LingerState
        {
            get => Client.LingerState;
            set => Client.LingerState = value!;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public async ValueTask ConnectAsync(EndPoint ep, CancellationToken cancellationToken)
        {
            var connectCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            
            if (ConnectTimeout > 0)
                connectCancellationSource.CancelAfter(ConnectTimeout);

            var isTimeouts = false;

            try
            {
                await CompleteConnectAsync(Client.ConnectAsync(ep, connectCancellationSource.Token));
            }
            catch (OperationCanceledException oce) when (oce.CancellationToken == connectCancellationSource.Token
                                                         && connectCancellationSource.IsCancellationRequested)
            {
                Interlocked.Exchange(ref _internalClientDisposed, 1);
                isTimeouts = true;
            }

            connectCancellationSource.Dispose();

            if (isTimeouts)
                throw new SocketException((int) SocketError.OperationAborted);
        }

        public void Disconnect(bool reuseToken)
        {
            try
            {
                CloseTransportPipe();

                var chkClientSocket = Volatile.Read(ref _clientSocket);
                if (chkClientSocket != null)
                {
                    chkClientSocket.Shutdown(SocketShutdown.Both);
                    chkClientSocket.Disconnect(reuseToken);
                }
            }
            catch (SocketException se) when (se.SocketErrorCode == SocketError.NotConnected)
            {
                // Could be a race, furthermore there is no method to determine
                // that socket is 100% not connected at the moment.
                // Socket.Connected returns the latest known state of the Socket,
                // but this state could be outdated.
            }

            _active = false;
        }

        public IDuplexPipe GetTransportPipe()
        {
            ThrowIfDisposed();

            if (!Connected) throw new InvalidOperationException("Not connected");

            return _dataPipe ??= SocketConnection.Create(Client, new PipeOptions(useSynchronizationContext: false));
        }

        private async ValueTask CompleteConnectAsync(ValueTask task)
        {
            await task.ConfigureAwait(false);
            _active = true;
        }

        // Disposes the Tcp connection.
        private void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0) return;

            if (!disposing) return;

            CloseTransportPipe();

            var chkClientSocket = Volatile.Read(ref _clientSocket);
            if (chkClientSocket != null)
            {
                try
                {
                    if (!chkClientSocket.Connected)
                        chkClientSocket.Shutdown(SocketShutdown.Both);
                }
                catch (SocketException se) when (se.SocketErrorCode == SocketError.NotConnected)
                {
                    // Could be a race, furthermore there is no method to determine
                    // that socket is 100% not connected at the moment.
                    // Socket.Connected returns the latest known state of the Socket,
                    // but this state could be outdated.
                    // TcpSocket uses InternalShutdown() method which don't throw
                }
                finally
                {
                    // Client socket could be disposed by failed Connect
                    if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
                    {
                        chkClientSocket.Close();
                    }
                }
            }

            GC.SuppressFinalize(this);
        }

        ~PipeTcpClient()
        {
            Dispose(false);
        }

        private void InitializeClientSocket()
        {
            Debug.Assert(_clientSocket == null);
            if (_family == AddressFamily.Unknown)
            {
                // If AF was not explicitly set try to initialize dual mode socket or fall-back to IPv4.
                _clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                if (_clientSocket.AddressFamily == AddressFamily.InterNetwork) _family = AddressFamily.InterNetwork;
            }
            else
            {
                _clientSocket = new Socket(_family, SocketType.Stream, ProtocolType.Tcp);
            }
        }

        private void ThrowIfDisposed()
        {
            if (Disposed) ThrowObjectDisposedException();

            void ThrowObjectDisposedException()
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        private void CloseTransportPipe()
        {
            if (_dataPipe != null)
            {
                _dataPipe.Input.CancelPendingRead();
                _dataPipe.Input.Complete();

                _dataPipe.Output.CancelPendingFlush();
                _dataPipe.Output.Complete();

                _dataPipe = null;
            }
        }
    }
}