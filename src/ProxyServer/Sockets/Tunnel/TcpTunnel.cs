using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Pipelines.Sockets.Unofficial;
using ProxyServer.Pipelines;

namespace ProxyServer.Sockets.Tunnel
{
    internal sealed class TcpTunnel : ITcpTunnel
    {
        private readonly ITunnelOptions _tunnelOptions;

        public TcpTunnel(ITunnelOptions tunnelOptions)
        {
            _tunnelOptions = tunnelOptions ?? throw new ArgumentNullException(nameof(tunnelOptions));
        }
        
        public async Task StartTunnelAsync(IDuplexPipe local, IDuplexPipe remote, CancellationToken cancellationToken = default)
        {
            if (local == null) throw new ArgumentNullException(nameof(local));
            if (remote == null) throw new ArgumentNullException(nameof(remote));
            
            // A little trick for simplification: we listen local and remote sockets, when one of them receives 0 bytes
            // (IsCompleted) and stops its loop we have to stop loop of another one
            // For the best performance we have to use more complicated way:
            // PipeReader.CancelPendingRead, PipeWriter.CancelPendingFlush
            var tunnelCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            tunnelCancellationTokenSource.CancelAfter(_tunnelOptions.KeepAliveTimeout);

            await Task.WhenAll(
                ListenLocalTcp(local.Input, remote.Output, tunnelCancellationTokenSource),
                ListenRemoteTcp(remote.Input, local.Output, tunnelCancellationTokenSource)
            );

            tunnelCancellationTokenSource.Dispose();
        }

        private async Task ListenLocalTcp(PipeReader localReader, PipeWriter remoteWriter,
            CancellationTokenSource tunnelCancellationTokenSource)
        {
            try
            {
                var tunnelCancellationToken = tunnelCancellationTokenSource.Token;

                while (true)
                {
                    tunnelCancellationToken.ThrowIfCancellationRequested();

                    if (!await localReader.ReadToAsync(remoteWriter, tunnelCancellationToken)
                        .ConfigureAwait(false)) break;
                }
            }
            catch (ConnectionResetException)
            {
                // it's ok RST packet received, which indicates an immediate dropping of the connection
                // tunnel ended
            }
            catch (OperationCanceledException oce) when (oce.CancellationToken == tunnelCancellationTokenSource.Token)
            {
                // it's ok, tunnel ended
            }
            finally
            {
                tunnelCancellationTokenSource.Cancel();
            }
        }

        private async Task ListenRemoteTcp(PipeReader remoteReader, PipeWriter localWriter,
            CancellationTokenSource tunnelCancellationTokenSource)
        {
            try
            {
                var tunnelCancellationToken = tunnelCancellationTokenSource.Token;

                while (true)
                {
                    tunnelCancellationToken.ThrowIfCancellationRequested();

                    if (!await remoteReader.ReadToAsync(localWriter, tunnelCancellationToken)
                        .ConfigureAwait(false)) break;
                }
            }
            catch (ConnectionResetException)
            {
                // it's ok RST packet received, which indicates an immediate dropping of the connection,
                // tunnel ended
            }
            catch (OperationCanceledException oce) when (oce.CancellationToken == tunnelCancellationTokenSource.Token)
            {
                // it's ok, tunnel ended
            }
            finally
            {
                tunnelCancellationTokenSource.Cancel();
            }
        }
    }
}