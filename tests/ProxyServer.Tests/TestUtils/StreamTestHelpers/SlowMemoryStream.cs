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
    
        public override ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = new CancellationToken())
        {
            Thread.Sleep(1000);
            return base.ReadAsync(destination, cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = new CancellationToken())
        {
            Thread.Sleep(1000);
            return base.WriteAsync(source, cancellationToken);
        }
    }
}