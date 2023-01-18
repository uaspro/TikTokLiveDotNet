namespace TikTokLiveDotNet.Notifications
{
    public abstract class ConnectionInfo
    {
        public TikTokLiveClient.ConnectionType ConnectionType { get; private init; }

        public string? FailureCause { get; private init; }

        protected ConnectionInfo(TikTokLiveClient.ConnectionType connectionType, string? failureCause = null)
        {
            ConnectionType = connectionType;
            FailureCause = failureCause;
        }
    }
}
