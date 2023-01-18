using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using TikTokLiveDotNet.Infrastructure.Client.Http.Models;

namespace TikTokLiveDotNet.Infrastructure.Client.Http
{
    internal class TikTokHttpClient : IDisposable
    {
        private const int DefaultTimeoutSeconds = 10;

        private const string UserAgentHeaderName = "User-Agent";

        private bool _disposedValue;

        private readonly CookieContainer _cookieContainer;
        private readonly HttpClient _httpClient;

        internal TikTokHttpClient(string? sessionId, Dictionary<string, string> customHeaders, CookieContainer cookieContainer)
        {
            _cookieContainer = cookieContainer;

            _httpClient = new HttpClient(
                new HttpClientHandler
                {
                    CookieContainer = _cookieContainer,
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                });

            _httpClient.Timeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds);

            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                SetSessionId(sessionId);
            }

            foreach (var header in Constants.DefaultHeaders.Concat(customHeaders))
            {
                _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }

        public async Task<string> GetRoomId(string uniqueStreamerId)
        {
            var response = await _httpClient.GetAsync($"{Constants.TikTokWebUrl}@{uniqueStreamerId}/live");
            var streamPageHtml = await response.Content.ReadAsStringAsync();
            return Utils.GetRoomIdFromStreamPageHtml(streamPageHtml);
        }

        public Task<RoomInfoResponse?> GetRoomInfo(Dictionary<string, string> customQueryParams)
        {
            return GetJsonDeserializedObject<RoomInfoResponse>(Constants.TikTokWebcastUrl, $"room/info/", customQueryParams);
        }

        public Task<GiftsListResponse?> GetGiftsList(Dictionary<string, string> customQueryParams)
        {
            return GetJsonDeserializedObject<GiftsListResponse>(Constants.TikTokWebcastUrl, $"gift/list/", customQueryParams);
        }

        public Task<Protobuf.WebcastResponse> GetRoomData(Dictionary<string, string> customQueryParams, bool shouldBeSigned = false)
        {
            return GetProtoDeserializedObject<Protobuf.WebcastResponse>(Constants.TikTokWebcastUrl, "im/fetch/", customQueryParams, shouldBeSigned);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private async Task<T> GetProtoDeserializedObject<T>(string baseUrl, string path, Dictionary<string, string> queryParams, bool shouldBeSigned = false)
        {
            var requestUrl = await BuildRequestUrl(baseUrl, path, queryParams, shouldBeSigned);
            var response = await _httpClient.GetAsync(requestUrl);

            return Serializer.Deserialize<T>(await response.Content.ReadAsStreamAsync());
        }

        private async Task<T?> GetJsonDeserializedObject<T>(string baseUrl, string path, Dictionary<string, string> queryParams, bool shouldBeSigned = false)
        {
            var requestUrl = await BuildRequestUrl(baseUrl, path, queryParams, shouldBeSigned);
            var response = await _httpClient.GetAsync(requestUrl);

            var responseData = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(responseData, Constants.DefaultJsonSerializerOptions);
        }

        private void SetSessionId(string? sessionId)
        {
            _cookieContainer.Add(new Cookie("sessionid", sessionId, null, Constants.TikTokCookiesDomain));
            _cookieContainer.Add(new Cookie("sessionid_ss", sessionId, null, Constants.TikTokCookiesDomain));
            _cookieContainer.Add(new Cookie("sid_tt", sessionId, null, Constants.TikTokCookiesDomain));
        }

        private async Task<string?> BuildRequestUrl(string baseUrl, string path, Dictionary<string, string> queryParams, bool shouldBeSigned)
        {
            var resultUrl = $"{baseUrl}{path}?{queryParams.BuildQueryString()}";

            if (!shouldBeSigned)
            {
                return resultUrl;
            }

            try
            {
                var signQueryParams = new Dictionary<string, string>
                {
                    { "client", "ttlive-node" },
                    { "uuc", "1" }, // count of parallel active connections, only single connection supported at the moment
                    { "url", resultUrl }
                };

                var signProviderResponse = await _httpClient.GetAsync($"{Constants.TikTokSignatureProviderHost}?{signQueryParams.BuildQueryString()}");
                if (signProviderResponse.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"Sign response code: ${(int)signProviderResponse.StatusCode}");
                }

                var signProviderResponseContent = await signProviderResponse.Content.ReadAsStringAsync();

                var signResponse = JsonSerializer.Deserialize<SignResponse>(signProviderResponseContent, Constants.DefaultJsonSerializerOptions);
                if (string.IsNullOrEmpty(signResponse?.SignedUrl))
                {
                    throw new Exception("Missing 'signedUrl' property");
                }

                var headers = _httpClient.DefaultRequestHeaders;
                if (!string.IsNullOrEmpty(signResponse?.UserAgent))
                {
                    if (headers.Contains(UserAgentHeaderName))
                    {
                        headers.Remove(UserAgentHeaderName);
                    }

                    headers.Add(UserAgentHeaderName, signResponse?.UserAgent);
                }

                if (!string.IsNullOrEmpty(signResponse?.MsToken))
                {
                    _cookieContainer.Add(new Cookie("msToken", signResponse?.MsToken, null, Constants.TikTokCookiesDomain));
                }

                return signResponse?.SignedUrl;
            }
            catch (Exception ex)
            {
                // If a sessionid is present, the signature is optional => Do not throw an error.
                if (_cookieContainer.GetAllCookies().Any(c => c.Name == "sessionid"))
                {
                    return resultUrl;
                }

                throw new Exception($"Failed to issue a signature for the request: {resultUrl} | Error: {ex.Message}");
            }
        }

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _httpClient.Dispose();
                }

                _disposedValue = true;
            }
        }
    }
}
