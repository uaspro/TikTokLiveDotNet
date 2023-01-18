using System.Collections.Generic;
using System.Text.Json;

namespace TikTokLiveDotNet.Infrastructure.Client.Http.Models
{
    internal class GiftsListResponse : WebcastResponse<GiftsList>
    {
    }

    public class GiftsList
    {
        public IEnumerable<JsonElement>? Gifts { get; init; }
    }
}
