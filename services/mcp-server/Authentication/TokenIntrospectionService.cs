using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace F1McpServer.Authentication;

public interface ITokenIntrospectionService
{
    Task<TokenIntrospectionResult> IntrospectTokenAsync(string token, CancellationToken cancellationToken = default);
}

public class TokenIntrospectionService : ITokenIntrospectionService
{
    private readonly HttpClient _httpClient;
    private readonly KeycloakOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TokenIntrospectionService> _logger;

    public TokenIntrospectionService(
        HttpClient httpClient,
        IOptions<KeycloakOptions> options,
        IMemoryCache cache,
        ILogger<TokenIntrospectionService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _cache = cache;
        _logger = logger;
    }

    public async Task<TokenIntrospectionResult> IntrospectTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"token_introspection_{ComputeTokenHash(token)}";

        if (_cache.TryGetValue(cacheKey, out TokenIntrospectionResult? cachedResult) && cachedResult != null)
        {
            _logger.LogDebug("Token introspection result retrieved from cache");
            return cachedResult;
        }

        try
        {
            var result = await PerformIntrospectionAsync(token, cancellationToken);

            if (result.Active)
            {
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.TokenIntrospectionCacheSeconds)
                };
                _cache.Set(cacheKey, result, cacheOptions);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token introspection failed");
            return new TokenIntrospectionResult { Active = false, Error = ex.Message };
        }
    }

    private async Task<TokenIntrospectionResult> PerformIntrospectionAsync(string token, CancellationToken cancellationToken)
    {
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));

        var request = new HttpRequestMessage(HttpMethod.Post, _options.IntrospectionEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = token,
            ["token_type_hint"] = "access_token"
        });
        request.Content = content;

        _logger.LogDebug("Performing token introspection against {Endpoint}", _options.IntrospectionEndpoint);

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Token introspection failed with status {StatusCode}: {Error}",
                response.StatusCode, errorContent);
            return new TokenIntrospectionResult
            {
                Active = false,
                Error = $"Introspection endpoint returned {response.StatusCode}"
            };
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<TokenIntrospectionResult>(responseContent);

        return result ?? new TokenIntrospectionResult { Active = false, Error = "Failed to deserialize response" };
    }

    private static string ComputeTokenHash(string token)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hashBytes);
    }
}

public class TokenIntrospectionResult
{
    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("client_id")]
    public string? ClientId { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("exp")]
    public long? ExpirationTime { get; set; }

    [JsonPropertyName("iat")]
    public long? IssuedAt { get; set; }

    [JsonPropertyName("nbf")]
    public long? NotBefore { get; set; }

    [JsonPropertyName("sub")]
    public string? Subject { get; set; }

    [JsonPropertyName("aud")]
    public object? Audience { get; set; }

    [JsonPropertyName("iss")]
    public string? Issuer { get; set; }

    [JsonPropertyName("jti")]
    public string? TokenId { get; set; }

    [JsonPropertyName("realm_access")]
    public RealmAccess? RealmAccess { get; set; }

    [JsonPropertyName("resource_access")]
    public Dictionary<string, ResourceAccess>? ResourceAccess { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("email_verified")]
    public bool? EmailVerified { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("preferred_username")]
    public string? PreferredUsername { get; set; }

    [JsonPropertyName("given_name")]
    public string? GivenName { get; set; }

    [JsonPropertyName("family_name")]
    public string? FamilyName { get; set; }

    [JsonIgnore]
    public string? Error { get; set; }

    public bool HasRole(string role)
    {
        return RealmAccess?.Roles?.Contains(role) == true;
    }

    public bool HasClientRole(string clientId, string role)
    {
        return ResourceAccess?.TryGetValue(clientId, out var access) == true &&
               access.Roles?.Contains(role) == true;
    }
}

public class RealmAccess
{
    [JsonPropertyName("roles")]
    public List<string>? Roles { get; set; }
}

public class ResourceAccess
{
    [JsonPropertyName("roles")]
    public List<string>? Roles { get; set; }
}
