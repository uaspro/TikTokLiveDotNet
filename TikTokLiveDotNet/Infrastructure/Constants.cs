﻿using System.Collections.Generic;
using System.Text.Json;

namespace TikTokLiveDotNet.Infrastructure
{
    internal static class Constants
    {
        public static readonly JsonSerializerOptions DefaultJsonSerializerOptions = new(JsonSerializerDefaults.Web);

        public const string TikTokBaseUrl = "https://www.tiktok.com";

        public const string TikTokWebUrl = TikTokBaseUrl + "/";
        public const string TikTokWebcastUrl = "https://webcast.tiktok.com/webcast/";

        public const string TikTokSignatureProviderHost = "https://tiktok.eulerstream.com/webcast/sign_url";

        public const string TikTokCookiesDomain = ".tiktok.com";

        public static readonly IReadOnlyDictionary<string, string> DefaultQueryParams = new Dictionary<string, string>
        {
            { "cursor", string.Empty },
            { "internal_ext", string.Empty },
            { "aid", "1988" },
            { "app_language", "en-US" },
            { "app_name", "tiktok_web" },
            { "browser_language", "en" },
            { "browser_name", "Mozilla" },
            { "browser_online", "true" },
            { "browser_platform", "Win32" },
            { "browser_version", "5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/106.0.0.0 Safari/537.36" },
            { "cookie_enabled", "true" },
            { "device_platform", "web" },
            { "focus_state", "true" },
            { "from_page", "user" },
            { "history_len", "4" },
            { "is_fullscreen", "false" },
            { "is_page_visible", "true" },
            { "did_rule", "3" },
            { "fetch_rule", "1" },
            { "last_rtt", "0" },
            { "live_id", "12" },
            { "resp_content_type", "protobuf" },
            { "screen_height", "1152" },
            { "screen_width", "2048" },
            { "tz_name", "Europe/Berlin" },
            { "referer", TikTokWebUrl },
            { "root_referer", TikTokWebUrl },
            { "version_code", "180800" },
            { "webcast_sdk_version", "1.3.0" },
            { "update_version_code", "1.3.0" }
        };

        public static readonly IReadOnlyDictionary<string, string> DefaultHeaders = new Dictionary<string, string>
        {
            { "Connection", "keep-alive" },
            { "Cache-Control", "max-age=0" },
            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/106.0.0.0 Safari/537.36" },
            { "Accept", "text/html,application/json,application/protobuf" },
            { "Referer", TikTokWebUrl },
            { "Origin", TikTokBaseUrl },
            { "Accept-Language", "en-US,en;q=0.9" },
            { "Accept-Encoding", "gzip, deflate" }
        };
    }
}
