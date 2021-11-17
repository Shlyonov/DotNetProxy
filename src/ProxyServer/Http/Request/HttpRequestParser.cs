using System;
using System.Net.Http;
using System.Net.Sockets;
using ProxyServer.Exceptions;
using ProxyServer.Utils;

namespace ProxyServer.Http.Request
{
    internal static class HttpRequestParser
    {
        public static HttpRequestHeaders ParseRequestHeader(string requestHeaderStr)
        {
            if (requestHeaderStr == null) throw new ArgumentNullException(nameof(requestHeaderStr));
            
            var requestHeaderArgs = requestHeaderStr.Split(" ");

            if (requestHeaderArgs.Length < 3)
            {
                throw new BadRequestException($"Bad request headers: {requestHeaderStr}!");
            }

            if (string.IsNullOrWhiteSpace(requestHeaderArgs[0]))
            {
                throw new BadRequestException($"Bad http method: {requestHeaderArgs[0]}");
            }

            var httpMethod = new HttpMethod(requestHeaderArgs[0]);

            if (string.IsNullOrWhiteSpace(requestHeaderArgs[1]))
            {
                throw new BadRequestException($"Bad request url: {requestHeaderArgs[1]}");
            }

            var requestString = requestHeaderArgs[1];

            if (!AddressUtils.TryParseEndPoint(requestString, out var requestAddress))
            {
                throw new BadRequestException($"Bad url: {requestString}");
            }

            if (requestAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new BadRequestException($"Not ipv4: {requestAddress.AddressFamily}");
            }

            var protocol = requestHeaderArgs[2];

            return new HttpRequestHeaders
            {
                HttpMethod = httpMethod,
                RequestUrl = requestString,
                RequestEndPoint = requestAddress,
                Protocol = protocol
            };
        }
    }
}