using DiscordRPC;
using DiscordRPC.Logging;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.IO;
using System.Text.Json.Serialization;
using System.Diagnostics;

// Define resultUrl string
string resultUrl = "";

// Get config from json file
string configRaw = File.ReadAllText("config.json");
var config = JsonSerializer.Deserialize<Config>(configRaw);

// Choose base url
using var HttpClient = new HttpClient 
{
    BaseAddress = new Uri(config.KavitaUrl) 
};

// Create the RPC client
var client = new DiscordRpcClient(config.DiscordApplicationId)
{
    Logger = new ConsoleLogger(LogLevel.Warning, true)
};


// Method to get current activity
async Task<List<CurrentActivity>> GetCurrentActivity()
{
    var currentActivity = await HttpClient.GetAsync("/api/Activity/current");
    currentActivity.EnsureSuccessStatusCode();
    var sessions = await currentActivity.Content.ReadFromJsonAsync<List<CurrentActivity>>();
    return sessions;
}

// Method to upload a file (image) to litterbox (file hoster)
async Task UploadToLitterbox(string fileUrl)
{
    using var httpClient = new HttpClient();
    // Step 1: download the file from the link
    Console.WriteLine($"Downloading from {fileUrl}");
    var fileBytes = await httpClient.GetByteArrayAsync(fileUrl);
    Console.WriteLine($"Download complete");

    // Step 2: build the multipart form
    using var form = new MultipartFormDataContent();
    form.Add(new StringContent("12h"), "time");
    form.Add(new StringContent("fileupload"), "reqtype");

    var fileContent = new ByteArrayContent(fileBytes);
    form.Add(fileContent, "fileToUpload", Path.GetFileName(fileUrl));

    // Step 3: upload to litterbox
    Console.WriteLine("Uploading image to litterbox...");
    var response = await httpClient.PostAsync(
        "https://litterbox.catbox.moe/resources/internals/api.php",
        form
    );
    response.EnsureSuccessStatusCode();
    resultUrl = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"Upload complete (URL: {resultUrl})");
}

// Method to update RPC
async Task UpdateRPC(RpcState state)
{
    var sessions = await GetCurrentActivity();
    var myActivity = sessions?
        .SelectMany(s => s.ActivityData)
        .FirstOrDefault();
    if (myActivity == null)
    {
        return;
    }
    if (myActivity.ChapterId != state.LastChapterId)
    {
        string fileUrl = $"{config.KavitaUrl}/api/image/chapter-cover?chapterId={myActivity.ChapterId}&apiKey={config.KavitaApiKey}.webp";
        await UploadToLitterbox(fileUrl);
        state.LastChapterId = myActivity.ChapterId;
    }
    if (myActivity != null)
    {
        Console.WriteLine($"LargeImageKey = '{resultUrl}' (length: {resultUrl.Length})");
        client.SetPresence(new RichPresence()
        {
            Details = myActivity.SeriesName,
            State = $"Page {myActivity.PagesRead + myActivity.StartPage + 1}/{myActivity.TotalPages}",
            Assets = new Assets()
            {
                LargeImageKey = resultUrl,
            },
        });
    }
}

// Tell user when connected
client.OnReady += (sender, e) =>
{
    Console.WriteLine("Connected to Discord with user {0}", e.User.Username);
};

// Tell user when prescence updates
client.OnPresenceUpdate += (sender, e) =>
{
    Console.WriteLine("Presence updated: {0}", e.Presence);
};

// Connects to the Discord IPC pipe
client.Initialize();

// Authenticate with Kavita API
var authResponse = await HttpClient.PostAsync(
    $"/api/Plugin/authenticate?apiKey={config.KavitaApiKey}&pluginName=kavita-rpc",
    null
);
authResponse.EnsureSuccessStatusCode();
var auth = await authResponse.Content.ReadFromJsonAsync<AuthKeyResponse>();
HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.Token);

var state = new RpcState();
while (true) {
    await UpdateRPC(state);
    await Task.Delay(config.UpdateInterval);
}
// Important: cleans up and clears the presence on exit
// client.Dispose();

record Config(
    [property: JsonPropertyName("discord_application_id")] string DiscordApplicationId,
    [property: JsonPropertyName("kavita_url")] string KavitaUrl,
    [property: JsonPropertyName("kavita_api_key")] string KavitaApiKey,
    [property: JsonPropertyName("update_interval")] int UpdateInterval
);
record AuthKeyResponse(
    [property: JsonPropertyName("token")] string Token
);
record ActivityData(
    [property: JsonPropertyName("seriesName")] string SeriesName,
    [property: JsonPropertyName("pagesRead")] int PagesRead,
    [property: JsonPropertyName("totalPages")] int TotalPages,
    [property: JsonPropertyName("chapterId")] int ChapterId,
    [property: JsonPropertyName("volumeId")] int VolumeId,
    [property: JsonPropertyName("seriesId")] int SeriesId,
    [property: JsonPropertyName("startPage")] int StartPage
);
record CurrentActivity(
    [property: JsonPropertyName("isActive")] bool IsActive,
    [property: JsonPropertyName("userId")] int UserId,
    [property: JsonPropertyName("activityData")] List<ActivityData> ActivityData
);
class RpcState
{
    public int LastChapterId;
}