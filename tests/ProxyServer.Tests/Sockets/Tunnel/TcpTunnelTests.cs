using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ProxyServer.Sockets.Tunnel;
using ProxyServer.Tests.TestUtils;
using ProxyServer.Tests.TestUtils.StreamTestHelpers;
using Shouldly;
using Xunit;

namespace ProxyServer.Tests.Sockets.Tunnel
{
    public class TcpTunnelTests
    {
        [Theory]
        [TextData2("TestData/SimpleTestTextLF.txt", "TestData/SimpleTestTextCRLF.txt")]
        public async Task StartTunnelAsync_ProperInput_ShouldTransferDataOk(string testText1, string testText2)
        {
            // arrange
            var msWithText1 = new DelayedCompleteMemoryStream(Encoding.ASCII.GetBytes(testText1));
            var pipeReader1 = PipeReader.Create(msWithText1);
            var msForWrite1 = new MemoryStream();
            var pipeWriter1 = PipeWriter.Create(msForWrite1);

            var msWithText2 = new DelayedCompleteMemoryStream(Encoding.ASCII.GetBytes(testText2));
            var pipeReader2 = PipeReader.Create(msWithText2);
            var msForWrite2 = new MemoryStream();
            var pipeWriter2 = PipeWriter.Create(msForWrite2);

            var tunnelOptionsMock = new Mock<ITunnelOptions>();
            tunnelOptionsMock.Setup(i => i.KeepAliveTimeout).Returns(30000);
            var tcpTunnel = new TcpTunnel(tunnelOptionsMock.Object);

            var duplexPipeLocal = new Mock<IDuplexPipe>();
            duplexPipeLocal.Setup(i => i.Input).Returns(pipeReader1);
            duplexPipeLocal.Setup(i => i.Output).Returns(pipeWriter1);

            var duplexPipeRemote = new Mock<IDuplexPipe>();
            duplexPipeRemote.Setup(i => i.Input).Returns(pipeReader2);
            duplexPipeRemote.Setup(i => i.Output).Returns(pipeWriter2);

            // act
            await tcpTunnel.StartTunnelAsync(duplexPipeLocal.Object, duplexPipeRemote.Object);

            // assert
            msWithText1.ToArray().SequenceEqual(msForWrite2.ToArray()).ShouldBeTrue();
            msWithText2.ToArray().SequenceEqual(msForWrite1.ToArray()).ShouldBeTrue();
        }

        [Theory]
        [TextData2("TestData/SimpleTestTextLF.txt", "TestData/SimpleTestTextCRLF.txt")]
        public async Task StartTunnelAsync_SmallKeepAlive_ShouldDoneFast(string testText1, string testText2)
        {
            // arrange
            var waitTimeTolerance = 500;
            
            var msWithText1 = new DelayedCompleteMemoryStream(Encoding.ASCII.GetBytes(testText1));
            var pipeReader1 = PipeReader.Create(msWithText1);
            var msForWrite1 = new MemoryStream();
            var pipeWriter1 = PipeWriter.Create(msForWrite1);

            var msWithText2 = new DelayedCompleteMemoryStream(Encoding.ASCII.GetBytes(testText2));
            var pipeReader2 = PipeReader.Create(msWithText2);
            var msForWrite2 = new MemoryStream();
            var pipeWriter2 = PipeWriter.Create(msForWrite2);

            var tunnelOptionsMock = new Mock<ITunnelOptions>();
            tunnelOptionsMock.Setup(i => i.KeepAliveTimeout).Returns(1);
            var tcpTunnel = new TcpTunnel(tunnelOptionsMock.Object);

            var duplexPipeLocal = new Mock<IDuplexPipe>();
            duplexPipeLocal.Setup(i => i.Input).Returns(pipeReader1);
            duplexPipeLocal.Setup(i => i.Output).Returns(pipeWriter1);

            var duplexPipeRemote = new Mock<IDuplexPipe>();
            duplexPipeRemote.Setup(i => i.Input).Returns(pipeReader2);
            duplexPipeRemote.Setup(i => i.Output).Returns(pipeWriter2);

            var sw = new Stopwatch();
            sw.Start();

            // act            
            await tcpTunnel.StartTunnelAsync(duplexPipeLocal.Object, duplexPipeRemote.Object);

            // assert
            sw.Stop();
            // DelayedCompleteMemoryStream read time 100 to 1000
            sw.ElapsedMilliseconds.ShouldBeLessThan(waitTimeTolerance);
        }

        [Theory]
        [InlineData(1000)]
        [InlineData(3000)]
        [InlineData(5000)]
        public async Task StartTunnelAsync_KeepAliveNoTransferRefreshTimeout_ShouldDoneNearToKeepAliveTime(int keepAlive)
        {
            // arrange
            var waitTimePlusTolerance = keepAlive+500;
            var waitTimeMinusTolerance = keepAlive-500;
            
            var msWithText1 = new InfiniteMemoryStream(1);
            var pipeReader1 = PipeReader.Create(msWithText1);
            var msForWrite1 = new MemoryStream();
            var pipeWriter1 = PipeWriter.Create(msForWrite1);

            var msWithText2 = new InfiniteMemoryStream(1);
            var pipeReader2 = PipeReader.Create(msWithText2);
            var msForWrite2 = new MemoryStream();
            var pipeWriter2 = PipeWriter.Create(msForWrite2);

            var tunnelOptionsMock = new Mock<ITunnelOptions>();
            tunnelOptionsMock.Setup(i => i.KeepAliveTimeout).Returns(keepAlive);
            var tcpTunnel = new TcpTunnel(tunnelOptionsMock.Object);

            var duplexPipeLocal = new Mock<IDuplexPipe>();
            duplexPipeLocal.Setup(i => i.Input).Returns(pipeReader1);
            duplexPipeLocal.Setup(i => i.Output).Returns(pipeWriter1);

            var duplexPipeRemote = new Mock<IDuplexPipe>();
            duplexPipeRemote.Setup(i => i.Input).Returns(pipeReader2);
            duplexPipeRemote.Setup(i => i.Output).Returns(pipeWriter2);

            var sw = new Stopwatch();
            sw.Start();

            // act            
            await tcpTunnel.StartTunnelAsync(duplexPipeLocal.Object, duplexPipeRemote.Object);

            // assert
            sw.Stop();
            sw.ElapsedMilliseconds.ShouldBeLessThan(waitTimePlusTolerance);
            sw.ElapsedMilliseconds.ShouldBeGreaterThan(waitTimeMinusTolerance);
        }
        
        [Theory]
        [InlineData(300)]
        [InlineData(500)]
        [InlineData(1000)]
        public async Task StartTunnelAsync_KeepAliveTransferRefreshTimeout_ShouldWorkMoreThanKeepAlive(int keepAlive)
        {
            // arrange
            var readWriteTimeMs = 100;
            var readCount = 50;
            var waitTime = keepAlive + readWriteTimeMs*readCount;
            var waitTimePlusTolerance = waitTime + readWriteTimeMs + keepAlive + 500;
            var waitTimeMinusTolerance = waitTime - readWriteTimeMs - keepAlive - 500;
            
            var msWithText1 = new InfiniteMemoryStream(readCount);
            msWithText1.IOTimeMs = readWriteTimeMs;
            var pipeReader1 = PipeReader.Create(msWithText1);
            var msForWrite1 = new MemoryStream();
            var pipeWriter1 = PipeWriter.Create(msForWrite1);

            var msWithText2 = new InfiniteMemoryStream(1);
            var pipeReader2 = PipeReader.Create(msWithText2);
            var msForWrite2 = new MemoryStream();
            var pipeWriter2 = PipeWriter.Create(msForWrite2);

            var tunnelOptionsMock = new Mock<ITunnelOptions>();
            tunnelOptionsMock.Setup(i => i.KeepAliveTimeout).Returns(keepAlive);
            var tcpTunnel = new TcpTunnel(tunnelOptionsMock.Object);

            var duplexPipeLocal = new Mock<IDuplexPipe>();
            duplexPipeLocal.Setup(i => i.Input).Returns(pipeReader1);
            duplexPipeLocal.Setup(i => i.Output).Returns(pipeWriter1);

            var duplexPipeRemote = new Mock<IDuplexPipe>();
            duplexPipeRemote.Setup(i => i.Input).Returns(pipeReader2);
            duplexPipeRemote.Setup(i => i.Output).Returns(pipeWriter2);

            var sw = new Stopwatch();
            sw.Start();

            // act            
            await tcpTunnel.StartTunnelAsync(duplexPipeLocal.Object, duplexPipeRemote.Object);

            // assert
            sw.Stop();
            sw.ElapsedMilliseconds.ShouldBeLessThan(waitTimePlusTolerance);
            sw.ElapsedMilliseconds.ShouldBeGreaterThan(waitTimeMinusTolerance);
        }
    }
}