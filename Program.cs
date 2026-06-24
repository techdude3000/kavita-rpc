using DiscordRPC;
using DiscordRPC.Logging;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.IO;
using System.Text.Json.Serialization;
using System.Diagnostics;
using ImageMagick;

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
    Logger = new ConsoleLogger(LogLevel.Info, true)
};


// Method to get current activity
async Task<List<CurrentActivity>> GetCurrentActivity()
{
    var currentActivity = await HttpClient.GetAsync("/api/Activity/current");
    currentActivity.EnsureSuccessStatusCode();
    var sessions = await currentActivity.Content.ReadFromJsonAsync<List<CurrentActivity>>();
    return sessions;
}

// Method to get volume info
async Task<VolumeInfo> GetVolumeInfo(int VolumeId)
{
    var volumeInfo = await HttpClient.GetAsync($"/api/Series/volume?volumeId={VolumeId}");
    volumeInfo.EnsureSuccessStatusCode();
    var sessions = await volumeInfo.Content.ReadFromJsonAsync<VolumeInfo>();
    return sessions;
}

// Method to get chapter info
async Task<ChapterInfo> GetChapterInfo(int ChapterId)
{
    var volumeInfo = await HttpClient.GetAsync($"/api/Series/chapter?chapterId={ChapterId}");
    volumeInfo.EnsureSuccessStatusCode();
    var sessions = await volumeInfo.Content.ReadFromJsonAsync<ChapterInfo>();
    return sessions;
}

// Method to upload a file (image) to litterbox (file hoster)
async Task<string> UploadToLitterbox(int? ChapterId, int? VolumeId)
{
    // Step 1: download the file from the link
    Console.WriteLine($"Downloading image");
    byte[] fileBytes;
    if (ChapterId.HasValue)
    {
        fileBytes = await HttpClient.GetByteArrayAsync($"/api/image/chapter-cover?chapterId={ChapterId}&apiKey={config.KavitaApiKey}.webp");
    }
    else
    {
        fileBytes = await HttpClient.GetByteArrayAsync($"/api/image/volume-cover?volumeId={VolumeId}&apiKey={config.KavitaApiKey}.webp");
    }
    Console.WriteLine($"Download complete");

    // Add transparent 1:1 padding to image to avoid discord squaring it
    using (var image = new MagickImage(fileBytes))
    {
        Console.WriteLine("Converting image...");
        int height = (int)image.Height;
        image.BackgroundColor = MagickColors.Transparent;
        image.Extent((uint)height, (uint)height, Gravity.Center);
        image.Resize(500, 500);
        image.Format = MagickFormat.WebP;
        fileBytes = image.ToByteArray();
        Console.WriteLine("Image converted.");
    }

    // Step 2: build the multipart form
    using var form = new MultipartFormDataContent();
    form.Add(new StringContent("12h"), "time");
    form.Add(new StringContent("fileupload"), "reqtype");

    var fileContent = new ByteArrayContent(fileBytes);
    form.Add(fileContent, "fileToUpload", "cover.webp");

    // Step 3: upload to litterbox
    Console.WriteLine("Uploading image to litterbox...");
    var response = await HttpClient.PostAsync(
        "https://litterbox.catbox.moe/resources/internals/api.php",
        form
    );
    response.EnsureSuccessStatusCode();
    string resultUrl = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"Upload complete (URL: {resultUrl})");
    return resultUrl;
}

// Method to update RPC
async Task UpdateRPC(RpcState state)
{
    var sessions = await GetCurrentActivity();
    var session = sessions?.FirstOrDefault(s => s.ActivityData.Count > 0);
    var myActivity = session?.ActivityData.FirstOrDefault();
    if (myActivity == null || session == null)
    {
        return;
    }
    // If the chapter id is different from last time, get new image
    if (myActivity.ChapterId != state.LastChapterId)
    {
        var volumeInfo = await GetVolumeInfo(myActivity.VolumeId);
        var chapterInfo = await GetChapterInfo(myActivity.ChapterId);
        // If user wants to show chapter images instead of volume images
        if (config.UseChapterImage)
        {
            state.CurrentImageUrl = await UploadToLitterbox(myActivity.ChapterId, null);
        }
        else if (myActivity.VolumeId != state.LastVolumeId)
        {
            state.CurrentImageUrl = await UploadToLitterbox(null, myActivity.VolumeId);
        }
        // If chapter number is negative, dont show current chapter
        if (chapterInfo.ChapterNumber < 0)
        {
             state.CurrentChapter = "";
        }
        else
        {
            state.CurrentChapter = $"Chapter {chapterInfo.ChapterNumber}, ";
        }
    
        // Same thing with volume number
        if (volumeInfo.VolumeNumber < 0)
        {
             state.CurrentVolume = "";
        }
        else
        {
            state.CurrentVolume = $"Volume {volumeInfo.VolumeNumber}, ";
        }
        state.LastChapterId = myActivity.ChapterId;
        state.LastVolumeId = myActivity.VolumeId;
    }
    if (myActivity != null)
    {
        client.SetPresence(new RichPresence()
        {
            Details = $"Reading: {myActivity.SeriesName}",
            State = $"{state.CurrentVolume}{state.CurrentChapter}Page {myActivity.PagesRead + myActivity.StartPage + 1} / {myActivity.TotalPages}",
            Timestamps = new Timestamps(session.StartTimeUtc),
            StatusDisplay = StatusDisplayType.Name,
            Assets = new Assets()
            {
                LargeImageKey = state.CurrentImageUrl,
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

// Actual program
var state = new RpcState();
while (true) {
    await UpdateRPC(state);
    await Task.Delay(config.UpdateInterval);
}

// Records
record Config(
    [property: JsonPropertyName("discord_application_id")] string DiscordApplicationId,
    [property: JsonPropertyName("kavita_url")] string KavitaUrl,
    [property: JsonPropertyName("kavita_api_key")] string KavitaApiKey,
    [property: JsonPropertyName("update_interval")] int UpdateInterval,
    [property: JsonPropertyName("use_chapter_image")] bool UseChapterImage
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
    [property: JsonPropertyName("startTimeUtc")] DateTime StartTimeUtc,
    [property: JsonPropertyName("activityData")] List<ActivityData> ActivityData
);
record VolumeInfo(
    [property: JsonPropertyName("name")] float VolumeNumber
);
record ChapterInfo(
    [property: JsonPropertyName("title")] float ChapterNumber
);
class RpcState
{
    public int LastChapterId;
    public int LastVolumeId;
    public string CurrentImageUrl;
    public string CurrentVolume;
    public string CurrentChapter;
}