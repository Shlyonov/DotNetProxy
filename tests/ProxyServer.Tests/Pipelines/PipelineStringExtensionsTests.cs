using System;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ProxyServer.Pipelines;
using ProxyServer.Tests.TestUtils;
using ProxyServer.Tests.TestUtils.StreamTestHelpers;
using Shouldly;
using Xunit;

namespace ProxyServer.Tests.Pipelines
{
    public class PipelineStringExtensionsTests
    {
        #region [PeekLineAsync Tests]

        [Theory]
        [TextData("TestData/SimpleTestTextLF.txt")]
        [TextData("TestData/SimpleTestTextCRLF.txt")]
        [TextData("TestData/HttpRequest.txt")]
        [TextData("TestData/HttpsRequest.txt")]
        [TextData("TestData/EmptyLineCRLF.txt")]
        [TextData("TestData/EmptyLineLF.txt")]
        public async Task PeekLineAsync_ProperInput_ShouldReturnLine(string testText)
        {
            // arrange
            var msWithText = new MemoryStream(Encoding.ASCII.GetBytes(testText));
            var pipeReader = PipeReader.Create(msWithText);
            var streamReader = new StreamReader(new MemoryStream(Encoding.ASCII.GetBytes(testText)));
            var line1 = await streamReader.ReadLineAsync();

            // act
            var memContainer = await pipeReader.PeekLineAsync();

            // assert
            memContainer.Length.ShouldBe(line1.Length);
            Encoding.ASCII.GetString(memContainer.MemoryOwner.Memory.Span[..memContainer.Length]).ShouldBe(line1);

            // clean
            streamReader.Dispose();
            memContainer.Dispose();
        }

        [Theory]
        [TextData("TestData/SimpleTestTextLF.txt")]
        [TextData("TestData/SimpleTestTextCRLF.txt")]
        [TextData("TestData/HttpRequest.txt")]
        [TextData("TestData/HttpsRequest.txt")]
        [TextData("TestData/EmptyLineCRLF.txt")]
        [TextData("TestData/EmptyLineLF.txt")]
        public async Task PeekLineAsync_ProperInput_ShouldReturnLineAndNotConsumeBuffer(string testText)
        {
            // arrange
            var msWithText = new MemoryStream(Encoding.ASCII.GetBytes(testText));
            var pipeReader = PipeReader.Create(msWithText);
            var streamReader = new StreamReader(new MemoryStream(Encoding.ASCII.GetBytes(testText)));
            var line1 = await streamReader.ReadLineAsync();

            // act
            var memContainer = await pipeReader.PeekLineAsync();

            // assert
            memContainer.Length.ShouldBe(line1.Length);
            Encoding.ASCII.GetString(memContainer.MemoryOwner.Memory.Span[..memContainer.Length]).ShouldBe(line1);
            var buffer = new byte[line1.Length];
            var _ = pipeReader.AsStream().Read(buffer);
            Encoding.ASCII.GetString(buffer).ShouldBe(line1);

            // clean
            streamReader.Dispose();
            memContainer.Dispose();
        }

        [Theory]
        [InlineData("Not a line just string. No line ending!")]
        public async Task PeekLineAsync_NoLineEnding_ShouldReturnNull(string text)
        {
            // arrange
            var msWithText = new MemoryStream(Encoding.ASCII.GetBytes(text));
            var pipeReader = PipeReader.Create(msWithText);

            // act
            var memContainer = await pipeReader.PeekLineAsync();

            // assert
            memContainer.Length.ShouldBe(0);
            memContainer.MemoryOwner.ShouldBeNull();

            // clean
            memContainer.Dispose();
        }

        [Theory]
        [TextData("TestData/HttpRequest.txt")]
        [TextData("TestData/HttpsRequest.txt")]
        public void PeekLineAsync_BadReader_ShouldThrowInvalidOperationException(string testText)
        {
            // arrange
            var msWithText = new SlowMemoryStream(Encoding.ASCII.GetBytes(testText));
            var pipeReader = PipeReader.Create(msWithText);
            pipeReader.Complete();

            // act
            var peekLineTask = pipeReader.PeekLineAsync();

            // assert
            Should.Throw<InvalidOperationException>(async () => await peekLineTask);
        }

        [Theory]
        [TextData("TestData/SimpleTestTextLF.txt")]
        [TextData("TestData/SimpleTestTextCRLF.txt")]
        [TextData("TestData/HttpRequest.txt")]
        [TextData("TestData/HttpsRequest.txt")]
        public void PeekLineAsync_CancellationRequested_ShouldThrowOperationCanceledException(string testText)
        {
            // arrange
            var msWithText = new SlowMemoryStream(Encoding.ASCII.GetBytes(testText));
            var pipeReader = PipeReader.Create(msWithText);
            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            // act
            var peekLineTask = pipeReader.PeekLineAsync(cts.Token);

            // assert
            Should.Throw<OperationCanceledException>(async () => await peekLineTask);

            // clean
            cts.Dispose();
        }

        #endregion

        #region [ReadLineAsync Tests]

        [Theory]
        [TextData("TestData/SimpleTestTextLF.txt")]
        [TextData("TestData/SimpleTestTextCRLF.txt")]
        [TextData("TestData/HttpRequest.txt")]
        [TextData("TestData/HttpsRequest.txt")]
        [TextData("TestData/EmptyLineCRLF.txt")]
        [TextData("TestData/EmptyLineLF.txt")]
        public async Task ReadLineAsync_ProperInput_ShouldReturnLine(string testText)
        {
            // arrange
            var msWithText = new MemoryStream(Encoding.ASCII.GetBytes(testText));
            var pipeReader = PipeReader.Create(msWithText);
            var streamReader = new StreamReader(new MemoryStream(Encoding.ASCII.GetBytes(testText)));
            var line1 = await streamReader.ReadLineAsync();

            // act
            var memContainer = await pipeReader.ReadLineAsync();

            // assert
            memContainer.Length.ShouldBe(line1.Length);
            Encoding.ASCII.GetString(memContainer.MemoryOwner.Memory.Span[..memContainer.Length]).ShouldBe(line1);

            // clean
            streamReader.Dispose();
            memContainer.Dispose();
        }

        [Theory]
        [InlineData("Not a line just string. No line ending!")]
        public async Task ReadLineAsync_NoLineEnding_ShouldReturnNull(string text)
        {
            // arrange
            var msWithText = new MemoryStream(Encoding.ASCII.GetBytes(text));
            var pipeReader = PipeReader.Create(msWithText);

            // act
            var memContainer = await pipeReader.PeekLineAsync();

            // assert
            memContainer.Length.ShouldBe(0);
            memContainer.MemoryOwner.ShouldBeNull();

            // clean
            memContainer.Dispose();
        }

        [Theory]
        [TextData("TestData/HttpRequest.txt")]
        [TextData("TestData/HttpsRequest.txt")]
        public void ReadLineAsync_BadReader_ShouldThrowInvalidOperationException(string testText)
        {
            // arrange
            var msWithText = new SlowMemoryStream(Encoding.ASCII.GetBytes(testText));
            var pipeReader = PipeReader.Create(msWithText);
            pipeReader.Complete();

            // act
            var peekLineTask = pipeReader.PeekLineAsync();

            // assert
            Should.Throw<InvalidOperationException>(async () => await peekLineTask);
        }

        [Theory]
        [TextData("TestData/SimpleTestTextLF.txt")]
        [TextData("TestData/SimpleTestTextCRLF.txt")]
        [TextData("TestData/HttpRequest.txt")]
        [TextData("TestData/HttpsRequest.txt")]
        public void ReadLineAsync_CancellationRequested_ShouldThrowOperationCanceledException(string testText)
        {
            // arrange
            var msWithText = new SlowMemoryStream(Encoding.ASCII.GetBytes(testText));
            var pipeReader = PipeReader.Create(msWithText);
            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            // act
            var peekLineTask = pipeReader.PeekLineAsync(cts.Token);

            // assert
            Should.Throw<OperationCanceledException>(async () => await peekLineTask);

            // clean
            cts.Dispose();
        }

        #endregion

        #region [WriteLineAsync Tests]

        [Theory]
        [InlineData("GET www.google.com:443 HTTP/1.1")]
        [InlineData("Test line")]
        public async Task WriteLineAsync_ProperInput_ShouldReturnLine(string testText)
        {
            // arrange
            var msForWrite = new SlowMemoryStream();
            var pipeWriter = PipeWriter.Create(msForWrite);
            var bytesToWrite = Encoding.ASCII.GetBytes(testText);
            var streamReader = new StreamReader(msForWrite);

            // act
            await pipeWriter.WriteLineAsync(bytesToWrite);

            // assert
            msForWrite.Position = 0;
            (await streamReader.ReadLineAsync()).ShouldBe(testText);

            // clean
            streamReader.Dispose();
        }

        [Theory]
        [InlineData("Test line")]
        public void WriteLineAsync_BadWriter_ShouldThrowInvalidOperationException(string testText)
        {
            // arrange
            var msForWrite = new SlowMemoryStream();
            var pipeWriter = PipeWriter.Create(msForWrite);
            pipeWriter.Complete();
            var bytesToWrite = Encoding.ASCII.GetBytes(testText);

            // act
            var writeLineTask = pipeWriter.WriteLineAsync(bytesToWrite);

            // assert
            Should.Throw<InvalidOperationException>(async () => await writeLineTask);
        }

        [Theory]
        [InlineData("GET www.google.com:443 HTTP/1.1")]
        [InlineData("Test line")]
        public void WriteLineAsync_CancellationRequested_ShouldThrowOperationCanceledException(string testText)
        {
            // arrange
            var msForWrite = new SlowMemoryStream();
            var pipeWriter = PipeWriter.Create(msForWrite);
            var bytesToWrite = Encoding.ASCII.GetBytes(testText);
            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            // act
            var writeLineTask = pipeWriter.WriteLineAsync(bytesToWrite, cts.Token);

            // assert
            Should.Throw<OperationCanceledException>(async () => await writeLineTask);

            // clean
            cts.Dispose();
        }

        #endregion
    }
}