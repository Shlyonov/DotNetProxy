using System;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
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
    public class PipelineExtensionsTests
    {
        [Theory]
        [TextData("TestData/SimpleTestTextLF.txt")]
        [TextData("TestData/SimpleTestTextCRLF.txt")]
        [TextData("TestData/HttpRequest.txt")]
        [TextData("TestData/HttpsRequest.txt")]
        public async Task ReadToAsync_ProperInput_ShouldCopyOk(string testText)
        {
            // arrange
            var msWithText = new MemoryStream(Encoding.ASCII.GetBytes(testText));
            var pipeReader = PipeReader.Create(msWithText);
            var msForWrite = new MemoryStream();
            var pipeWriter = PipeWriter.Create(msForWrite);

            // act
            while (await pipeReader.ReadToAsync(pipeWriter))
            {
            }

            // assert
            msWithText.Position.ShouldBe(msWithText.Length);
            msWithText.ToArray().SequenceEqual(msForWrite.ToArray()).ShouldBeTrue();
            msForWrite.Position.ShouldBe(msWithText.Length);
        }

        [Theory]
        [TextData("TestData/SimpleTestTextLF.txt")]
        [TextData("TestData/SimpleTestTextCRLF.txt")]
        [TextData("TestData/HttpRequest.txt")]
        [TextData("TestData/HttpsRequest.txt")]
        public async Task ReadToAsync_CancellationRequested_ShouldThrowOperationCanceledException(string testText)
        {
            // arrange
            var msWithText = new SlowMemoryStream(Encoding.ASCII.GetBytes(testText));
            var pipeReader = PipeReader.Create(msWithText);
            var msForWrite = new SlowMemoryStream();
            var pipeWriter = PipeWriter.Create(msForWrite);
            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            
            // act
            var readToTask = pipeReader.ReadToAsync(pipeWriter, cts.Token);

            // assert
            await Should.ThrowAsync<OperationCanceledException>(async () => await readToTask);
            
            // clean
            cts.Dispose();
        }
    }
}