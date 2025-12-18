namespace McpAuthzServer.Core.Models;

/// <summary>
/// Represents an OAuth2 access token with associated metadata
/// </summary>
public class AccessToken
{
    /// <summary>
    /// The actual token string (JWT or opaque token)
    /// </summary>
    public required string Token { get; init; }

    /// <summary>
    /// OAuth2 client identifier
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Scopes granted to this token
    /// </summary>
    public required IReadOnlyList<string> Scopes { get; init; }

    /// <summary>
    /// Unix timestamp when token expires
    /// </summary>
    public long? ExpiresAt { get; init; }

    /// <summary>
    /// Resource this token is bound to (RFC 8707)
    /// </summary>
    public string? Resource { get; init; }

    /// <summary>
    /// User identifier
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Additional claims from the token
    /// </summary>
    public Dictionary<string, object> AdditionalClaims { get; init; } = new();

    /// <summary>
    /// Check if token is expired
    /// </summary>
    public bool IsExpired()
    {
        if (ExpiresAt == null) return false;
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= ExpiresAt.Value;
    }

    /// <summary>
    /// Mask the token for logging (show only first and last 4 characters)
    /// </summary>
    public string MaskedToken(int visibleChars = 4)
    {
        if (string.IsNullOrEmpty(Token)) return "<empty>";
        if (Token.Length <= visibleChars * 2) return Token;
        return $"{Token[..visibleChars]}...{Token[^visibleChars..]}";
    }
}
