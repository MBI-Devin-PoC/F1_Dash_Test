using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace F1McpServer.Authentication;

public class KeycloakAuthenticationHandler : AuthenticationHandler<KeycloakAuthenticationOptions>
{
    private readonly ITokenIntrospectionService _tokenIntrospectionService;
    private readonly KeycloakOptions _keycloakOptions;

    public KeycloakAuthenticationHandler(
        IOptionsMonitor<KeycloakAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ITokenIntrospectionService tokenIntrospectionService,
        IOptions<KeycloakOptions> keycloakOptions)
        : base(options, logger, encoder)
    {
        _tokenIntrospectionService = tokenIntrospectionService;
        _keycloakOptions = keycloakOptions.Value;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
        {
            return AuthenticateResult.NoResult();
        }

        var authHeader = authorizationHeader.ToString();
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.Fail("Empty bearer token");
        }

        try
        {
            var introspectionResult = await _tokenIntrospectionService.IntrospectTokenAsync(token, Context.RequestAborted);

            if (!introspectionResult.Active)
            {
                Logger.LogWarning("Token introspection returned inactive token: {Error}", introspectionResult.Error);
                return AuthenticateResult.Fail("Token is not active");
            }

            var claims = BuildClaims(introspectionResult);
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            Logger.LogDebug("Successfully authenticated user {Username}", introspectionResult.PreferredUsername);
            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Authentication failed during token introspection");
            return AuthenticateResult.Fail($"Authentication failed: {ex.Message}");
        }
    }

    private List<Claim> BuildClaims(TokenIntrospectionResult introspectionResult)
    {
        var claims = new List<Claim>();

        if (!string.IsNullOrEmpty(introspectionResult.Subject))
            claims.Add(new Claim(ClaimTypes.NameIdentifier, introspectionResult.Subject));

        if (!string.IsNullOrEmpty(introspectionResult.PreferredUsername))
            claims.Add(new Claim(ClaimTypes.Name, introspectionResult.PreferredUsername));

        if (!string.IsNullOrEmpty(introspectionResult.Email))
            claims.Add(new Claim(ClaimTypes.Email, introspectionResult.Email));

        if (!string.IsNullOrEmpty(introspectionResult.GivenName))
            claims.Add(new Claim(ClaimTypes.GivenName, introspectionResult.GivenName));

        if (!string.IsNullOrEmpty(introspectionResult.FamilyName))
            claims.Add(new Claim(ClaimTypes.Surname, introspectionResult.FamilyName));

        if (!string.IsNullOrEmpty(introspectionResult.Name))
            claims.Add(new Claim("name", introspectionResult.Name));

        if (!string.IsNullOrEmpty(introspectionResult.ClientId))
            claims.Add(new Claim("client_id", introspectionResult.ClientId));

        if (!string.IsNullOrEmpty(introspectionResult.Scope))
            claims.Add(new Claim("scope", introspectionResult.Scope));

        if (!string.IsNullOrEmpty(introspectionResult.TokenId))
            claims.Add(new Claim("jti", introspectionResult.TokenId));

        if (introspectionResult.RealmAccess?.Roles != null)
        {
            foreach (var role in introspectionResult.RealmAccess.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        if (introspectionResult.ResourceAccess != null)
        {
            foreach (var (clientId, access) in introspectionResult.ResourceAccess)
            {
                if (access.Roles != null)
                {
                    foreach (var role in access.Roles)
                    {
                        claims.Add(new Claim($"resource_role:{clientId}", role));
                    }
                }
            }
        }

        return claims;
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        Response.Headers.Append("WWW-Authenticate", $"Bearer realm=\"{_keycloakOptions.Realm}\"");
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 403;
        return Task.CompletedTask;
    }
}

public class KeycloakAuthenticationOptions : AuthenticationSchemeOptions
{
    public bool EnableTokenIntrospection { get; set; } = true;
    public List<string> RequiredScopes { get; set; } = new();
    public List<string> RequiredRoles { get; set; } = new();
}
