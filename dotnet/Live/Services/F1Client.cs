using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using System.Web;
using Live.Models;
using Live.Utilities;

namespace Live.Services;

public class F1Client : IDisposable
{
    private const string F1BaseUrl = "livetiming.formula1.com/signalr";
    private const string SignalRHub = """[{ "name": "Streaming" }]""";
    private const string SignalRSubscribe = """
        {
            "H": "Streaming",
            "M": "Subscribe",
            "A": [[
                "Heartbeat",
                "CarData.z",
                "Position.z",
                "ExtrapolatedClock",
                "TopThree",
                "RcmSeries",
                "TimingStats",
                "TimingAppData",
                "WeatherData",
                "TrackStatus",
                "SessionStatus",
                "DriverList",
                "RaceControlMessages",
                "SessionInfo",
                "SessionData",
                "LapCount",
                "TimingData",
                "TeamRadio",
                "PitLaneTimeCollection",
                "ChampionshipPrediction"
            ]],
            "I": 1
        }
        """;

    private readonly ILogger<F1Client> _logger;
    private readonly HttpClient _httpClient;

    public F1Client(ILogger<F1Client> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("F1");
    }

    public async IAsyncEnumerable<Message> ConnectAndStreamAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateBounded<Message>(new BoundedChannelOptions(32)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _ = Task.Run(async () => await RunConnectionLoopAsync(channel.Writer, cancellationToken), cancellationToken);

        await foreach (var message in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return message;
        }
    }

    private async Task RunConnectionLoopAsync(ChannelWriter<Message> writer, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);

                ClientWebSocket? socket = null;
                try
                {
                    socket = await InitializeConnectionAsync(cancellationToken);
                    if (socket is null)
                    {
                        _logger.LogError("Failed to initialize WebSocket connection, retrying...");
                        continue;
                    }

                    var buffer = new byte[1024 * 64];
                    var messageBuffer = new StringBuilder();

                    while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                    {
                        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                        try
                        {
                            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), linkedCts.Token);

                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                _logger.LogWarning("WebSocket closed by server, reconnecting...");
                                break;
                            }

                            if (result.MessageType == WebSocketMessageType.Text)
                            {
                                messageBuffer.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                                if (result.EndOfMessage)
                                {
                                    var messageText = messageBuffer.ToString();
                                    messageBuffer.Clear();

                                    var message = ParseMessage(messageText);
                                    if (message is not null)
                                    {
                                        if (ShouldRestart(message))
                                        {
                                            _logger.LogInformation("Session change detected, restarting connection...");
                                            break;
                                        }
                                        await writer.WriteAsync(message, cancellationToken);
                                    }
                                }
                            }
                        }
                        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                        {
                            _logger.LogWarning("Timeout waiting for message, reconnecting...");
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in WebSocket connection, reconnecting...");
                }
                finally
                {
                    if (socket is not null)
                    {
                        try
                        {
                            if (socket.State == WebSocketState.Open)
                            {
                                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                            }
                        }
                        catch { }
                        socket.Dispose();
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Connection loop cancelled");
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private async Task<ClientWebSocket?> InitializeConnectionAsync(CancellationToken cancellationToken)
    {
        var envUrl = Environment.GetEnvironmentVariable("WS_URL");
        Uri wsUri;
        Negotiation? negotiation = null;

        if (!string.IsNullOrEmpty(envUrl))
        {
            wsUri = new Uri(envUrl);
        }
        else
        {
            negotiation = await NegotiateAsync(cancellationToken);
            if (negotiation is null)
            {
                return null;
            }

            var queryParams = HttpUtility.ParseQueryString(string.Empty);
            queryParams["clientProtocol"] = "1.5";
            queryParams["transport"] = "webSockets";
            queryParams["connectionToken"] = negotiation.Token;
            queryParams["connectionData"] = SignalRHub;

            wsUri = new Uri($"wss://{F1BaseUrl}/connect?{queryParams}");
        }

        var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("User-Agent", "BestHTTP");
        socket.Options.SetRequestHeader("Accept-Encoding", "gzip,identity");

        if (negotiation?.Cookie is not null)
        {
            socket.Options.Cookies = new CookieContainer();
            socket.Options.Cookies.SetCookies(new Uri($"https://{F1BaseUrl}"), negotiation.Cookie);
        }

        try
        {
            using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, connectCts.Token);
            await socket.ConnectAsync(wsUri, linkedCts.Token);
            _logger.LogInformation("Connected to WebSocket");

            var subscribeBytes = Encoding.UTF8.GetBytes(SignalRSubscribe);
            await socket.SendAsync(new ArraySegment<byte>(subscribeBytes), WebSocketMessageType.Text, true, cancellationToken);
            _logger.LogInformation("Subscribed to F1 data feeds");

            return socket;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to WebSocket");
            socket.Dispose();
            return null;
        }
    }

    private record Negotiation(string Token, string? Cookie);

    private async Task<Negotiation?> NegotiateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var queryParams = HttpUtility.ParseQueryString(string.Empty);
            queryParams["clientProtocol"] = "1.5";
            queryParams["connectionData"] = SignalRHub;

            var url = $"https://{F1BaseUrl}/negotiate?{queryParams}";

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

            var response = await _httpClient.GetAsync(url, linkedCts.Token);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonDocument>(linkedCts.Token);
            var token = json?.RootElement.GetProperty("ConnectionToken").GetString() ?? string.Empty;

            string? cookie = null;
            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                cookie = cookies.FirstOrDefault();
            }

            _logger.LogDebug("Negotiation successful, token: {Token}", token);
            return new Negotiation(token, cookie);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Negotiation failed");
            return null;
        }
    }

    private Message? ParseMessage(string data)
    {
        try
        {
            var json = JsonNode.Parse(data);
            if (json is null)
                return null;

            if (json["R"] is JsonNode initial)
            {
                JsonTransformer.Transform(initial);
                return new Message.Initial(initial);
            }

            if (json["M"] is JsonArray updates && updates.Count > 0)
            {
                var items = new List<(string Topic, JsonNode Data)>();

                foreach (var update in updates)
                {
                    var category = update?["A"]?[0]?.GetValue<string>();
                    var updateData = update?["A"]?[1];

                    if (category is null || updateData is null)
                        continue;

                    JsonTransformer.Transform(updateData);
                    items.Add((JsonTransformer.ToCamelCase(category), updateData));
                }

                if (items.Count > 0)
                    return new Message.Updates(items);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse message");
            return null;
        }
    }

    private static bool ShouldRestart(Message message)
    {
        if (message is Message.Updates updates)
        {
            foreach (var (topic, data) in updates.Items)
            {
                if (topic == "sessionInfo" && data["name"] is not null)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public void Dispose()
    {
    }
}
