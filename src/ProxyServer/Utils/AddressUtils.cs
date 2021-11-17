using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace ProxyServer.Utils
{
    internal static class AddressUtils
    {
        private const string HttpScheme = "http://";
        private const string HttpsScheme = "https://";
        
        /// <summary>
        /// Ugly and slow method parses EndPoint
        /// </summary>
        /// <param name="url">Input url to parse</param>
        /// <param name="endPointContainer">Output container with EndPoint and AddressFamily</param>
        /// <returns>Is success parsed</returns>
        public static bool TryParseEndPoint(string url, out EndPointContainer endPointContainer)
        {
            endPointContainer = default;

            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            if (TryParseIpEndPoint(url, out var ipAddress, out var port))
            {
                endPointContainer = new EndPointContainer(new IPEndPoint(ipAddress, port), ipAddress.AddressFamily);
                return true;
            }

            if (TryParseUri(url, UriKind.Absolute, out var uri))
            {
                endPointContainer =
                    new EndPointContainer(new DnsEndPoint(uri.Host, uri.Port, AddressFamily.InterNetwork),
                        AddressFamily.InterNetwork);
                return true;
            }

            return false;
        }

        private static bool TryParseIpEndPoint(string endPoint, out IPAddress ip, out int port)
        {
            endPoint = endPoint.Replace(HttpScheme, string.Empty)
                .Replace(HttpsScheme, string.Empty);

            ip = default;
            port = 0;

            endPoint = (endPoint.Split('/'))[0];

            var ep = endPoint.Split(':');
            
            switch (ep.Length)
            {
                case < 2:
                    return false;
                case > 2:
                {
                    if (!IPAddress.TryParse(string.Join(":", ep, 0, ep.Length - 1), out ip))
                    {
                        return false;
                    }

                    break;
                }
                default:
                {
                    if (!IPAddress.TryParse(ep[0], out ip))
                    {
                        return false;
                    }

                    break;
                }
            }

            return int.TryParse(ep[^1], NumberStyles.None, NumberFormatInfo.CurrentInfo, out port);
        }

        private static bool TryParseUri(string url, UriKind uriKind, out Uri uri)
        {
            uri = default;

            if (string.IsNullOrWhiteSpace(url))
                return false;

            var r = new Regex(@":(?<port>[0-9]+)",
                RegexOptions.None, TimeSpan.FromMilliseconds(150));
            var m = r.Match(url);

            if (!m.Success)
                return Uri.TryCreate(url, uriKind, out uri);

            if (!int.TryParse(m.Groups["port"].Value, out var parsedPort))
                return Uri.TryCreate(url, uriKind, out uri);

            if (url.StartsWith(HttpsScheme))
                return Uri.TryCreate(url, uriKind, out uri);

            url = url.Replace($":{parsedPort.ToString()}", string.Empty);

            if (parsedPort == 443)
                url = $"{HttpsScheme}{url}";

            return Uri.TryCreate(url, UriKind.Absolute, out uri);
        }
    }

    internal class EndPointContainer
    {
        public EndPointContainer(EndPoint ep, AddressFamily af)
        {
            EndPoint = ep ?? throw new ArgumentNullException(nameof(ep));
            AddressFamily = af;
        }

        public EndPoint EndPoint { get; }
        public AddressFamily AddressFamily { get; }
    }
}
