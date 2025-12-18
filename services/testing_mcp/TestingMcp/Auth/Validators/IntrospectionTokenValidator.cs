using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TestingMcp.Core.Interfaces;
using TestingMcp.Core.Models;

namespace TestingMcp.Auth.Validators;

public class IntrospectionTokenValidator : ITokenValidator
{
    private readonly string _introspectionEndpoint;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _serverUrl;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IntrospectionTokenValidator> _logger;

    public IntrospectionTokenValidator(
        string introspectionEndpoint,
        string clientId,
        string clientSecret,
        string serverUrl,
        IHttpClientFactory httpClientFactory,
        ILogger<IntrospectionTokenValidator> logger)
    {
        _introspectionEndpoint = introspectionEndpoint;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _serverUrl = serverUrl;
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        _logger.LogInformation("Introspection Verifier initialized with endpoint: {Endpoint}", _introspectionEndpoint);
    }

    public async Task<AccessToken?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        // Validate URL to prevent SSRF attacks
        if (!_introspectionEndpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !_introspectionEndpoint.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase) &&
            !_introspectionEndpoint.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Rejecting introspection endpoint with unsafe scheme: {Endpoint}", _introspectionEndpoint);
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            // Create Basic Auth header
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("token", token),
                new KeyValuePair<string, string>("token_type_hint", "access_token")
            });

            var response = await client.PostAsync(_introspectionEndpoint, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Token introspection returned status {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var data = JsonSerializer.Deserialize<IntrospectionResponse>(json);

            if (data == null || !data.Active)
            {
                return null;
            }

            var scopes = data.Scope?.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>();
            // Ensure required scopes are always included (matching Python behavior)
            if (!scopes.Contains("gas-token")) scopes.Add("gas-token");
            if (!scopes.Contains("offline_access")) scopes.Add("offline_access");

            _logger.LogInformation("Token introspection successful for client: {ClientId}", data.ClientId);

            return new AccessToken
            {
                Token = token,
                ClientId = data.ClientId ?? "unknown",
                Scopes = scopes,
                ExpiresAt = data.Exp,
                Resource = data.Aud?.ToString(),
                UserId = data.Sub
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token introspection failed");
            return null;
        }
    }
}
