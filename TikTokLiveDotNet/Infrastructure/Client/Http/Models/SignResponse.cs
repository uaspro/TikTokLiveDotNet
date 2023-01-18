using System.Text.Json.Serialization;

namespace TikTokLiveDotNet.Infrastructure.Client.Http.Models
{
    internal class SignResponse
    {
        public string? SignedUrl { get; init; }

        [JsonPropertyName("User-Agent")]
        public string? UserAgent { get; init; }

        public string? MsToken { get; init; }
    }
}
