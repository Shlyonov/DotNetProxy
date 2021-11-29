using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ProxyServer.Tests.TestUtils.StreamTestHelpers
{
    public class SlowMemoryStream : MemoryStream
    {
        public SlowMemoryStream() : base()
        {
            
        }
        public SlowMemoryStream(byte[] bytes) : base(bytes)
        {
        
        }
    
        public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = new())
        {
            await Task.Delay(1000, cancellationToken);
            return await base.ReadAsync(destination, cancellationToken);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = new())
        {
            await Task.Delay(1000, cancellationToken);
            await base.WriteAsync(source, cancellationToken);
        }
    }
}