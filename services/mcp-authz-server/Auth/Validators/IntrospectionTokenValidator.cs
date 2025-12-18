using McpAuthzServer.Core.Interfaces;
using McpAuthzServer.Core.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace McpAuthzServer.Auth.Validators;

/// <summary>
/// Validates opaque tokens via OAuth2 introspection endpoint (similar to Python's IntrospectionTokenVerifier)
/// </summary>
public class IntrospectionTokenValidator : ITokenValidator
{
    private readonly string _introspectionEndpoint;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly ILogger<IntrospectionTokenValidator> _logger;
    private readonly HttpClient _httpClient;

    public IntrospectionTokenValidator(
        string introspectionEndpoint,
        string clientId,
        string clientSecret,
        IHttpClientFactory httpClientFactory,
        ILogger<IntrospectionTokenValidator> logger)
    {
        _introspectionEndpoint = introspectionEndpoint;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("IntrospectionClient");

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
            // Prepare Basic Auth header
            var authHeaderValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));

            var request = new HttpRequestMessage(HttpMethod.Post, _introspectionEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeaderValue);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = token,
                ["token_type_hint"] = "access_token"
            });

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Token introspection returned status {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var introspectionResponse = JsonSerializer.Deserialize<IntrospectionResponse>(json);

            if (introspectionResponse == null || !introspectionResponse.Active)
            {
                _logger.LogDebug("Token introspection returned inactive token");
                return null;
            }

            // Parse scopes
            var scopes = introspectionResponse.Scope?.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>();

            _logger.LogInformation("Token introspection successful for client: {ClientId}", introspectionResponse.ClientId);

            return new AccessToken
            {
                Token = token,
                ClientId = introspectionResponse.ClientId ?? "unknown",
                Scopes = scopes,
                ExpiresAt = introspectionResponse.Exp,
                UserId = introspectionResponse.Sub,
                Resource = introspectionResponse.Aud,
                AdditionalClaims = new Dictionary<string, object>
                {
                    ["username"] = introspectionResponse.Username ?? string.Empty,
                    ["token_type"] = introspectionResponse.TokenType ?? "Bearer",
                    ["iss"] = introspectionResponse.Iss ?? string.Empty
                }
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error during token introspection: {Message}", ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token introspection");
            return null;
        }
    }
}
