using McpAuthzServer.Core.Interfaces;
using McpAuthzServer.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace McpAuthzServer.Auth.Validators;

/// <summary>
/// Validates JWT tokens using JWKS endpoint (similar to Python's JWTTokenVerifier)
/// </summary>
public class JwtTokenValidator : ITokenValidator
{
    private readonly string _issuer;
    private readonly string _jwksUrl;
    private readonly string? _audience;
    private readonly ILogger<JwtTokenValidator> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configManager;

    public JwtTokenValidator(
        string issuer,
        string jwksUrl,
        string? audience,
        ILogger<JwtTokenValidator> logger)
    {
        _issuer = issuer;
        _jwksUrl = jwksUrl;
        _audience = audience;
        _logger = logger;
        _tokenHandler = new JwtSecurityTokenHandler();

        // Configure to fetch JWKS from Keycloak
        _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            _jwksUrl.Replace("/protocol/openid-connect/certs", "/.well-known/openid-configuration"),
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever());

        _logger.LogInformation("JWT Verifier initialized with Issuer: {Issuer}, JWKS: {JwksUrl}", _issuer, _jwksUrl);
    }

    public async Task<AccessToken?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if token is a JWT (has 3 parts separated by dots)
            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                _logger.LogDebug("Token is not a JWT (doesn't have 3 parts)");
                return null; // Not a JWT, might be opaque token
            }

            // Get signing keys from JWKS endpoint
            var config = await _configManager.GetConfigurationAsync(cancellationToken);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = !string.IsNullOrEmpty(_audience),
                ValidAudience = _audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = config.SigningKeys,
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            var principal = _tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwtToken)
            {
                _logger.LogWarning("Token validation succeeded but result is not a JWT");
                return null;
            }

            // Extract scopes
            var scopes = ExtractScopes(principal);

            // Extract client ID
            var clientId = ExtractClientId(principal);

            _logger.LogDebug("JWT token validated successfully for client: {ClientId}", clientId);

            return new AccessToken
            {
                Token = token,
                ClientId = clientId,
                Scopes = scopes,
                ExpiresAt = jwtToken.ValidTo != DateTime.MinValue 
                    ? new DateTimeOffset(jwtToken.ValidTo).ToUnixTimeSeconds() 
                    : null,
                UserId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub"),
                AdditionalClaims = principal.Claims.ToDictionary(c => c.Type, c => (object)c.Value)
            };
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.LogWarning("JWT token has expired");
            return null;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "JWT token validation failed: {Message}", ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during JWT validation");
            return null;
        }
    }

    private static List<string> ExtractScopes(ClaimsPrincipal principal)
    {
        // Check multiple possible claim names (Keycloak can use different formats)
        var scopeClaims = principal.FindAll("scope").Select(c => c.Value).ToList();
        if (scopeClaims.Any())
        {
            return scopeClaims.SelectMany(s => s.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Distinct().ToList();
        }

        scopeClaims = principal.FindAll("scp").Select(c => c.Value).ToList();
        if (scopeClaims.Any())
        {
            return scopeClaims;
        }

        return new List<string>();
    }

    private static string ExtractClientId(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue("client_id") 
            ?? principal.FindFirstValue("azp") 
            ?? principal.FindFirstValue("appid") 
            ?? "unknown";
    }
}
