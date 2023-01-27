using TikTokLiveDotNet;

var exitEvent = new ManualResetEvent(false);

Console.OutputEncoding = Encoding.Unicode;

Console.WriteLine("Enter stream URL or streamer nickname:");

string streamLinkOrStreamerNickname;
while (string.IsNullOrWhiteSpace(streamLinkOrStreamerNickname = Console.ReadLine())) ;

// 1. Setup client
using var tikTokLiveStreamClient = new TikTokLiveClient(streamLinkOrStreamerNickname);

// 2. Configure event handlers
tikTokLiveStreamClient.ReconnectionHappened.Subscribe(
    info =>
    {
        Console.ForegroundColor = info.IsRetry ? ConsoleColor.Red : ConsoleColor.Green;
        Console.WriteLine($">> {info.ConnectionType} connection {(info.IsRetry ? $"failed | Error: {info.FailureCause}. Retrying..." : "established!")}");
    });

tikTokLiveStreamClient.DisconnectionHappened.Subscribe(
    info =>
    {
        Console.ForegroundColor = info.IsFailure ? ConsoleColor.Red : ConsoleColor.DarkYellow;
        Console.WriteLine($">> {info.ConnectionType} disconnected{(info.IsFailure ? $" | Error: {info.FailureCause}." : ".")}");
    });

tikTokLiveStreamClient.ChatMessageReceived.Subscribe(chatMessage =>
{
    Console.ForegroundColor = ConsoleColor.DarkBlue;
    Console.WriteLine($"<{chatMessage.User.Nickname}>: {chatMessage.Comment}");
});

tikTokLiveStreamClient.GiftMessageReceived.Subscribe(async giftMessage =>
{
    // Simulate some async work
    await Task.Delay(100);

    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"<{giftMessage.User.Nickname}> sent a gift: {giftMessage.repeatCount}x {giftMessage.giftDetails.giftName} " +
                        $"({giftMessage.repeatCount}x{giftMessage.giftDetails.diamondCount}={giftMessage.repeatCount * giftMessage.giftDetails.diamondCount} coins) | " +
                        $"({(giftMessage.repeatEnd == 1 ? "Combo ended" : "Ongoing combo")})");
});

tikTokLiveStreamClient.LikeMessageReceived.Subscribe(likeMessage =>
{
    Console.ForegroundColor = ConsoleColor.Gray;
    Console.WriteLine($"<{likeMessage.User.Nickname}> liked the stream {likeMessage.likeCount}x times | (Total likes count - {likeMessage.totalLikeCount})");
});

tikTokLiveStreamClient.ViewerUpdateMessageReceived.Subscribe(viewerUpdateMessage =>
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"Viewers count: {viewerUpdateMessage.viewerCount} | Top 3 viewers: {string.Join(", ", viewerUpdateMessage.topViewers.Take(3).Select(v => $"[{v.coinCount} coins] <{v.User.Nickname}>"))}");
});

// 3. Connect to the stream
await tikTokLiveStreamClient.Connect();

// Wait for exit event in order to prevent exiting console app
exitEvent.WaitOne();
