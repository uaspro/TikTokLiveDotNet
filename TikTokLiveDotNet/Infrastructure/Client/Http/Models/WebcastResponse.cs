namespace TikTokLiveDotNet.Infrastructure.Client.Http.Models
{
    internal abstract class WebcastResponse<T>
    {
        public T? Data { get; init; }
    }
}
