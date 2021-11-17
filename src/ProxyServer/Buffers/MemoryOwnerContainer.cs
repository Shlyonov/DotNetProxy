using System;
using System.Buffers;

namespace ProxyServer.Buffers
{
    public readonly struct MemoryOwnerContainer<T> : IDisposable
    {
        public MemoryOwnerContainer(IMemoryOwner<T> memoryOwner, int length)
        {
            MemoryOwner = memoryOwner ?? throw new ArgumentNullException(nameof(memoryOwner));
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            
            Length = length;
        }

        public IMemoryOwner<T> MemoryOwner { get;  }
        public int Length { get;  }

        public void Dispose()
        {
            MemoryOwner?.Dispose();
        }
    }
}