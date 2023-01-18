using System;
using TikTokLiveDotNet.Protobuf;

namespace TikTokLiveDotNet.Notifications
{
    public class ControlMessageInfo
    {
        private readonly WebcastControlMessage _webcastControlMessage;

        public enum ActionType
        {
            LiveEnded = 3
        }

        public ControlMessageInfo(WebcastControlMessage webcastControlMessage)
        {
            _webcastControlMessage = webcastControlMessage;
        }

        public ActionType Action => (ActionType) Convert.ToInt32(_webcastControlMessage.Action);
    }
}
