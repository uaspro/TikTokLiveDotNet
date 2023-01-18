using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using TikTokLiveDotNet.Infrastructure;
using TikTokLiveDotNet.Infrastructure.Client.Http;
using TikTokLiveDotNet.Infrastructure.Client.Http.Models;
using TikTokLiveDotNet.Notifications;
using TikTokLiveDotNet.Protobuf;

namespace TikTokLiveDotNet
{
    public class TikTokLiveClient : IDisposable
    {
        public class Options
        {
            public static readonly Options Default = new();

            public bool ProcessInitialData { get; init; } = true;

            public bool EnableExtendedGiftInfo { get; init; } = false;

            public bool EnableWebsocketUpgrade { get; init; } = true;

            public int RetryWebsocketUpgradeCount { get; init; } = 5;

            public int RetryWebsocketUpgradeDelayMilliseconds { get; init; } = 5_000;

            public bool EnableWebsocketCompression { get; init; } = true;

            public bool EnableRequestPolling { get; init; } = false;

            public int RequestPollingIntervalMilliseconds { get; init; } = 1_000;

            public string? SessionId { get; init; } = null;

            public string Locale { get; init; } = "en-US";

            public Dictionary<string, string> CustomQueryParams { get; init; } = new Dictionary<string, string>();

            public Dictionary<string, string> CustomRequestHeaders { get; init; } = new Dictionary<string, string>();
        }

        public enum ConnectionType
        {
            Websocket,
            Polling
        }

        public class State
        {
            public bool IsConnecting { get; private set; }

            public bool IsConnected => ConnectionType != null;

            public ConnectionType? ConnectionType { get; private set; }

            internal void SetConnecting()
            {
                if(IsConnecting)
                {
                    throw new Exception("Already connecting!");
                }

                if(IsConnected)
                {
                    throw new Exception("Already connected!");
                }

                IsConnecting = true;
            }

            internal void SetConnected(ConnectionType connectionType)
            {
                if (!IsConnecting)
                {
                    throw new Exception("No connection started!");
                }

                if (IsConnected)
                {
                    throw new Exception("Already connected!");
                }

                IsConnecting = false;
                ConnectionType = connectionType;
            }

            internal void SetDisconnected()
            {
                IsConnecting = false;
                ConnectionType = null;
            }
        }

        public class Context
        {
            public RoomInfo? RoomInfo { get; internal set; }

            public GiftsList? AvailableGifts { get; internal set; }
        }

        private bool _disposedValue;

        private TikTokHttpClient? _tikTokHttpClient;
        private WebcastWebsocketClient? _webcastWebsocketClient;

        private Dictionary<string, string> _clientQueryParams = new Dictionary<string, string>();
        private CookieContainer? _cookieContainer;
        private readonly string _uniqueStreamerId;

        private readonly CancellationTokenSource _pollingTaskCancellationTokenSource = new();

        private readonly Subject<ReconnectionInfo> _reconnectionHappenedSubject = new();
        private readonly Subject<DisconnectionInfo> _disconnectionHappenedSubject = new();

        private readonly Subject<Message> _unhandledWebcastResponseMessageReceivedSubject = new();

        private readonly Subject<WebcastChatMessage> _webcastChatMessageReceivedSubject = new();
        private readonly Subject<WebcastSocialMessage> _webcastSocialMessageReceivedSubject = new();
        private readonly Subject<WebcastMemberMessage> _webcastMemberMessageReceivedSubject = new();
        private readonly Subject<WebcastHourlyRankMessage> _webcastHourlyRankMessageReceivedSubject = new();
        private readonly Subject<WebcastLinkMicBattle> _webcastLinkMicBattleReceivedSubject = new();
        private readonly Subject<WebcastLinkMicArmies> _webcastLinkMicArmiesReceivedSubject = new();
        private readonly Subject<WebcastEmoteChatMessage> _webcastEmoteChatMessageReceivedSubject = new();
        private readonly Subject<WebcastGiftMessage> _webcastGiftMessageReceivedSubject = new();
        private readonly Subject<WebcastEnvelopeMessage> _webcastEnvelopeMessageReceivedSubject = new();
        private readonly Subject<WebcastLikeMessage> _webcastLikeMessageReceivedSubject = new();
        private readonly Subject<WebcastQuestionNewMessage> _webcastQuestionNewMessageReceivedSubject = new();
        private readonly Subject<WebcastRoomUserSeqMessage> _webcastRoomUserSeqMessageReceivedSubject = new();
        private readonly Subject<ControlMessageInfo> _liveEndedReceivedSubject = new();

        private readonly Subject<WebcastControlMessage> _webcastUnhandledControlMessageSubject = new();
        
        public TikTokLiveClient(string uniqueStreamerId, Options options)
        {
            _uniqueStreamerId = Utils.ValidateAndNormalizeUniqueStreamerId(uniqueStreamerId);
            
            ClientOptions = options;

            Reset();
        }

        public TikTokLiveClient(string uniqueStreamerId) : this(uniqueStreamerId, Options.Default)
        {
        }

        public Options ClientOptions { get; init; }

        public State ClientState { get; init; } = new State();

        public Context ClientContext { get; init; } = new Context();

        /// <summary>
        /// Stream for client reconnection notifications (triggered after the new connection)
        /// </summary>
        public IObservable<ReconnectionInfo> ReconnectionHappened => _reconnectionHappenedSubject.AsObservable();

        /// <summary>
        /// Stream for client disconnection notifications (triggered after connection was lost)
        /// </summary>
        public IObservable<DisconnectionInfo> DisconnectionHappened => _disconnectionHappenedSubject.AsObservable();

        /// <summary>
        /// Stream with received unhandled raw webcast response messages
        /// </summary>
        public IObservable<Message> UnhandledWebcastResponseMessageReceived => _unhandledWebcastResponseMessageReceivedSubject.AsObservable();

        /// <summary>
        /// Stream with received chat messages
        /// </summary>
        public IObservable<WebcastChatMessage> ChatMessageReceived => _webcastChatMessageReceivedSubject.AsObservable();

        /// <summary>
        /// Stream with received social messages
        /// </summary>
        public IObservable<WebcastSocialMessage> SocialMessageReceived => _webcastSocialMessageReceivedSubject.AsObservable();

        /// <summary>
        /// Stream with received member messages (usually - user subscription)
        /// </summary>
        public IObservable<WebcastMemberMessage> MemberMessageReceived => _webcastMemberMessageReceivedSubject.AsObservable();

        /// <summary>
        /// Stream with received ranking updates
        /// </summary>
        public IObservable<WebcastHourlyRankMessage> HourlyRankMessageReceived => _webcastHourlyRankMessageReceivedSubject.AsObservable();

        /// <summary>
        /// Stream with received mic battle begin notifications
        /// </summary>
        public IObservable<WebcastLinkMicBattle> LinkMicBattleReceived => _webcastLinkMicBattleReceivedSubject.AsObservable();

        /// <summary>
        /// Stream with received mic battle updates
        /// </summary>
        public IObservable<WebcastLinkMicArmies> LinkMicArmiesReceived => _webcastLinkMicArmiesReceivedSubject.AsObservable();

        /// <summary>
        /// Stream with received emotes
        /// </summary>
        public IObservable<WebcastEmoteChatMessage> EmoteChatMessageReceived => _webcastEmoteChatMessageReceivedSubject.AsObservable();

        /// <summary>
        /// Stream with received gifts
        /// </summary>
        public IObservable<WebcastGiftMessage> GiftMessageReceived => _webcastGiftMessageReceivedSubject.AsObservable();

        /// <summary>
        /// Stream with received envelopes
        /// </summary>
        public IObservable<WebcastEnvelopeMessage> EnvelopeMessageReceived => _webcastEnvelopeMessageReceivedSubject.AsObservable();

        /// <summary>
        /// Stream with received likes
        /// </summary>
        public IObservable<WebcastLikeMessage> LikeMessageReceived => _webcastLikeMessageReceivedSubject.AsObservable();

        /// <summary>
        /// Stream with received questions
        /// </summary>
        public IObservable<WebcastQuestionNewMessage> QuestionNewMessageReceived => _webcastQuestionNewMessageReceivedSubject.AsObservable();

        /// <summary>
        /// Stream with received room viewer updates
        /// </summary>
        public IObservable<WebcastRoomUserSeqMessage> ViewerUpdateMessageReceived => _webcastRoomUserSeqMessageReceivedSubject.AsObservable();

        /// <summary>
        /// Stream with received live ended notifications
        /// </summary>
        public IObservable<ControlMessageInfo> LiveEndedReceived => _liveEndedReceivedSubject.AsObservable();

        /// <summary>
        /// Stream with received unhandled control messages
        /// </summary>
        public IObservable<WebcastControlMessage> UnhandledWebcastControlMessageReceived => _webcastUnhandledControlMessageSubject.AsObservable();

        public async Task Connect()
        {
            ClientState.SetConnecting();

            try
            {
                await FetchRoomInfo();

                if (ClientContext.RoomInfo?.Status == RoomInfo.RoomStatus.StreamEnded)
                {
                    throw new Exception("LIVE has ended");
                }

                if (ClientOptions.EnableExtendedGiftInfo)
                {
                    await FetchAvailableGifts();
                }

                var isWebsocketUpgradeDone = await TryUpgradeToWebsocket();

                // Sometimes no upgrade to WebSocket is offered by TikTok
                // In that case we use request polling (if enabled and possible)
                if (!isWebsocketUpgradeDone) {
                    try
                    {
                        if (!ClientOptions.EnableRequestPolling)
                        {
                            throw new Exception("Failed to perform websocket upgrade and request polling is disabled (`enableRequestPolling` option).");
                        }

                        if (ClientOptions.SessionId == null)
                        {
                            // We cannot use request polling if the user has no sessionid defined.
                            // The reason for this is that TikTok needs a valid signature if the user is not logged in.
                            // Signing a request every second would generate too much traffic to the signing server.
                            // If a sessionid is present a signature is not required.
                            throw new Exception("Failed to perform websocket upgrade. Please provide a valid `sessionId` to use request polling instead.");
                        }

                        StartFetchRoomPolling();
                    } 
                    catch (Exception ex)
                    {
                        _reconnectionHappenedSubject.OnError(ex);
                    }
                }             
            }
            catch (Exception)
            {
                ClientState.SetDisconnected();

                throw;
            }
        }

        public void Disconnect()
        {
            Reset();
            
            ClientState.SetDisconnected();
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void Reset()
        {
            _tikTokHttpClient?.Dispose();
            _webcastWebsocketClient?.Dispose();

            _clientQueryParams.Clear();
            _cookieContainer = new CookieContainer();

            _tikTokHttpClient = new TikTokHttpClient(ClientOptions.SessionId, ClientOptions.CustomRequestHeaders, _cookieContainer);

            foreach (var queryParam in Constants.DefaultQueryParams.Concat(ClientOptions.CustomQueryParams))
            {
                _clientQueryParams.Add(queryParam.Key, queryParam.Value);
            }

            _clientQueryParams["app_language"] = ClientOptions.Locale;
            _clientQueryParams["webcast_language"] = ClientOptions.Locale;

            if(ClientContext.RoomInfo != null)
            {
                _clientQueryParams["room_id"] = ClientContext.RoomInfo.Id.ToString();
            }
        }

        private async Task FetchRoomInfo()
        {
            try
            {
                var roomId = await _tikTokHttpClient.GetRoomId(_uniqueStreamerId);
                _clientQueryParams["room_id"] = roomId;

                var roomInfoResponse = await _tikTokHttpClient.GetRoomInfo(_clientQueryParams);
                ClientContext.RoomInfo = roomInfoResponse?.Data;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to fetch room info | Error: ${ex.Message}");
            }
        }

        private async Task FetchAvailableGifts()
        {
            try
            {
                var giftsListResponse = await _tikTokHttpClient.GetGiftsList(_clientQueryParams);

                ClientContext.AvailableGifts = giftsListResponse.Data;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to fetch available gifts | Error: ${ex.Message}");
            }
        }

        private async Task<WebcastResponse> FetchRoomData(bool isInitial = false)
        {
            var webcastResponse = await _tikTokHttpClient.GetRoomData(_clientQueryParams, isInitial);

            if (isInitial && string.IsNullOrEmpty(webcastResponse.Cursor))
            {
                throw new Exception("Missing cursor in initial fetch response.");
            }

            // Set cursor and internal_ext param to continue with the next request
            if (!string.IsNullOrEmpty(webcastResponse.Cursor)) 
            {
                _clientQueryParams["cursor"] = webcastResponse.Cursor;
            }

            if (!string.IsNullOrEmpty(webcastResponse.internalExt))
            {
                _clientQueryParams["internal_ext"] = webcastResponse.internalExt;
            }

            return webcastResponse;
        }

        private async Task<bool> TryUpgradeToWebsocket()
        {
            var isWsUpgradeDone = false;

            var retryCount = ClientOptions.RetryWebsocketUpgradeCount;
            while (!isWsUpgradeDone && retryCount-- > 0)
            {
                WebcastResponse webcastResponse;

                try
                {
                    webcastResponse = await FetchRoomData(isInitial: true);

                    isWsUpgradeDone = await UpgradeToWebsocketIfPossible(webcastResponse);

                    if (isWsUpgradeDone)
                    {
                        ClientState.SetConnected(ConnectionType.Websocket);

                        _reconnectionHappenedSubject.OnNext(
                            ReconnectionInfo.Success(ConnectionType.Websocket));
                    }
                }
                catch (Exception ex)
                {
                    Reset();

                    _reconnectionHappenedSubject.OnNext(
                            ReconnectionInfo.Failure(ConnectionType.Websocket, ex.Message));

                    await Task.Delay(ClientOptions.RetryWebsocketUpgradeDelayMilliseconds);

                    continue;
                }

                // Processing initial data if option enabled
                if (ClientOptions.ProcessInitialData)
                {
                    HandleWebcastResponse(webcastResponse);
                }
            }

            return isWsUpgradeDone;
        }

        private async Task<bool> UpgradeToWebsocketIfPossible(WebcastResponse webcastResponse)
        {
            // Upgrade to Websocket only if this option offered
            var upgradeToWsOffered = !string.IsNullOrEmpty(webcastResponse.wsUrl) && webcastResponse.wsParam != null;
            if (!ClientOptions.EnableWebsocketUpgrade || !upgradeToWsOffered)
            {
                return false;
            }

            var websocketParams = new Dictionary<string, string>
            {
                { webcastResponse.wsParam.Name, webcastResponse.wsParam.Value }
            };

            _webcastWebsocketClient = 
                new WebcastWebsocketClient(
                    webcastResponse.wsUrl, 
                    _clientQueryParams, 
                    websocketParams, 
                    _cookieContainer, 
                    ClientOptions.EnableWebsocketCompression);

            _webcastWebsocketClient
                .WebcastResponseReceived
                .Subscribe(webcastResponse => HandleWebcastResponse(webcastResponse));

            _webcastWebsocketClient
                .DisconnectionHappened
                .Subscribe(info =>
                {
                    _disconnectionHappenedSubject.OnNext(
                        info.Exception == null 
                            ? DisconnectionInfo.Success(ConnectionType.Websocket) 
                            : DisconnectionInfo.Failure(ConnectionType.Websocket, info.Exception.Message));
                });

            await _webcastWebsocketClient.Start();

            return true;
        }

        private void StartFetchRoomPolling()
        {
            var pollingTaskCancellationToken = _pollingTaskCancellationTokenSource.Token;
            Task.Run(async () =>
            {
                ClientState.SetConnected(ConnectionType.Polling);

                _reconnectionHappenedSubject.OnNext(
                    ReconnectionInfo.Success(ConnectionType.Polling));

                while (true)
                {
                    pollingTaskCancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        await FetchRoomData();
                    }
                    catch (Exception)
                    {
                        // ignored
                    }

                    await Task.Delay(ClientOptions.RequestPollingIntervalMilliseconds);
                }
            });
        }

        private void HandleWebcastResponse(WebcastResponse webcastResponse)
        {
            foreach (var message in webcastResponse.Messages)
            {
                using var messageStream = new MemoryStream(message.Binary);
                Stream webcastMessageStream = messageStream;

                if(ClientOptions.EnableWebsocketCompression && messageStream.IsPossiblyGZipped())
                {
                    webcastMessageStream = new GZipStream(messageStream, CompressionMode.Decompress);
                }

                using (webcastMessageStream)
                {
                    switch (message.Type)
                    {
                        case nameof(WebcastChatMessage):
                            var chatMessage = Serializer.Deserialize<WebcastChatMessage>(messageStream);
                            _webcastChatMessageReceivedSubject.OnNext(chatMessage);

                            break;
                        case nameof(WebcastSocialMessage):
                            var socialMessage = Serializer.Deserialize<WebcastSocialMessage>(messageStream);
                            _webcastSocialMessageReceivedSubject.OnNext(socialMessage);

                            break;
                        case nameof(WebcastMemberMessage):
                            var memberMessage = Serializer.Deserialize<WebcastMemberMessage>(messageStream);
                            _webcastMemberMessageReceivedSubject.OnNext(memberMessage);

                            break;
                        case nameof(WebcastHourlyRankMessage):
                            var hourlyRankMessage = Serializer.Deserialize<WebcastHourlyRankMessage>(messageStream);
                            _webcastHourlyRankMessageReceivedSubject.OnNext(hourlyRankMessage);

                            break;
                        case nameof(WebcastLinkMicBattle):
                            var linkMicBattle = Serializer.Deserialize<WebcastLinkMicBattle>(messageStream);
                            _webcastLinkMicBattleReceivedSubject.OnNext(linkMicBattle);

                            break;
                        case nameof(WebcastLinkMicArmies):
                            var linkMicArmies = Serializer.Deserialize<WebcastLinkMicArmies>(messageStream);
                            _webcastLinkMicArmiesReceivedSubject.OnNext(linkMicArmies);

                            break;
                        case nameof(WebcastEmoteChatMessage):
                            var emoteChatMessage = Serializer.Deserialize<WebcastEmoteChatMessage>(messageStream);
                            _webcastEmoteChatMessageReceivedSubject.OnNext(emoteChatMessage);

                            break;
                        case nameof(WebcastGiftMessage):
                            var giftMessage = Serializer.Deserialize<WebcastGiftMessage>(messageStream);
                            _webcastGiftMessageReceivedSubject.OnNext(giftMessage);

                            break;
                        case nameof(WebcastEnvelopeMessage):
                            var envelopeMessage = Serializer.Deserialize<WebcastEnvelopeMessage>(messageStream);
                            _webcastEnvelopeMessageReceivedSubject.OnNext(envelopeMessage);

                            break;
                        case nameof(WebcastLikeMessage):
                            var likeMessage = Serializer.Deserialize<WebcastLikeMessage>(messageStream);
                            _webcastLikeMessageReceivedSubject.OnNext(likeMessage);

                            break;
                        case nameof(WebcastQuestionNewMessage):
                            var questionNewMessage = Serializer.Deserialize<WebcastQuestionNewMessage>(messageStream);
                            _webcastQuestionNewMessageReceivedSubject.OnNext(questionNewMessage);

                            break;
                        case nameof(WebcastRoomUserSeqMessage):
                            var roomUserSeqMessage = Serializer.Deserialize<WebcastRoomUserSeqMessage>(messageStream);
                            _webcastRoomUserSeqMessageReceivedSubject.OnNext(roomUserSeqMessage);

                            break;
                        case nameof(WebcastControlMessage):
                            var controlMessage = Serializer.Deserialize<WebcastControlMessage>(messageStream);
                            
                            var controlMessageInfo = new ControlMessageInfo(controlMessage);
                            if(controlMessageInfo.Action == ControlMessageInfo.ActionType.LiveEnded)
                            {
                                _liveEndedReceivedSubject.OnNext(controlMessageInfo);
                            }
                            else
                            {
                                _webcastUnhandledControlMessageSubject.OnNext(controlMessage);
                            }

                            break;
                        default:
                            _unhandledWebcastResponseMessageReceivedSubject.OnNext(message);
                            
                            break;
                    }
                }
            }
        }

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _reconnectionHappenedSubject.OnCompleted();
                    _disconnectionHappenedSubject.OnCompleted();

                    _unhandledWebcastResponseMessageReceivedSubject.OnCompleted();

                    _webcastChatMessageReceivedSubject.OnCompleted();
                    _webcastGiftMessageReceivedSubject.OnCompleted();
                    _webcastLikeMessageReceivedSubject.OnCompleted();
                    _webcastQuestionNewMessageReceivedSubject.OnCompleted();
                    _webcastRoomUserSeqMessageReceivedSubject.OnCompleted();
                    _webcastSocialMessageReceivedSubject.OnCompleted();
                    _webcastMemberMessageReceivedSubject.OnCompleted();
                    _webcastHourlyRankMessageReceivedSubject.OnCompleted();
                    _webcastLinkMicBattleReceivedSubject.OnCompleted();
                    _webcastLinkMicArmiesReceivedSubject.OnCompleted();
                    _webcastEmoteChatMessageReceivedSubject.OnCompleted();
                    _webcastEnvelopeMessageReceivedSubject.OnCompleted();
                    _webcastUnhandledControlMessageSubject.OnCompleted();

                    _pollingTaskCancellationTokenSource.Cancel();

                    _tikTokHttpClient?.Dispose();
                    _webcastWebsocketClient?.Dispose();
                }

                _disposedValue = true;
            }
        }
    }
}
