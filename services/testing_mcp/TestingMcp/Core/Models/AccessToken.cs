namespace TestingMcp.Core.Models;

public class AccessToken
{
    public string Token { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = new();
    public long? ExpiresAt { get; set; }
    public string? Resource { get; set; }
    public string? UserId { get; set; }
    public Dictionary<string, object>? AdditionalClaims { get; set; }

    public bool IsExpired()
    {
        if (!ExpiresAt.HasValue) return false;
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds() > ExpiresAt.Value;
    }
}
