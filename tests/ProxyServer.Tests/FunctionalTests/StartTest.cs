using System.Threading;
using System.Threading.Tasks;
using ProxyServer.Tests.Fixtures;
using Shouldly;
using Xunit;

namespace ProxyServer.Tests.FunctionalTests
{
    public class StartTest
    {
        private readonly NetProxyServerFixture _netProxyServerFixture;
        
        public StartTest()
        {
            _netProxyServerFixture = new NetProxyServerFixture();
        }
        
        [Theory]
        [InlineData(12300, 100, 5000)]
        public async Task Start_NoOptions_ShouldOk(int port, int checkPeriodMs, int maxStartTimeMs)
        {
            bool WaitServerStart(INetProxyServer netProxyServer)
            {
                var startTime = 0;
                while (startTime < maxStartTimeMs)
                {
                    Thread.Sleep(checkPeriodMs);
                    if (netProxyServer.Active)
                    {
                        return true;
                    }

                    startTime += checkPeriodMs;
                }

                return false;
            }

            // arrange
            var proxyServer = _netProxyServerFixture.GetProxyServer();

            // act
            var startAsyncTask = Task.Run(async ()=> await proxyServer.StartAsync(port));
            var isServerStarted = WaitServerStart(proxyServer);
            
            // assert
            isServerStarted.ShouldBeTrue();
            
            // clean
            proxyServer.Stop();
        }
        
        [Theory]
        [InlineData(12300, 100, 5000)]
        public async Task Start_SomeOptions_ShouldOk(int port, int checkPeriodMs, int maxStartTimeMs)
        {
            bool WaitServerStart(INetProxyServer netProxyServer)
            {
                var startTime = 0;
                while (startTime < maxStartTimeMs)
                {
                    Thread.Sleep(checkPeriodMs);
                    if (netProxyServer.Active)
                    {
                        return true;
                    }

                    startTime += checkPeriodMs;
                }

                return false;
            }

            // arrange
            var proxyServer = _netProxyServerFixture.GetProxyServer(o =>
            {
                o.Backlog = 5;
                o.ConnectionLimit = 100;
                o.ConnectTimeout = 100000;
                o.ReceiveTimeout = 100000;
                o.SendTimeout = 100000;
                o.KeepAliveTimeout = 100000;
            });
            
            // act
            var startAsyncTask = Task.Run(async ()=> await proxyServer.StartAsync(port));
            var isServerStarted = WaitServerStart(proxyServer);
            
            // assert
            isServerStarted.ShouldBeTrue();
            
            // clean
            proxyServer.Stop();
        }
    }
}