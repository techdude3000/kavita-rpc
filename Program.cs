using DiscordRPC;
using DiscordRPC.Logging;
using System.Net.Http;
using System.Net.Http.Json;

const string DISCORD_APP_ID = "1518850863878115378";

// Create the client
var client = new DiscordRpcClient(DISCORD_APP_ID)
{
    Logger = new ConsoleLogger(LogLevel.Info, true)
};

client.OnReady += (sender, e) =>
{
    Console.WriteLine("Connected to Discord with user {0}", e.User.Username);
};

client.OnPresenceUpdate += (sender, e) =>
{
    Console.WriteLine("Presence updated: {0}", e.Presence);
};

// Connects to the Discord IPC pipe
client.Initialize();

// Set the rich presence
client.SetPresence(new RichPresence()
{
    Details = "details",
    State = "state",
    Assets = new Assets()
    {
        LargeImageKey = "image_large",
        LargeImageText = "image text",
        SmallImageKey = "image_small"
    },
});

Console.WriteLine("Press any key to exit...");
Console.ReadKey();

// Important: cleans up and clears the presence on exit
client.Dispose();