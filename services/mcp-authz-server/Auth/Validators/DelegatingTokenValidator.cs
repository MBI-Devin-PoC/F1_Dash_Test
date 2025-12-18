using McpAuthzServer.Core.Interfaces;
using McpAuthzServer.Core.Models;
using Microsoft.Extensions.Logging;

namespace McpAuthzServer.Auth.Validators;

/// <summary>
/// Delegates token validation to JWT or Introspection validators
/// (Similar to Python's DelegatingTokenVerifier)
/// </summary>
public class DelegatingTokenValidator : ITokenValidator
{
    private readonly JwtTokenValidator _jwtValidator;
    private readonly IntrospectionTokenValidator? _introspectionValidator;
    private readonly ITokenCache _tokenCache;
    private readonly ILogger<DelegatingTokenValidator> _logger;

    public DelegatingTokenValidator(
        JwtTokenValidator jwtValidator,
        ITokenCache tokenCache,
        ILogger<DelegatingTokenValidator> logger,
        IntrospectionTokenValidator? introspectionValidator = null)
    {
        _jwtValidator = jwtValidator;
        _introspectionValidator = introspectionValidator;
        _tokenCache = tokenCache;
        _logger = logger;
    }

    public async Task<AccessToken?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check cache first
            var cachedToken = _tokenCache.Get(token);
            if (cachedToken != null && !cachedToken.IsExpired())
            {
                _logger.LogDebug("Token found in cache");
                return cachedToken;
            }

            // First try JWT validation
            var accessToken = await _jwtValidator.ValidateTokenAsync(token, cancellationToken);
            if (accessToken != null)
            {
                _logger.LogDebug("Token verified as JWT successfully");
                _tokenCache.Add(accessToken);
                return accessToken;
            }

            // If JWT validation fails and introspection is available, try introspection for opaque tokens
            if (_introspectionValidator != null)
            {
                accessToken = await _introspectionValidator.ValidateTokenAsync(token, cancellationToken);
                if (accessToken != null)
                {
                    _logger.LogDebug("Token verified as opaque token successfully via introspection");
                    _tokenCache.Add(accessToken);
                    return accessToken;
                }
            }

            _logger.LogWarning("Token validation failed with all available methods");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token verification failed with unexpected exception");
            return null;
        }
    }
}
