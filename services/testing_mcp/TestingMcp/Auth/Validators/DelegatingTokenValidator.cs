using TestingMcp.Core.Interfaces;
using TestingMcp.Core.Models;

namespace TestingMcp.Auth.Validators;

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
            // First try to verify as a JWT token
            var accessToken = await _jwtValidator.ValidateTokenAsync(token, cancellationToken);
            if (accessToken != null)
            {
                _logger.LogDebug("Token verified as JWT successfully.");
                _tokenCache.Add(accessToken);
                return accessToken;
            }

            // If JWT verification fails and introspection is available, try introspection
            if (_introspectionValidator != null)
            {
                accessToken = await _introspectionValidator.ValidateTokenAsync(token, cancellationToken);
                if (accessToken != null)
                {
                    _logger.LogDebug("Token verified as opaque token successfully via introspection.");
                    _tokenCache.Add(accessToken);
                    return accessToken;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token verification failed with unexpected exception");
            return null;
        }
    }
}
