namespace TikTokLiveDotNet.Infrastructure.Client.Http.Models
{
    internal class RoomInfoResponse : WebcastResponse<RoomInfo>
    {
    }

    public class RoomInfo
    {
        public enum RoomStatus
        {
            Live = 2,
            StreamEnded = 4
        }

        public long Id { get; init; }

        public RoomStatus Status { get; init; }
    }
}
