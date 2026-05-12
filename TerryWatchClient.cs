using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TerryWatch.Client;

public static class TerryWatch
{
    public static TerryWatchClient Init(
        string apiKey,
        string appId,
        string baseUrl = "https://terrywatch.net",
        HttpClient? httpClient = null)
    {
        return new TerryWatchClient(
            httpClient ?? new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10),
            },
            new Uri(baseUrl.TrimEnd('/') + "/"),
            appId,
            apiKey);
    }
}

public static class Terrywatch
{
    public static TerryWatchClient Init(
        string apiKey,
        string appId,
        string baseUrl = "https://terrywatch.net",
        HttpClient? httpClient = null)
    {
        return TerryWatch.Init(apiKey, appId, baseUrl, httpClient);
    }
}

public sealed class TerryWatchClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient httpClient;
    private readonly string appId;
    private readonly string apiSecret;

    public TerryWatchClient(
        HttpClient httpClient,
        Uri baseUrl,
        string appId,
        string apiSecret)
    {
        this.httpClient = httpClient;
        this.httpClient.BaseAddress = baseUrl;
        this.appId = appId;
        this.apiSecret = apiSecret;
    }

    public async Task<TerryWatchSession> StartSessionAsync(
        string steamId,
        string authToken,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/sessions/start")
        {
            Content = JsonContent.Create(
                new StartSessionRequest(steamId, authToken),
                options: JsonOptions),
        };

        request.Headers.Add("X-App-Id", appId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiSecret);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await ReadJsonAsync<StartSessionResponse>(response, cancellationToken);

        return new TerryWatchSession(httpClient, body.SessionId, body.SessionToken, body.Player.Uuid);
    }

    private static async Task<T> ReadJsonAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string? errorCode = null;
            try
            {
                errorCode = JsonSerializer.Deserialize<ApiError>(body, JsonOptions)?.Error;
            }
            catch (JsonException)
            {
                // The API should return JSON errors, but keep the raw body for diagnostics.
            }

            throw new TerryWatchApiException(response.StatusCode, errorCode, body);
        }

        var result = JsonSerializer.Deserialize<T>(body, JsonOptions);
        if (result is null)
        {
            throw new TerryWatchApiException(response.StatusCode, null, body);
        }

        return result;
    }

    private sealed record StartSessionRequest(string SteamId, string AuthToken);

    private sealed record StartSessionResponse(
        [property: JsonPropertyName("session_id")] string SessionId,
        [property: JsonPropertyName("session_token")] string SessionToken,
        PlayerResponse Player);

    private sealed record PlayerResponse(string Uuid);

    private sealed record ApiError(string? Error);

    public sealed class TerryWatchSession
    {
        private readonly HttpClient httpClient;

        internal TerryWatchSession(
            HttpClient httpClient,
            string sessionId,
            string sessionToken,
            string playerUuid)
        {
            this.httpClient = httpClient;
            SessionId = sessionId;
            SessionToken = sessionToken;
            PlayerUuid = playerUuid;
        }

        public string SessionId { get; }

        public string SessionToken { get; }

        public string PlayerUuid { get; }

        public async Task SendEventAsync(
            string name,
            object? properties = null,
            DateTimeOffset? occurredAt = null,
            CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/events")
            {
                Content = JsonContent.Create(
                    new TrackEventRequest(
                        name,
                        properties is null
                            ? null
                            : JsonSerializer.SerializeToElement(properties, JsonOptions),
                        occurredAt),
                    options: JsonOptions),
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SessionToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await httpClient.SendAsync(request, cancellationToken);
            await ReadJsonAsync<TrackEventResponse>(response, cancellationToken);
        }

        public async Task<EndSessionResponse> EndAsync(
            DateTimeOffset? endedAt = null,
            CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/sessions/end")
            {
                Content = JsonContent.Create(new EndSessionRequest(endedAt), options: JsonOptions),
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SessionToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await httpClient.SendAsync(request, cancellationToken);

            return await ReadJsonAsync<EndSessionResponse>(response, cancellationToken);
        }

        private sealed record TrackEventRequest(
            string Name,
            JsonElement? Properties = null,
            DateTimeOffset? OccurredAt = null);

        private sealed record TrackEventResponse(int Id);

        private sealed record EndSessionRequest(DateTimeOffset? EndedAt = null);
    }
}

public sealed class TerryWatchApiException : Exception
{
    public TerryWatchApiException(HttpStatusCode statusCode, string? errorCode, string responseBody)
        : base(errorCode is null
            ? $"TerryWatch API request failed with {(int)statusCode}."
            : $"TerryWatch API request failed with {(int)statusCode}: {errorCode}.")
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        ResponseBody = responseBody;
    }

    public HttpStatusCode StatusCode { get; }

    public string? ErrorCode { get; }

    public string ResponseBody { get; }
}

public sealed record EndSessionResponse(
    [property: JsonPropertyName("session_id")] string SessionId,
    [property: JsonPropertyName("duration_seconds")] int? DurationSeconds);
