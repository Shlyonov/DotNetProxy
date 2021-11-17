using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ProxyServer.Pipelines
{
    internal static class PipelineExtensions
    {
        /// <summary>
        /// Simple method for reading data from PipeReader and coping it to PipeWriter.
        /// It's a replacement of PipeWriter.CopyToAsync() which has two excess memory allocations
        /// </summary>
        /// <param name="reader">PipeReader source copy from</param>
        /// <param name="writer">PipeWriter copy to</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Is coping will be continued</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async ValueTask<bool> ReadToAsync(this PipeReader reader, PipeWriter writer,
            CancellationToken cancellationToken = default)
        {
            var readResult = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
         
            if (readResult.IsCanceled)
            {
                // Reader canceled, consumed zero, copying will not be continued
                reader.AdvanceTo(readResult.Buffer.Start);
                return false;
            }
                     
            if (readResult.IsCompleted)
            {
                // Reader completed, consumed zero, copying will not be continued
                reader.AdvanceTo(readResult.Buffer.Start);
                return false;
            }
         
            var read = (int) readResult.Buffer.Length;
                     
            var writeMem = writer.GetMemory(read);
         
            readResult.Buffer.CopyTo(writeMem.Span);
         
            // Consume all available data since we have to write it
            reader.AdvanceTo(readResult.Buffer.End, readResult.Buffer.End);
         
            // All data has written
            writer.Advance(read);
         
            var flushResult = await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

            // if write stream completed or canceled copying will not be continued
            return !flushResult.IsCompleted && !flushResult.IsCanceled;
        }
    }
}