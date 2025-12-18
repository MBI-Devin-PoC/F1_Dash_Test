using System.Text.Json.Serialization;

namespace TestingMcp.Core.Models;

public class IntrospectionResponse
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
    public long? Exp { get; set; }

    [JsonPropertyName("iat")]
    public long? Iat { get; set; }

    [JsonPropertyName("nbf")]
    public long? Nbf { get; set; }

    [JsonPropertyName("sub")]
    public string? Sub { get; set; }

    [JsonPropertyName("aud")]
    public object? Aud { get; set; }

    [JsonPropertyName("iss")]
    public string? Iss { get; set; }

    [JsonPropertyName("jti")]
    public string? Jti { get; set; }
}
