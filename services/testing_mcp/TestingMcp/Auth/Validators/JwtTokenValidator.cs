using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using TestingMcp.Core.Interfaces;
using TestingMcp.Core.Models;

namespace TestingMcp.Auth.Validators;

public class JwtTokenValidator : ITokenValidator
{
    private readonly string _issuer;
    private readonly string _jwksUrl;
    private readonly string? _audience;
    private readonly string _serverUrl;
    private readonly bool _validateResource;
    private readonly ILogger<JwtTokenValidator> _logger;
    private readonly HttpClient _httpClient;
    private JsonWebKeySet? _jwks;
    private DateTime _jwksLastFetched = DateTime.MinValue;
    private readonly TimeSpan _jwksCacheDuration = TimeSpan.FromMinutes(5);

    public JwtTokenValidator(
        string issuer,
        string jwksUrl,
        string serverUrl,
        ILogger<JwtTokenValidator> logger,
        string? audience = null,
        bool validateResource = false)
    {
        _issuer = issuer;
        _jwksUrl = jwksUrl;
        _serverUrl = serverUrl;
        _audience = audience;
        _validateResource = validateResource;
        _logger = logger;

        // Configure HttpClient to accept self-signed certificates for development
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        _httpClient = new HttpClient(handler);

        _logger.LogInformation("JWT Verifier initialized with:");
        _logger.LogInformation("  JWKS URL: {JwksUrl}", _jwksUrl);
        _logger.LogInformation("  Issuer: {Issuer}", _issuer);
        _logger.LogInformation("  Audience: {Audience}", _audience ?? "none");
    }

    public async Task<AccessToken?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if token is a JWT (has 3 parts separated by dots)
            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                return null; // Not a JWT, maybe opaque token
            }

            // Fetch JWKS if needed
            await RefreshJwksIfNeededAsync(cancellationToken);
            if (_jwks == null)
            {
                _logger.LogWarning("Failed to fetch JWKS");
                return null;
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = _audience != null,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = _jwks.GetSigningKeys(),
                ClockSkew = TimeSpan.FromMinutes(1),
                NameClaimType = "preferred_username",
                RoleClaimType = "roles"
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            var jwtToken = (JwtSecurityToken)validatedToken;

            // Extract scopes
            var scopes = ExtractScopes(jwtToken);

            // Extract client ID
            var clientId = ExtractClientId(jwtToken);

            // Extract expiration
            var expiresAt = jwtToken.Payload.Expiration;

            // Extract audience for resource
            var resource = ExtractAudience(jwtToken);

            _logger.LogInformation("JWT token verified successfully for client: {ClientId}", clientId);

            return new AccessToken
            {
                Token = token,
                ClientId = clientId,
                Scopes = scopes,
                ExpiresAt = expiresAt,
                Resource = resource,
                UserId = jwtToken.Subject
            };
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.LogInformation("JWT token has expired");
            return null;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogInformation("JWT token validation failed: {Error}", ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JWT token verification failed");
            return null;
        }
    }

    private async Task RefreshJwksIfNeededAsync(CancellationToken cancellationToken)
    {
        if (_jwks != null && DateTime.UtcNow - _jwksLastFetched < _jwksCacheDuration)
        {
            return;
        }

        try
        {
            var response = await _httpClient.GetStringAsync(_jwksUrl, cancellationToken);
            _jwks = new JsonWebKeySet(response);
            _jwksLastFetched = DateTime.UtcNow;
            _logger.LogDebug("JWKS refreshed from {JwksUrl}", _jwksUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch JWKS from {JwksUrl}", _jwksUrl);
        }
    }

    private List<string> ExtractScopes(JwtSecurityToken token)
    {
        // Standard OAuth2 'scope' claim (space-separated)
        if (token.Payload.TryGetValue("scope", out var scopeClaim) && scopeClaim is string scopeString)
        {
            return scopeString.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        var scopes = new List<string>();

        // Keycloak 'realm_access.roles' claim
        if (token.Payload.TryGetValue("realm_access", out var realmAccess) && realmAccess is JsonElement realmAccessElement)
        {
            if (realmAccessElement.TryGetProperty("roles", out var rolesElement) && rolesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var role in rolesElement.EnumerateArray())
                {
                    scopes.Add($"realm:{role.GetString()}");
                }
            }
        }

        // Keycloak 'resource_access' claim
        if (token.Payload.TryGetValue("resource_access", out var resourceAccess) && resourceAccess is JsonElement resourceAccessElement)
        {
            foreach (var client in resourceAccessElement.EnumerateObject())
            {
                if (client.Value.TryGetProperty("roles", out var clientRoles) && clientRoles.ValueKind == JsonValueKind.Array)
                {
                    foreach (var role in clientRoles.EnumerateArray())
                    {
                        scopes.Add($"{client.Name}:{role.GetString()}");
                    }
                }
            }
        }

        return scopes;
    }

    private string ExtractClientId(JwtSecurityToken token)
    {
        // Standard 'client_id' claim
        if (token.Payload.TryGetValue("client_id", out var clientId) && clientId != null)
        {
            return clientId.ToString()!;
        }

        // Keycloak 'azp' (authorized party) claim
        if (token.Payload.TryGetValue("azp", out var azp) && azp != null)
        {
            return azp.ToString()!;
        }

        // OAuth2 'aud' claim if it's a string
        if (token.Payload.Aud?.FirstOrDefault() is string aud)
        {
            return aud;
        }

        // Fall back to 'sub' (subject)
        return token.Subject ?? "unknown";
    }

    private string? ExtractAudience(JwtSecurityToken token)
    {
        return token.Payload.Aud?.FirstOrDefault();
    }
}
