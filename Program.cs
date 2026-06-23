using DiscordRPC;
using DiscordRPC.Logging;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.IO;
using System.Text.Json.Serialization;

// Get kavita url and application id from json file
string configRaw = File.ReadAllText("config.json");
var config = JsonSerializer.Deserialize<Config>(configRaw);
string applicationId = config.DiscordApplicationId;
string kavitaUrl = config.KavitaUrl;

HttpClient sharedClient = new()
{
    BaseAddress = new Uri(kavitaUrl),
};
// Create the client
var client = new DiscordRpcClient(applicationId)
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

record Config(
    [property: JsonPropertyName("discord_application_id")] string DiscordApplicationId,
    [property: JsonPropertyName("kavita_url")] string KavitaUrl
);