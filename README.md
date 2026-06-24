# kavita-rpc
Beta — many things not finished / unstable
## Build
1. Install .NET 9.0 SDK
2. Clone the repo: `git clone https://github.com/techdude3000/kavita-rpc`
3. Download the .nupkg of the [prerelease version of discord-rpc-csharp](https://github.com/Lachee/discord-rpc-csharp/releases/tag/v1.6.2)
4. Add the downloaded package `dotnet add package DiscordRichPresence -s (directory where you saved the .nupkg)`
5. `dotnet build` or `dotnet run`

## Edit config
1. Rename the example config: `mv config.example.json config.json`
2. Edit the config: `nano config.json`
3. Change the `kavita_url` field to your kavita url
4. Change the `kavita_api_key` field to your kavita API key (found at Settings --> Account --> Auth Keys / OPDS)
5. Optional — Change the `update_interval` (measured in miliseconds)
6. Optional — If you want to display chapter cover images instead of volume cover images, set `use_chapter_image` to true

### Notes:
Kavita's API seems to always be 1 page behind (so I have added +1 to the page count). The API being behind can lead to some odd things, like when being on page 1 of a chapter/volume, it might display that you are on page e.g. 15 / 14 on the previous chapter/volume\
\
Uses [Magik.NET](https://github.com/dlemstra/Magick.NET) and [discord-rpc-csharp](https://github.com/Lachee/discord-rpc-csharp)\
\
Project was not vibecoded but did have some ai assistance.
