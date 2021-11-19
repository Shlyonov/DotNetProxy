using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ProxyServer.Tests.TestUtils.StreamTestHelpers
{
    public class DelayedCompleteMemoryStream : MemoryStream
    {
        private const int _delayAfterRead = 1000;

        public DelayedCompleteMemoryStream() : base()
        {
            
        }

        public DelayedCompleteMemoryStream(byte[] bytes) : base(bytes)
        {

        }
    
        public override async ValueTask<int> ReadAsync(Memory<byte> destination,
            CancellationToken cancellationToken = new CancellationToken())
        {
            await Task.Delay(100, cancellationToken);
            var read = await base.ReadAsync(destination, cancellationToken);
            if(read == 0)
                Thread.Sleep(_delayAfterRead);
            return read;
        }
    }
}