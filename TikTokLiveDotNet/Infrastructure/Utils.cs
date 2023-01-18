using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web;

namespace TikTokLiveDotNet.Infrastructure
{
    internal static class Utils
    {
        private static byte[] GZipHeaderBytes = { 0x1f, 0x8b, 8 };

        public static string? BuildQueryString(this IEnumerable<KeyValuePair<string, string>> queryParams)
        {
            var queryString = HttpUtility.ParseQueryString(string.Empty);

            foreach (var kvp in queryParams)
            {
                queryString.Add(kvp.Key, kvp.Value);
            }

            return queryString.ToString();
        }

        public static string ValidateAndNormalizeUniqueStreamerId(string uniqueStreamerId)
        {
            if(string.IsNullOrWhiteSpace(uniqueStreamerId))
            {
                throw new ArgumentException(
                    nameof(uniqueStreamerId), 
                    "Missing or invalid value for 'uniqueId'. Please provide the username from TikTok URL.");
            }

            uniqueStreamerId = uniqueStreamerId.Replace(Constants.TikTokWebUrl, string.Empty);
            uniqueStreamerId = uniqueStreamerId.Replace("/live", string.Empty);
            uniqueStreamerId = uniqueStreamerId.Replace("@", string.Empty);
            uniqueStreamerId = uniqueStreamerId.Trim();

            return uniqueStreamerId;
        }

        public static string GetRoomIdFromStreamPageHtml(string streamPageHtml)
        {
            var matchMeta = Regex.Match(streamPageHtml, "room_id=([0-9]*)");
            if(matchMeta.Success)
            {
                return matchMeta.Groups[1].Value;
            }

            var matchJson = Regex.Match(streamPageHtml, "\"roomId\":\"([0-9]*)\"");
            if (matchJson.Success)
            {
                return matchMeta.Groups[1].Value;
            }

            var isValidResponse = streamPageHtml.Contains("\"og:url\"");
            throw new Exception(isValidResponse ? "User might be offline." : "Your IP or country might be blocked by TikTok.");
        }

        public static IEnumerable<Cookie> GetAllCookies(this CookieContainer cookieContainer)
        {
            var table = (Hashtable) cookieContainer.GetType().InvokeMember("m_domainTable",
                                                                            BindingFlags.NonPublic |
                                                                            BindingFlags.GetField |
                                                                            BindingFlags.Instance,
                                                                            null,
                                                                            cookieContainer,
                                                                            new object[] { });

            foreach (var key in table.Keys)
            {
                var domain = key as string;
                if (domain == null)
                {
                    continue;
                }

                if (domain.StartsWith("."))
                {
                    domain = domain.Substring(1);
                }

                var httpAddress = string.Format("http://{0}/", domain);
                var httpsAddress = string.Format("https://{0}/", domain);

                if (Uri.TryCreate(httpAddress, UriKind.RelativeOrAbsolute, out var httpUri))
                {
                    foreach (Cookie cookie in cookieContainer.GetCookies(httpUri))
                    {
                        yield return cookie;
                    }
                }

                if (Uri.TryCreate(httpsAddress, UriKind.RelativeOrAbsolute, out var httpsUri))
                {
                    foreach (Cookie cookie in cookieContainer.GetCookies(httpsUri))
                    {
                        yield return cookie;
                    }
                }
            }
        }

        public static bool IsPossiblyGZipped(this Stream stream)
        {
            if (stream.Length <= GZipHeaderBytes.Length)
            {
                return false;
            }

            var header = new byte[GZipHeaderBytes.Length];

            stream.Read(header, 0, GZipHeaderBytes.Length);
            stream.Seek(0, SeekOrigin.Begin);

            return header.SequenceEqual(GZipHeaderBytes);
        }
    }
}
