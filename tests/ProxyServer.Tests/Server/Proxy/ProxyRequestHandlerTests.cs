using System;
using System.IO;
using System.IO.Pipelines;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ProxyServer.Http;
using ProxyServer.Http.Request;
using ProxyServer.Server.Client;
using ProxyServer.Server.Proxy;
using ProxyServer.Sockets;
using ProxyServer.Utils;
using Shouldly;
using Xunit;

namespace ProxyServer.Tests.Server.Proxy
{
    public class ProxyRequestHandlerTests
    {
        [Theory]
        [InlineData("GET", "www.columbia.edu/~fdc/sample.html", 80)]
        public async Task ProcessProxyRequest_HttpRequest_ShouldPeekAndStartTunnel(string method, string httpUrl,
            int port)
        {
            // arrange
            var msWithText1 = new MemoryStream();
            var pipeReader1 = PipeReader.Create(msWithText1);

            var duplexPipeLocal = new Mock<IDuplexPipe>();
            duplexPipeLocal.Setup(i => i.Input).Returns(pipeReader1);

            var localSocketConnection = new Mock<ISocketConnection>();
            localSocketConnection.Setup(i => i.GetTransportPipe())
                .Returns(() => duplexPipeLocal.Object);
            localSocketConnection.Setup(i => i.Connected)
                .Returns(true);

            var remoteSocketConnection = new Mock<ISocketConnection>();
            remoteSocketConnection.Setup(i => i.ConnectAsync(It.IsAny<EndPoint>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);

            var clientHandlerMock = new Mock<IClientHandler>();
            var requestContext = new HttpRequestContext();
            clientHandlerMock.Setup(i => i.GetRequestContext()).Returns(() => requestContext);
            clientHandlerMock.Setup(i => i.Client).Returns(localSocketConnection.Object);
            clientHandlerMock.Setup(i => i.Remote).Returns(remoteSocketConnection.Object);

            var httpProtocolHelperMock = new Mock<IHttpProtocolHelper>();
            Expression<Func<IHttpProtocolHelper, Task<HttpRequestHeaders>>> peekRequestHeaderAsyncExpression =
                m => m.PeekRequestHeaderAsync(It.Is<PipeReader>(x => x.GetHashCode() == pipeReader1.GetHashCode()),
                    It.IsAny<CancellationToken>());
            httpProtocolHelperMock.Setup(peekRequestHeaderAsyncExpression)
                .Returns(() => Task.FromResult(new HttpRequestHeaders()
                {
                    HttpMethod = new HttpMethod(method),
                    RequestEndPoint =
                        new EndPointContainer(new DnsEndPoint(httpUrl, port), AddressFamily.InterNetwork)
                }));

            var proxyRequestHandler =
                new ProxyRequestHandler(httpProtocolHelperMock.Object, NullLogger<ProxyRequestHandler>.Instance);

            // act
            await proxyRequestHandler.ProcessProxyRequestAsync(clientHandlerMock.Object);

            // assert
            httpProtocolHelperMock.Verify(peekRequestHeaderAsyncExpression, Times.Once);
            httpProtocolHelperMock.Verify(m =>
                m.StartTcpTunnelAsync(It.IsAny<IDuplexPipe>(), It.IsAny<IDuplexPipe>(),
                    It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData("CONNECT", "www.google.coml", 443)]
        public async Task ProcessProxyRequest_HttpsRequest_ShouldPeekSkipToEndReturnOkAndStartTunnel(string method,
            string httpUrl, int port)
        {
            // arrange
            var msWithText1 = new MemoryStream();
            var pipeReader1 = PipeReader.Create(msWithText1);
            var msForWrite1 = new MemoryStream();
            var pipeWriter1 = PipeWriter.Create(msForWrite1);

            var duplexPipeLocal = new Mock<IDuplexPipe>();
            duplexPipeLocal.Setup(i => i.Input).Returns(pipeReader1);
            duplexPipeLocal.Setup(i => i.Output).Returns(pipeWriter1);
            var localSocketConnection = new Mock<ISocketConnection>();
            localSocketConnection.Setup(i => i.GetTransportPipe())
                .Returns(() => duplexPipeLocal.Object);
            localSocketConnection.Setup(i => i.Connected)
                .Returns(true);

            var remoteSocketConnection = new Mock<ISocketConnection>();
            remoteSocketConnection.Setup(i => i.ConnectAsync(It.IsAny<EndPoint>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);

            var clientHandlerMock = new Mock<IClientHandler>();
            var requestContext = new HttpRequestContext();
            clientHandlerMock.Setup(i => i.GetRequestContext()).Returns(() => requestContext);
            clientHandlerMock.Setup(i => i.Client).Returns(localSocketConnection.Object);
            clientHandlerMock.Setup(i => i.Remote).Returns(remoteSocketConnection.Object);

            var httpProtocolHelperMock = new Mock<IHttpProtocolHelper>();

            Expression<Func<IHttpProtocolHelper, Task<HttpRequestHeaders>>> peekRequestHeaderAsyncExpression =
                m => m.PeekRequestHeaderAsync(It.Is<PipeReader>(x => x.GetHashCode() == pipeReader1.GetHashCode()),
                    It.IsAny<CancellationToken>());
            httpProtocolHelperMock.Setup(peekRequestHeaderAsyncExpression)
                .Returns(() => Task.FromResult(new HttpRequestHeaders()
                {
                    HttpMethod = new HttpMethod(method),
                    RequestEndPoint =
                        new EndPointContainer(new DnsEndPoint(httpUrl, port), AddressFamily.InterNetwork)
                }));

            var proxyRequestHandler =
                new ProxyRequestHandler(httpProtocolHelperMock.Object, NullLogger<ProxyRequestHandler>.Instance);

            // act
            await proxyRequestHandler.ProcessProxyRequestAsync(clientHandlerMock.Object);

            // assert
            httpProtocolHelperMock.Verify(peekRequestHeaderAsyncExpression, Times.Once);
            httpProtocolHelperMock.Verify(m =>
                m.SkipToEndAsync(It.Is<PipeReader>(x => x.GetHashCode() == pipeReader1.GetHashCode()),
                    It.IsAny<CancellationToken>()), Times.Once);
            httpProtocolHelperMock.Verify(m =>
                m.WriteConnectionOkAsync(It.Is<PipeWriter>(x => x.GetHashCode() == pipeWriter1.GetHashCode()),
                    It.IsAny<CancellationToken>()), Times.Once);
            httpProtocolHelperMock.Verify(m =>
                m.StartTcpTunnelAsync(It.IsAny<IDuplexPipe>(), It.IsAny<IDuplexPipe>(),
                    It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ProcessProxyRequest_NullReferences_ShouldNotThrowJustLog()
        {
            // arrange
            var clientHandlerMock = new Mock<IClientHandler>();
            var httpProtocolHelperMock = new Mock<IHttpProtocolHelper>();
            var loggerMock = new Mock<ILogger<ProxyRequestHandler>>();
            var proxyRequestHandler =
                new ProxyRequestHandler(httpProtocolHelperMock.Object, loggerMock.Object);

            // act
            var processProxyRequestTask = proxyRequestHandler.ProcessProxyRequestAsync(clientHandlerMock.Object);

            // assert
            await Should.NotThrowAsync(async () => await processProxyRequestTask);
            loggerMock.Verify(
                m =>
                    m.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(),
                        It.IsAny<Func<It.IsAnyType, Exception, string>>()
                    ), Times.Once);
        }
    }
}