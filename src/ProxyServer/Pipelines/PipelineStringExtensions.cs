using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using ProxyServer.Buffers;

namespace ProxyServer.Pipelines
{
    internal static class PipelineStringExtensions
    {
        //    We parse lines via http specifications:
        //
        //    § 3.5 Message Parsing Robustness
        //
        //    Line parsing part:
        //
        //    Although the line terminator for the start-line and header fields is
        //    the sequence CRLF, a recipient MAY recognize a single LF as a line
        //    terminator and ignore any preceding CR.

        private const byte ByteCr = (byte) '\r';
        private const byte ByteLf = (byte) '\n';
        private static readonly ReadOnlyMemory<byte> CurrentLineEnding = new[] {ByteCr, ByteLf};

        public static async ValueTask<MemoryOwnerContainer<byte>> PeekLineAsync(this PipeReader pipeReader,
            CancellationToken cancellationToken = default)
        {
            if (pipeReader == null) throw new ArgumentNullException(nameof(pipeReader));

            while (true)
            {
                if (cancellationToken.CanBeCanceled)
                    cancellationToken.ThrowIfCancellationRequested();

                var result = await pipeReader.ReadAsync(cancellationToken).ConfigureAwait(false);

                if (result.IsCanceled)
                {
                    // Reader canceled, consumed zero
                    pipeReader.AdvanceTo(result.Buffer.Start);
                    throw new OperationCanceledException();
                }

                if (result.IsCompleted)
                {
                    // Reader completed, consumed zero
                    pipeReader.AdvanceTo(result.Buffer.Start);
                    return default;
                }

                var buffer = result.Buffer;

                if (TryPeekLineFromByteSequence(ref buffer, out var lineMemory))
                {
                    // Consumed and examined set to buffer.Start, because we want to peek line
                    // and allow following consumers examine from beginning
                    pipeReader.AdvanceTo(buffer.Start, buffer.Start);
                    return lineMemory;
                }

                // Haven't found line, want to examine more
                pipeReader.AdvanceTo(buffer.Start, buffer.End);
            }
        }

        public static async ValueTask<MemoryOwnerContainer<byte>> ReadLineAsync(this PipeReader pipeReader,
            CancellationToken cancellationToken = default)
        {
            if (pipeReader == null) throw new ArgumentNullException(nameof(pipeReader));

            while (true)
            {
                if (cancellationToken.CanBeCanceled)
                    cancellationToken.ThrowIfCancellationRequested();

                // Since we want to peek line we will not consume any data
                var result = await pipeReader.ReadAsync(cancellationToken).ConfigureAwait(false);

                if (result.IsCanceled)
                {
                    // Reader canceled, consumed zero
                    pipeReader.AdvanceTo(result.Buffer.Start);
                    throw new OperationCanceledException();
                }

                if (result.IsCompleted)
                {
                    // Reader completed, consumed zero
                    pipeReader.AdvanceTo(result.Buffer.Start);
                    return default;
                }

                var buffer = result.Buffer;

                if (TryReadLineFromByteSequence(ref buffer, out var line, out var position))
                {
                    // Have found line, all data is consumed 
                    pipeReader.AdvanceTo(position, position);
                    return line;
                }

                // Haven't found line, want to examine more, consumed zero
                pipeReader.AdvanceTo(buffer.Start, buffer.End);
            }
        }

        public static async ValueTask WriteLineAsync(this PipeWriter pipeWriter, ReadOnlyMemory<byte> line,
            CancellationToken cancellationToken = default)
        {
            if (pipeWriter == null) throw new ArgumentNullException(nameof(pipeWriter));
            
            var lengthWithEnding = line.Span.Length + CurrentLineEnding.Length;
            var writeMem = pipeWriter.GetMemory(lengthWithEnding);

            line.CopyTo(writeMem);
            CurrentLineEnding.CopyTo(writeMem[line.Span.Length..]);
            pipeWriter.Advance(lengthWithEnding);

            var flushResult = await pipeWriter.FlushAsync(cancellationToken).ConfigureAwait(false);

            if (flushResult.IsCanceled || flushResult.IsCompleted)
                throw new OperationCanceledException();
        }


        private static bool TryPeekLineFromByteSequence(ref ReadOnlySequence<byte> buffer,
            out MemoryOwnerContainer<byte> memoryOwnerContainer)
        {
            var reader = new SequenceReader<byte>(buffer);

            if (reader.TryReadTo(out ReadOnlySpan<byte> requestLineByteSpan, ByteLf, advancePastDelimiter: true))
            {
                var lineL = requestLineByteSpan.Length;

                if (lineL > 0)
                {
                    // Since we read to ByteLf we have to trim ByteCr before if exists
                    var lastSymbol = requestLineByteSpan[lineL - 1];

                    if (lastSymbol == ByteCr)
                        lineL--; // ByteCr founded, adjust line length
                }
                
                var lineMemory = MemoryPool<byte>.Shared.Rent(lineL);

                requestLineByteSpan[..lineL].CopyTo(lineMemory.Memory.Span);

                memoryOwnerContainer = new MemoryOwnerContainer<byte>(lineMemory, lineL);

                // line founded
                return true;
            }

            memoryOwnerContainer = default;

            // didn't find line
            return false;
        }

        private static bool TryReadLineFromByteSequence(ref ReadOnlySequence<byte> buffer, out MemoryOwnerContainer<byte> memoryOwnerContainer,
            out SequencePosition position)
        {
            var reader = new SequenceReader<byte>(buffer);

            var result = false;

            if (reader.TryReadTo(out ReadOnlySpan<byte> requestLineByteSpan, ByteLf, advancePastDelimiter: true))
            {
                var lineL = requestLineByteSpan.Length;

                if (lineL > 0)
                {
                    // Since we read to ByteLf we have to trim ByteCr before if exists
                    var lastSymbol = requestLineByteSpan[lineL - 1];

                    if (lastSymbol == ByteCr)
                        lineL--; // ByteCr founded, adjust line length
                }

                var lineByteSpan = MemoryPool<byte>.Shared.Rent(lineL);

                requestLineByteSpan[..lineL].CopyTo(lineByteSpan.Memory.Span);

                memoryOwnerContainer = new MemoryOwnerContainer<byte>(lineByteSpan, lineL);

                // line founded
                result = true;
            }
            else
            {
                memoryOwnerContainer = default;
            }

            position = reader.Position;

            // didn't find line
            return result;
        }
    }
}