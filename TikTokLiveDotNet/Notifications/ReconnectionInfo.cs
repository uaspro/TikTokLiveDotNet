namespace TikTokLiveDotNet.Notifications
{
    public class ReconnectionInfo : ConnectionInfo
    {
        public bool IsRetry => FailureCause != null;

        private ReconnectionInfo(TikTokLiveClient.ConnectionType connectionType, string? failureCause = null)
            : base(connectionType, failureCause)
        {
        }

        internal static ReconnectionInfo Success(TikTokLiveClient.ConnectionType connectionType) => new ReconnectionInfo(connectionType);

        internal static ReconnectionInfo Failure(TikTokLiveClient.ConnectionType connectionType, string failureCause) => new ReconnectionInfo(connectionType, failureCause);
    }
}
