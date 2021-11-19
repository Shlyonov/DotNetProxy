using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProxyServer.Tests.TestUtils.StreamTestHelpers
{
    internal class InfiniteMemoryStream : MemoryStream
    {
        public InfiniteMemoryStream() : base()
        {

        }
        public InfiniteMemoryStream(byte[] bytes) : base(bytes)
        {

        }        

        public override ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = new CancellationToken())
        {
            Thread.Sleep(100);
            return ValueTask.FromResult(100);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = new CancellationToken())
        {
            Thread.Sleep(100);
            return base.WriteAsync(source, cancellationToken);
        }
    }
}
