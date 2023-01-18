using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TikTokLiveDotNet.Infrastructure;
using TikTokLiveDotNet.Protobuf;
using Websocket.Client;

namespace TikTokLiveDotNet
{
    internal class WebcastWebsocketClient : IDisposable
    {
        private const int DefaultTimeoutSeconds = 30;
        private const int DefaultKeepAliveIntervalSeconds = 15;
        private const int DefaultPingIntervalSeconds = 10;

        private const string EchoProtocolName = "echo-protocol";
        private const string CookieHeaderName = "Cookie";
        private const string DefaultWebcastMessageType = "msg";

        private const string CompressionQueryParamName = "compress";
        private const string CompressionQueryParamValue = "gzip";

        private static readonly byte[] PingBytes = new byte[] { 58, 2, 104, 98 };

        private bool _disposedValue;

        private readonly bool _isCompressionEnabled;
        private readonly WebsocketClient _websocketClient;

        private readonly CancellationTokenSource _pingTaskCancellationTokenSource = new();

        private readonly Subject<WebcastResponse> _webcastResponseReceivedSubject = new();
        private readonly Subject<DisconnectionInfo> _disconnectionHappenedSubject = new();

        public WebcastWebsocketClient(
            string websocketUrl, 
            Dictionary<string, string> queryParams, 
            Dictionary<string, string> webSocketParams, 
            CookieContainer cookieContainer,
            bool isCompressionEnabled)
        {
            if(isCompressionEnabled)
            {
                webSocketParams.Add(CompressionQueryParamName, CompressionQueryParamValue);
            }

            var websocketUrlWithParams = $"{websocketUrl}?{queryParams.Concat(webSocketParams).BuildQueryString()}";
            var websocketUri = new Uri(websocketUrlWithParams);
            var websocketClientFactory = new Func<ClientWebSocket>(() => 
            {
                var client = new ClientWebSocket();

                client.Options.KeepAliveInterval = TimeSpan.FromSeconds(DefaultKeepAliveIntervalSeconds);
                client.Options.AddSubProtocol(EchoProtocolName);

                var cookieHeader = new StringBuilder();
                foreach (var cookie in cookieContainer.GetAllCookies())
                {
                    cookieHeader.Append($"{cookie};");
                }

                client.Options.SetRequestHeader(CookieHeaderName, cookieHeader.ToString());

                return client;
            });

            _isCompressionEnabled = isCompressionEnabled;

            _websocketClient = new WebsocketClient(websocketUri, websocketClientFactory)
            {
                ReconnectTimeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds)
            };

            _websocketClient.ReconnectionHappened.Subscribe(info =>
            {
                SetupPingLoop();

                _websocketClient.MessageReceived.Subscribe(async message => await MessageReceivedHandler(message));

                _websocketClient.DisconnectionHappened.Subscribe(_disconnectionHappenedSubject.OnNext);
            });
        }

        /// <summary>
        /// Stream with received webcast responses
        /// </summary>
        public IObservable<WebcastResponse> WebcastResponseReceived => _webcastResponseReceivedSubject.AsObservable();

        /// <summary>
        /// Stream for client disconnection event (triggered after connection was lost)
        /// </summary>
        public IObservable<DisconnectionInfo> DisconnectionHappened => _disconnectionHappenedSubject.AsObservable();

        public Task Start()
        {
            return _websocketClient.StartOrFail();
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void SetupPingLoop()
        {
            var pingTaskCancellationToken = _pingTaskCancellationTokenSource.Token;
            Task.Run(async () =>
            {
                while (true)
                {
                    pingTaskCancellationToken.ThrowIfCancellationRequested();

                    _websocketClient.Send(PingBytes);

                    await Task.Delay(TimeSpan.FromSeconds(DefaultPingIntervalSeconds));
                }
            });
        }

        private async Task MessageReceivedHandler(ResponseMessage message)
        {
            if (message.MessageType != WebSocketMessageType.Binary)
            {
                return;
            }

            try
            {
                using var webcastWebsocketMessageStream = new MemoryStream(message.Binary);
                var webcastWebsocketMessage = Serializer.Deserialize<WebcastWebsocketMessage>(webcastWebsocketMessageStream);

                if (webcastWebsocketMessage.Type != DefaultWebcastMessageType)
                {
                    // ignored

                    return;
                }

                using var messageStream = new MemoryStream(webcastWebsocketMessage.Binary);
                Stream webcastResponseStream = messageStream;

                if (_isCompressionEnabled && messageStream.IsPossiblyGZipped())
                {
                    webcastResponseStream = new GZipStream(messageStream, CompressionMode.Decompress);
                }

                using (webcastResponseStream) 
                {
                    var webcastResponse = Serializer.Deserialize<WebcastResponse>(webcastResponseStream);
                    if (webcastResponse.needAck)
                    {
                        await SendAcknowledgement(webcastWebsocketMessage);
                    }

                    _webcastResponseReceivedSubject.OnNext(webcastResponse);
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private Task SendAcknowledgement(WebcastWebsocketMessage webcastWebsocketMessage)
        {
            var acknowledgement = new WebcastWebsocketAck
            {
                Type = "ack",
                Id = webcastWebsocketMessage.Id
            };

            using var acknowledgementMemoryStream = new MemoryStream();
            Serializer.Serialize(acknowledgementMemoryStream, acknowledgement);

            return _websocketClient.SendInstant(acknowledgementMemoryStream.ToArray());
        }

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _pingTaskCancellationTokenSource.Cancel();

                    _webcastResponseReceivedSubject.OnCompleted();

                    _websocketClient.Dispose();
                }

                _disposedValue = true;
            }
        }
    }
}
