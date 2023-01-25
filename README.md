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
var streamLink = @"https://www.tiktok.com/@username/live";
using var tikTokLiveStreamClient = new TikTokLiveClient(streamLink);
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
Example project here - [TikTokLiveDotNet.Example](https://github.com/uaspro/TikTokLiveDotNet/tree/main/TikTokLiveDotNet.Example)

![image](https://user-images.githubusercontent.com/1931585/213922229-e8fd6638-1843-4e9c-bea7-43bd349c1c23.png)
