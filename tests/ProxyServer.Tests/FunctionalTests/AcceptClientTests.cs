using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ProxyServer.Tests.Fixtures;
using Shouldly;
using Xunit;

namespace ProxyServer.Tests.FunctionalTests
{
    public class AcceptClientTests
    {
        private readonly NetProxyServerFixture _netProxyServerFixture;

        public AcceptClientTests()
        {
            _netProxyServerFixture = new NetProxyServerFixture();
        }

        [Theory]
        [InlineData(12300, "http://www.columbia.edu/~fdc/sample.html")]
        [InlineData(12300, "https://google.com")]
        public async Task AcceptClient_ProperUrl_ShouldOk(int port, string address)
        {
            // arrange
            var proxyServer = _netProxyServerFixture.GetProxyServer();
            var startAsyncTask = Task.Run(async () => await proxyServer.StartAsync(port));
            WaitServerStart(proxyServer);
            var httpClient = CreateHttpClient(port);

            // act
            var result = await httpClient.GetAsync(address);

            // assert
            result.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await result.Content.ReadAsStringAsync()).ShouldContain("<html");

            // clean
            proxyServer.Stop();
            httpClient.Dispose();
        }

        [Theory]
        [InlineData(12300, "https://gooTYTYTYTYgle.col")]
        public async Task AcceptClient_BadUrl_ShouldReturnBadGateway(int port, string address)
        {
            // arrange
            var proxyServer = _netProxyServerFixture.GetProxyServer();
            var startAsyncTask = Task.Run(async () => await proxyServer.StartAsync(port));
            WaitServerStart(proxyServer);
            var httpClient = CreateHttpClient(port);

            // act
            var result = await httpClient.GetAsync(address);

            // assert
            result.StatusCode.ShouldBe(HttpStatusCode.BadGateway);

            // clean
            proxyServer.Stop();
            httpClient.Dispose();
        }

        [Theory]
        [InlineData(12300, "GET jdjskdskdfh HTTP/1.1")]
        [InlineData(12300, "GET https:www.google.com HTFP/1.1")]
        public async Task AcceptClient_BadUrl_ShouldReturnBadRequest(int port, string requestHeader)
        {
            // arrange
            var proxyServer = _netProxyServerFixture.GetProxyServer();
            var startAsyncTask = Task.Run(async () => await proxyServer.StartAsync(port));
            WaitServerStart(proxyServer);
            var tcpClient = new TcpClient();

            // act
            await tcpClient.ConnectAsync("127.0.0.1", port);
            var ns = tcpClient.GetStream();
            var streamWriter = new StreamWriter(ns);
            await streamWriter.WriteLineAsync(requestHeader);
            await streamWriter.WriteLineAsync(String.Empty);
            await streamWriter.FlushAsync();
            var streamReader = new StreamReader(ns);
            var result = await streamReader.ReadLineAsync();

            // assert
            result.ShouldContain("400 Bad Request");

            // clean
            proxyServer.Stop();
            tcpClient.Dispose();
        }


        private HttpClient CreateHttpClient(int port)
        {
            var proxy = new WebProxy
            {
                Address = new Uri($"http://127.0.0.1:{port}"),
                BypassProxyOnLocal = false,
                UseDefaultCredentials = false
            };

            var httpClientHandler = new HttpClientHandler
            {
                Proxy = proxy,
            };

            var httpClient = new HttpClient(httpClientHandler);

            return httpClient;
        }

        private void WaitServerStart(INetProxyServer netProxyServer)
        {
            var startTime = 0;
            var checkPeriod = 100;
            while (startTime < 5000)
            {
                Thread.Sleep(checkPeriod);
                if (netProxyServer.Active)
                {
                    return;
                }

                startTime += checkPeriod;
            }

            throw new ApplicationException("Server didn't start");
        }
    }
}