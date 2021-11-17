using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ConsoleManualTest
{
    class Program
    {
        static async Task Main()
        {
            var httpSite = "http://www.columbia.edu/~fdc/sample.html";
            var httpsSite = "https://www.google.com";

            // Local
            var proxy = new WebProxy
            {
                Address = new Uri($"http://127.0.0.1:10800"),
                BypassProxyOnLocal = false,
                UseDefaultCredentials = false
            };
       
            var httpClientHandler = new HttpClientHandler
            {
                Proxy = proxy,
            };
            
            var httpClient = new HttpClient(httpClientHandler);

            try
            {
                var responseMessage = await httpClient.GetAsync(httpsSite);
                var stringResult = await responseMessage.Content.ReadAsStringAsync();
                Console.WriteLine(responseMessage.StatusCode.ToString());
                Console.WriteLine(stringResult);
            }
            catch (IOException ioe)
            {
                Console.WriteLine(ioe.Message);
                if (ioe.InnerException != null) Console.WriteLine(ioe.InnerException.Message);
                if (ioe.InnerException is {InnerException: { }}) Console.WriteLine(ioe.InnerException.InnerException.Message);
            }
            catch (SocketException se)
            {
                Console.WriteLine(se.Message);
                if (se.InnerException != null) Console.WriteLine(se.InnerException.Message);
                if (se.InnerException is {InnerException: { }}) Console.WriteLine(se.InnerException.InnerException.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                if (e.InnerException != null) Console.WriteLine(e.InnerException.Message);
                if (e.InnerException is {InnerException: { }}) Console.WriteLine(e.InnerException.InnerException.Message);
            }
            finally
            {
                httpClient.Dispose();
            }
        }
    }
}