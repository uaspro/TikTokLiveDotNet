namespace TikTokLiveDotNet.Notifications
{
    public class DisconnectionInfo : ConnectionInfo
    {
        public bool IsFailure => FailureCause != null;

        private DisconnectionInfo(TikTokLiveClient.ConnectionType connectionType, string? failureCause = null)
            : base(connectionType, failureCause)
        {
        }

        internal static DisconnectionInfo Success(TikTokLiveClient.ConnectionType connectionType) => new DisconnectionInfo(connectionType);

        internal static DisconnectionInfo Failure(TikTokLiveClient.ConnectionType connectionType, string failureCause) => new DisconnectionInfo(connectionType, failureCause);
    }
}
