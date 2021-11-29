using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Pipelines.Sockets.Unofficial;
using ProxyServer.Pipelines;

namespace ProxyServer.Sockets.Tunnel
{
    internal sealed class TcpTunnel : ITcpTunnel
    {
        private const int KeepAlivePeriodCheckMs = 100;
        private readonly ITunnelOptions _tunnelOptions;
        private volatile int _operationFired = 0;

        public TcpTunnel(ITunnelOptions tunnelOptions)
        {
            _tunnelOptions = tunnelOptions ?? throw new ArgumentNullException(nameof(tunnelOptions));
        }

        public async Task StartTunnelAsync(IDuplexPipe local, IDuplexPipe remote,
            CancellationToken cancellationToken = default)
        {
            if (local == null) throw new ArgumentNullException(nameof(local));
            if (remote == null) throw new ArgumentNullException(nameof(remote));

            // A little trick for simplification: we listen local and remote sockets, when one of them receives 0 bytes
            // (IsCompleted) and stops its loop we have to stop loop of another one
            // For the best performance we have to use more complicated way:
            // PipeReader.CancelPendingRead, PipeWriter.CancelPendingFlush
            var tunnelCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            await Task.WhenAll(
                CheckTimeOut(_tunnelOptions.KeepAliveTimeout, tunnelCancellationTokenSource),
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

                    SetOperationFired();
                }
            }
            catch (ConnectionResetException)
            {
                // it's ok RST packet received, which indicates an immediate dropping of the connection
                // tunnel ended
            }
            catch (OperationCanceledException) when (tunnelCancellationTokenSource.IsCancellationRequested)
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

                    SetOperationFired();
                }
            }
            catch (ConnectionResetException)
            {
                // it's ok RST packet received, which indicates an immediate dropping of the connection,
                // tunnel ended
            }
            catch (OperationCanceledException) when (tunnelCancellationTokenSource.IsCancellationRequested)
            {
                // it's ok, tunnel ended
            }
            finally
            {
                tunnelCancellationTokenSource.Cancel();
            }
        }

        private async Task CheckTimeOut(int keepAlive, CancellationTokenSource cancellationTokenSource)
        {
            var sw = Stopwatch.StartNew();

            do
            {
                try
                {
                    var now = DateTime.Now;
                    
                    await Task.Delay(KeepAlivePeriodCheckMs, cancellationTokenSource.Token).ConfigureAwait(false);
               
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    if (Interlocked.CompareExchange(ref _operationFired, 0, 1) == 1)
                    {
                        sw.Restart();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            } while (sw.ElapsedMilliseconds < keepAlive);

            if (!cancellationTokenSource.IsCancellationRequested)
            {
                cancellationTokenSource.Cancel();
            }
        }

        private void SetOperationFired()
        {
            Interlocked.Exchange(ref _operationFired, 1);
        }
    }
}