using System.Text.Json;
using TestingMcp.Core.Models;

namespace TestingMcp.Services;

public class TokenExchangeService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TokenExchangeService> _logger;
    private readonly string _authzServerUrl;

    public TokenExchangeService(
        IHttpClientFactory httpClientFactory,
        ILogger<TokenExchangeService> logger,
        string authzServerUrl)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _authzServerUrl = authzServerUrl;
    }

    public async Task<string?> ExchangeForGasTokenAsync(string mcpAccessToken, CancellationToken cancellationToken = default)
    {
        // If the token doesn't contain a dot, it's already a GAS token (opaque), return as is
        if (!mcpAccessToken.Contains('.'))
        {
            return mcpAccessToken;
        }

        var url = $"{_authzServerUrl}/broker/oidc/token";
        
        try
        {
            // Create HttpClient that accepts self-signed certificates for localhost
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            using var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {mcpAccessToken}");
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenResponse = JsonSerializer.Deserialize<TokenExchangeResponse>(json);

            return tokenResponse?.AccessToken ?? string.Empty;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error when calling broker endpoint: {Url}", url);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error when calling broker endpoint");
            return null;
        }
    }

    public async Task<Dictionary<string, object>?> GetUserInfoAsync(string gasAccessToken, string userInfoEndpoint, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {gasAccessToken}");
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            var response = await client.GetAsync(userInfoEndpoint, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error when calling userinfo endpoint: {Endpoint}", userInfoEndpoint);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error when calling userinfo endpoint");
            return null;
        }
    }
}
