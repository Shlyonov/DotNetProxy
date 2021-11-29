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
        private volatile int _readStarted = 0;
        private readonly int _maxAmountOfReads;
        private int _readsCount;

        public int IOTimeMs { get; set; } = 100;

        public InfiniteMemoryStream() : base()
        {
        }

        public InfiniteMemoryStream(int maxAmountOfReads) : base()
        {
            _maxAmountOfReads = maxAmountOfReads;
        }

        public InfiniteMemoryStream(byte[] bytes) : base(bytes)
        {
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> destination,
            CancellationToken cancellationToken = new())
        {
            Thread.Sleep(IOTimeMs);

            var lockIsFree = false;

            try
            {
                lockIsFree = Interlocked.CompareExchange(ref _readStarted, 1, 0) == 0;

                if (!lockIsFree)
                {
                    throw new Exception("Multithread read in now allowed!");
                }

                if (_maxAmountOfReads > 0 &&  _readsCount >= _maxAmountOfReads)
                {
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        await Task.Delay(100, cancellationToken);
                    }
                }

                _readsCount++;

                return await ValueTask.FromResult(100);
            }
            finally
            {
                if (lockIsFree)
                {
                    Interlocked.Exchange(ref _readStarted, 0);
                }
            }
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> source,
            CancellationToken cancellationToken = new())
        {
            await Task.Delay(IOTimeMs, cancellationToken);
            await base.WriteAsync(source, cancellationToken);
        }
    }
}