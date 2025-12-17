namespace F1McpServer.Authentication;

public class KeycloakOptions
{
    public const string SectionName = "Keycloak";

    public string Authority { get; set; } = string.Empty;
    public string Realm { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public bool RequireHttpsMetadata { get; set; } = true;
    public bool ValidateAudience { get; set; } = true;
    public bool ValidateIssuer { get; set; } = true;
    public bool ValidateLifetime { get; set; } = true;
    public bool EnableTokenIntrospection { get; set; } = true;
    public int TokenIntrospectionCacheSeconds { get; set; } = 60;

    public string RealmUrl => $"{Authority.TrimEnd('/')}/realms/{Realm}";
    public string TokenEndpoint => $"{RealmUrl}/protocol/openid-connect/token";
    public string IntrospectionEndpoint => $"{RealmUrl}/protocol/openid-connect/token/introspect";
    public string UserInfoEndpoint => $"{RealmUrl}/protocol/openid-connect/userinfo";
    public string JwksUri => $"{RealmUrl}/protocol/openid-connect/certs";
    public string OpenIdConfigurationEndpoint => $"{RealmUrl}/.well-known/openid-configuration";
}
