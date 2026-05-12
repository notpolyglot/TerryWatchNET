# TerryWatch C# Client

A small dependency-free C# client for sending game telemetry to TerryWatch.

## Setup

Reference this project from your game or server project:

```bash
dotnet add reference path/to/TerryWatch.Client.csproj
```

Then import the namespace:

```csharp
using TerryWatch.Client;
```

## Basic Usage

Initialize the client with the credentials shown on the TerryWatch game setup page:

```csharp
var client = Terrywatch.Init(
    apiKey: "your_api_secret",
    appId: "your_app_id"
);
```

For a deployed TerryWatch server, pass the base URL:

```csharp
var client = Terrywatch.Init(
    apiKey: "your_api_secret",
    appId: "your_app_id",
    baseUrl: "https://your-terrywatch-domain.com"
);
```

Start a player session, send events, then end the session:

```csharp
var session = await client.StartSessionAsync(
    steamId: "76561198000000001",
    authToken: "sbox_auth_token"
);

await session.SendEventAsync("level_started", new
{
    level = 1,
    map = "facility"
});

await session.SendEventAsync("level_complete", new
{
    level = 1,
    score = 1200,
    time_seconds = 84
});

var ended = await session.EndAsync();
Console.WriteLine($"Session duration: {ended.DurationSeconds}s");
```

## API Flow

1. `Terrywatch.Init(...)` stores your app ID, API key, and server URL.
2. `StartSessionAsync(...)` calls `POST /api/sessions/start` and returns a session token.
3. `SendEventAsync(...)` calls `POST /api/events` using that session token.
4. `EndAsync(...)` calls `POST /api/sessions/end`.

The TerryWatch dashboard unlocks after the first successful session start request.

## Errors

Failed API responses throw `TerryWatchApiException`.

```csharp
try
{
    var session = await client.StartSessionAsync(steamId, authToken);
}
catch (TerryWatchApiException exception)
{
    Console.WriteLine(exception.StatusCode);
    Console.WriteLine(exception.ErrorCode);
    Console.WriteLine(exception.ResponseBody);
}
```

Common error codes include:

- `missing_credentials`
- `invalid_credentials`
- `invalid_auth_token`
- `missing_token`
- `invalid_token`
- `session_ended`
- `session_idle_closed`

## Optional HttpClient

If your app already manages an `HttpClient`, pass it into `Init`:

```csharp
var httpClient = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(5)
};

var client = Terrywatch.Init(
    apiKey: "your_api_secret",
    appId: "your_app_id",
    baseUrl: "https://your-terrywatch-domain.com",
    httpClient: httpClient
);
```

## Build

```bash
dotnet build TerryWatch.Client.csproj
```
