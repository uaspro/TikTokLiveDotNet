# TikTokLiveDotNet
This library provides an ability to subscribe and act on TikTok Live stream events (chat messages, gifts, etc.).
Ispired by [TikTok-Live-Connector](https://github.com/zerodytrash/TikTok-Live-Connector) and [TikTokLiveSharp](https://github.com/sebheron/TikTokLiveSharp).

# Usage
Install [NuGet package](https://www.nuget.org/packages/TikTokLiveDotNet/):
```c#
Install-Package TikTokLiveDotNet
```

Setup client:
```c#
using var tikTokLiveStreamClient = new TikTokLiveClient("{streamLinkOrNickname}");
```

Configure event handler(s):
```c#
tikTokLiveStreamClient.GiftMessageReceived.Subscribe(giftMessage =>
{
    Console.WriteLine($"<{giftMessage.User.Nickname}> sent a gift: {giftMessage.repeatCount}x {giftMessage.giftDetails.giftName}");
});
```

Connect to the stream:
```c#
await tikTokLiveStreamClient.Connect();
```

# Examples
Example project here - [TikTokLiveDotNet.Example](https://github.com/uaspro/TikTokLiveDotNet/tree/uaspro/add-readme/TikTokLiveDotNet.Example)
