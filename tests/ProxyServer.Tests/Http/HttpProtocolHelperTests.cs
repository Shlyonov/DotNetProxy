using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ProxyServer.Exceptions;
using ProxyServer.Http;
using ProxyServer.Sockets.Tunnel;
using ProxyServer.Tests.TestUtils;
using ProxyServer.Tests.TestUtils.StreamTestHelpers;
using Shouldly;
using Xunit;

namespace ProxyServer.Tests.Http
{
    public class HttpProtocolHelperTests
    {
        #region [PeekRequestHeaderAsync Tests]

        [Theory]
        [TextData("TestData/HttpRequest.txt")]
        [TextData("TestData/HttpsRequest.txt")]
        public async Task PeekRequestHeaderAsync_ProperInput_ShouldReturnHeaders(string testText)
        {
            // arrange
            var msWithText = new MemoryStream(Encoding.ASCII.GetBytes(testText));
            var pipeReader = PipeReader.Create(msWithText);
            var streamReader = new StreamReader(new MemoryStream(Encoding.ASCII.GetBytes(testText)));
            var line1 = await streamReader.ReadLineAsync();
            var line1Splatted = line1.Split(" ");
            var httpProtocolHelper = new HttpProtocolHelper(new Mock<ITcpTunnel>().Object);

            // act
            var requestHeaders = await httpProtocolHelper.PeekRequestHeaderAsync(pipeReader);

            // assert
            requestHeaders.ShouldNotBeNull();
            requestHeaders.HttpMethod.ShouldBe(new HttpMethod(line1Splatted[0]));
            requestHeaders.RequestUrl.ShouldBe(line1Splatted[1]);
            requestHeaders.Protocol.ShouldBe(line1Splatted[2]);
            var requestHeaderEp = requestHeaders.RequestEndPoint.EndPoint.ToString()
                ?.Replace("InterNetwork/", String.Empty);
            Uri.TryCreate(requestHeaderEp, UriKind.Absolute, out var uri).ShouldBeTrue();
            line1Splatted[1].Contains(uri.Host).ShouldBeTrue();

            // clean
            streamReader.Dispose();
        }

        [Theory]
        [InlineData("GER www.google.com:443 HTTP/1.1")]
        [InlineData("GET www%google*com:443 HTTP/1.1")]
        [InlineData("GET www.google.com:443 HTTC/5")]
        public async Task PeekRequestHeaderAsync_CorruptedHeader_ShouldThrowBadRequest(string testText)
        {
            // arrange
            var msWithText = new MemoryStream(Encoding.ASCII.GetBytes(testText));
            var pipeReader = PipeReader.Create(msWithText);
            var httpProtocolHelper = new HttpProtocolHelper(new Mock<ITcpTunnel>().Object);

            // act
            var peekRequestHeaderTask = httpProtocolHelper.PeekRequestHeaderAsync(pipeReader);

            // assert
            await Should.ThrowAsync<BadRequestException>(async () => await peekRequestHeaderTask);
        }

        [Theory]
        [TextData("TestData/HttpRequest.txt")]
        public async Task PeekRequestHeaderAsync_CancellationRequested_ShouldThrowOperationCanceledException(string testText)
        {
            // arrange
            var msWithText = new SlowMemoryStream(Encoding.ASCII.GetBytes(testText));
            var pipeReader = PipeReader.Create(msWithText);
            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            var httpProtocolHelper = new HttpProtocolHelper(new Mock<ITcpTunnel>().Object);

            // act
            var readToTask = httpProtocolHelper.PeekRequestHeaderAsync(pipeReader, cts.Token);

            // assert
            await Should.ThrowAsync<OperationCanceledException>(async () => await readToTask);

            // clean
            cts.Dispose();
        }

        #endregion

        #region [SkipToEndAsync Tests]

        [Theory]
        [TextData("TestData/HttpRequest.txt")]
        [TextData("TestData/HttpsRequest.txt")]
        public async Task SkipToEndAsync_ProperInput_ShouldOk(string testText)
        {
            // arrange
            var msWithText = new MemoryStream(Encoding.ASCII.GetBytes(testText));
            var pipeReader = PipeReader.Create(msWithText);
            var httpProtocolHelper = new HttpProtocolHelper(new Mock<ITcpTunnel>().Object);

            // act
            await httpProtocolHelper.SkipToEndAsync(pipeReader);

            // assert
            msWithText.Position.ShouldBe(msWithText.Length);
            pipeReader.TryRead(out var readResult).ShouldBeFalse();
        }

        [Theory]
        [TextData("TestData/HttpRequest.txt")]
        [TextData("TestData/HttpsRequest.txt")]
        public async Task SkipToEndAsync_ProperInputFromMiddle_ShouldOk(string testText)
        {
            // arrange
            SequencePosition AdvanceSequenceReader(ReadResult readResultArrange1)
            {
                var sequenceReader = new SequenceReader<byte>(readResultArrange1.Buffer);
                sequenceReader.Advance(50);
                return sequenceReader.Position;
            }

            var msWithText = new MemoryStream(Encoding.ASCII.GetBytes(testText));
            var pipeReader = PipeReader.Create(msWithText);
            var httpProtocolHelper = new HttpProtocolHelper(new Mock<ITcpTunnel>().Object);
            var readResultArrange = await pipeReader.ReadAsync();
            var advancedPosition = AdvanceSequenceReader(readResultArrange);
            pipeReader.AdvanceTo(advancedPosition, advancedPosition);

            // act
            await httpProtocolHelper.SkipToEndAsync(pipeReader);

            // assert
            msWithText.Position.ShouldBe(msWithText.Length);
            pipeReader.TryRead(out var readResult).ShouldBeFalse();
        }

        [Theory]
        [TextData("TestData/HttpRequest.txt")]
        [TextData("TestData/HttpsRequest.txt")]
        public async Task SkipToEndAsync_CancellationRequested_ShouldThrowOperationCanceledException(string testText)
        {
            // arrange
            var msWithText = new SlowMemoryStream(Encoding.ASCII.GetBytes(testText));
            var pipeReader = PipeReader.Create(msWithText);
            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            var httpProtocolHelper = new HttpProtocolHelper(new Mock<ITcpTunnel>().Object);

            // act
            var skipToEndTask = httpProtocolHelper.SkipToEndAsync(pipeReader, cts.Token);

            // assert
            await Should.ThrowAsync<OperationCanceledException>(async () => await skipToEndTask);
        }

        #endregion

        #region [Write responses Tests]

        [Fact]
        public async Task WriteConnectionOkAsync_ProperWriter_ShouldOk()
        {
            // arrange
            var msForWrite = new MemoryStream();
            var pipeWriter = PipeWriter.Create(msForWrite);
            var streamReader = new StreamReader(msForWrite);
            var httpProtocolHelper = new HttpProtocolHelper(new Mock<ITcpTunnel>().Object);

            // act
            await httpProtocolHelper.WriteConnectionOkAsync(pipeWriter);

            // assert
            msForWrite.Position = 0;
            (await streamReader.ReadLineAsync()).ShouldBe("HTTP/1.1 200 Connection Established");
            (await streamReader.ReadLineAsync()).ShouldBe("");
        }
        
        [Fact]
        public async Task WriteConnectionOkAsync_BadWriter_ShouldThrowInvalidOperation()
        {
            // arrange
            var msForWrite = new MemoryStream();
            var pipeWriter = PipeWriter.Create(msForWrite);
            await pipeWriter.CompleteAsync();
            var httpProtocolHelper = new HttpProtocolHelper(new Mock<ITcpTunnel>().Object);

            // act
            var writeConnectionOkTask = httpProtocolHelper.WriteConnectionOkAsync(pipeWriter);

            // assert
            await Should.ThrowAsync<InvalidOperationException>(async () => await writeConnectionOkTask);
        }

        [Fact]
        public async Task WriteConnectionOkAsync_CancellationRequested_ShouldThrowOperationCanceledException()
        {
            // arrange
            var msForWrite = new SlowMemoryStream();
            var pipeWriter = PipeWriter.Create(msForWrite);
            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            var httpProtocolHelper = new HttpProtocolHelper(new Mock<ITcpTunnel>().Object);

            // act
            var writeConnectionOkTask = httpProtocolHelper.WriteConnectionOkAsync(pipeWriter, cts.Token);

            // assert
            await Should.ThrowAsync<OperationCanceledException>(async () => await writeConnectionOkTask);

            // clean
            cts.Dispose();
        }
        
        [Fact]
        public async Task WriteBadRequestAsync_ProperWriter_ShouldOk()
        {
            // arrange
            var msForWrite = new MemoryStream();
            var pipeWriter = PipeWriter.Create(msForWrite);
            var streamReader = new StreamReader(msForWrite);
            var httpProtocolHelper = new HttpProtocolHelper(new Mock<ITcpTunnel>().Object);

            // act
            await httpProtocolHelper.WriteBadRequestAsync(pipeWriter);

            // assert
            msForWrite.Position = 0;
            (await streamReader.ReadLineAsync()).ShouldBe("HTTP/1.1 400 Bad Request");
            (await streamReader.ReadLineAsync()).ShouldBe("Connection: close");
            (await streamReader.ReadLineAsync()).ShouldBe("");
        }
        
        [Fact]
        public async Task WriteBadRequestAsync_BadWriter_ShouldThrowInvalidOperation()
        {
            // arrange
            var msForWrite = new MemoryStream();
            var pipeWriter = PipeWriter.Create(msForWrite);
            await pipeWriter.CompleteAsync();
            var httpProtocolHelper = new HttpProtocolHelper(new Mock<ITcpTunnel>().Object);

            // act
            var writeBadRequestTask = httpProtocolHelper.WriteBadRequestAsync(pipeWriter);

            // assert
            await Should.ThrowAsync<InvalidOperationException>(async () => await writeBadRequestTask);
        }

        [Fact]
        public async Task WriteBadRequestAsync_CancellationRequested_ShouldThrowOperationCanceledException()
        {
            // arrange
            var msForWrite = new SlowMemoryStream();
            var pipeWriter = PipeWriter.Create(msForWrite);
            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            var httpProtocolHelper = new HttpProtocolHelper(new Mock<ITcpTunnel>().Object);

            // act
            var writeConnectionOkTask = httpProtocolHelper.WriteBadRequestAsync(pipeWriter, cts.Token);

            // assert
            await Should.ThrowAsync<OperationCanceledException>(async () => await writeConnectionOkTask);

            // clean
            cts.Dispose();
        }
        
        [Fact]
        public async Task WriteBadGatewayAsync_ProperWriter_ShouldOk()
        {
            // arrange
            var msForWrite = new MemoryStream();
            var pipeWriter = PipeWriter.Create(msForWrite);
            var streamReader = new StreamReader(msForWrite);
            var httpProtocolHelper = new HttpProtocolHelper(new Mock<ITcpTunnel>().Object);

            // act
            await httpProtocolHelper.WriteBadGatewayAsync(pipeWriter);

            // assert
            msForWrite.Position = 0;
            (await streamReader.ReadLineAsync()).ShouldBe("HTTP/1.1 502 Bad Gateway");
            (await streamReader.ReadLineAsync()).ShouldBe("Connection: close");
            (await streamReader.ReadLineAsync()).ShouldBe("");
        }
        
        [Fact]
        public async Task WriteBadGatewayAsync_BadWriter_ShouldThrowInvalidOperation()
        {
            // arrange
            var msForWrite = new MemoryStream();
            var pipeWriter = PipeWriter.Create(msForWrite);
            await pipeWriter.CompleteAsync();
            var httpProtocolHelper = new HttpProtocolHelper(new Mock<ITcpTunnel>().Object);

            // act
            var writeBadGatewayTask = httpProtocolHelper.WriteBadGatewayAsync(pipeWriter);

            // assert
            await Should.ThrowAsync<InvalidOperationException>(async () => await writeBadGatewayTask);
        }

        [Fact]
        public async Task WriteBadGatewayAsync_CancellationRequested_ShouldThrowOperationCanceledException()
        {
            // arrange
            var msForWrite = new SlowMemoryStream();
            var pipeWriter = PipeWriter.Create(msForWrite);
            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            var httpProtocolHelper = new HttpProtocolHelper(new Mock<ITcpTunnel>().Object);

            // act
            var writeConnectionOkTask = httpProtocolHelper.WriteBadGatewayAsync(pipeWriter, cts.Token);

            // assert
            await Should.ThrowAsync<OperationCanceledException>(async () => await writeConnectionOkTask);

            // clean
            cts.Dispose();
        }

        #endregion

        #region [Start tcp tunnel Tests]
 
        [Theory]
        [TextData("TestData/SimpleTestTextLF.txt")]
        [TextData("TestData/SimpleTestTextCRLF.txt")]
        public async Task StartTcpTunnelAsync_ProperInput_ShouldOk(string testText)
        {
            // arrange
            var msWithText1 = new MemoryStream(Encoding.ASCII.GetBytes(testText));
            var pipeReader1 = PipeReader.Create(msWithText1);
            var msForWrite1 = new MemoryStream();
            var pipeWriter1 = PipeWriter.Create(msForWrite1);
            var msWithText2 = new MemoryStream(Encoding.ASCII.GetBytes(testText));
            var pipeReader2 = PipeReader.Create(msWithText2);
            var msForWrite2 = new MemoryStream();
            var pipeWriter2 = PipeWriter.Create(msForWrite2);
            var httpProtocolHelper = new HttpProtocolHelper(new Mock<ITcpTunnel>().Object);
            var duplexPipeLocal = new Mock<IDuplexPipe>();
            duplexPipeLocal.Setup(i => i.Input).Returns(pipeReader1);
            duplexPipeLocal.Setup(i => i.Output).Returns(pipeWriter1);
            var duplexPipeRemote = new Mock<IDuplexPipe>();
            duplexPipeRemote.Setup(i => i.Input).Returns(pipeReader2);
            duplexPipeRemote.Setup(i => i.Output).Returns(pipeWriter2);

            // act
            var startTunnelTask = httpProtocolHelper.StartTcpTunnelAsync(duplexPipeLocal.Object, duplexPipeRemote.Object);

            // assert
            await Should.NotThrowAsync(async () => await startTunnelTask);
        }

        #endregion
    }
}