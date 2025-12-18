using McpAuthzServer.Auth;
using McpAuthzServer.Auth.Validators;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;
using ModelContextProtocol.Server;

// Alias to avoid conflict with MCP SDK's ITokenCache
using ITokenCache = McpAuthzServer.Core.Interfaces.ITokenCache;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Configuration
var config = builder.Configuration;
var serverHost = config["McpServer:Host"] ?? "0.0.0.0";
var serverPort = int.Parse(config["McpServer:Port"] ?? "5000");
var serverName = config["McpServer:ServerName"] ?? "localhost";
var protocol = config["McpServer:Protocol"] ?? "http";
var serverUrl = $"{protocol}://{serverName}:{serverPort}";

// Check if Keycloak is configured
var keycloakSection = config.GetSection("Keycloak");
var keycloakEnabled = keycloakSection.Exists() && !string.IsNullOrEmpty(keycloakSection["Authority"]);

string? keycloakAuthority = null;
string? keycloakRealm = null;
string? keycloakRealmUrl = null;
string? keycloakJwksUrl = null;

// Introspection settings (optional)
string? introspectionEndpoint = null;
string? introspectionClientId = null;
string? introspectionClientSecret = null;
bool enableIntrospection = false;

if (keycloakEnabled)
{
    keycloakAuthority = keycloakSection["Authority"]!;
    keycloakRealm = keycloakSection["Realm"] ?? "master";
    keycloakRealmUrl = $"{keycloakAuthority}/realms/{keycloakRealm}";
    keycloakJwksUrl = $"{keycloakRealmUrl}/protocol/openid-connect/certs";

    // Optional introspection configuration
    var introspectionSection = config.GetSection("Introspection");
    enableIntrospection = introspectionSection.Exists() && !string.IsNullOrEmpty(introspectionSection["Endpoint"]);
    if (enableIntrospection)
    {
        introspectionEndpoint = introspectionSection["Endpoint"]!;
        introspectionClientId = introspectionSection["ClientId"] ?? "";
        introspectionClientSecret = introspectionSection["ClientSecret"] ?? "";
    }
}

// Configure services
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

// Token cache
var tokenCacheSize = int.Parse(config["TokenCache:MaxSize"] ?? "100");
builder.Services.AddSingleton<ITokenCache>(new TokenCache(tokenCacheSize));

if (keycloakEnabled)
{
    // JWT Token Validator (always enabled when Keycloak is configured)
    builder.Services.AddSingleton(sp => new JwtTokenValidator(
        keycloakRealmUrl!,
        keycloakJwksUrl!,
        null, // audience - validated separately
        sp.GetRequiredService<ILogger<JwtTokenValidator>>()));

    // Introspection Token Validator (optional)
    if (enableIntrospection)
    {
        builder.Services.AddSingleton(sp => new IntrospectionTokenValidator(
            introspectionEndpoint!,
            introspectionClientId!,
            introspectionClientSecret!,
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<ILogger<IntrospectionTokenValidator>>()));
    }

    // Delegating Token Validator
    builder.Services.AddSingleton(sp =>
    {
        var jwtValidator = sp.GetRequiredService<JwtTokenValidator>();
        var tokenCache = sp.GetRequiredService<ITokenCache>();
        var logger = sp.GetRequiredService<ILogger<DelegatingTokenValidator>>();
        var introspectionValidator = enableIntrospection
            ? sp.GetService<IntrospectionTokenValidator>()
            : null;
        return new DelegatingTokenValidator(jwtValidator, tokenCache, logger, introspectionValidator);
    });

    // Authentication with MCP SDK's built-in handler
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = McpAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.Authority = keycloakRealmUrl;
        options.RequireHttpsMetadata = protocol == "https";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = keycloakRealmUrl,
            ValidateAudience = false, // Audience validated via custom logic or disabled for DCR
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "preferred_username",
            RoleClaimType = "roles"
        };

        // Custom token validation using DelegatingTokenValidator
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var delegatingValidator = context.HttpContext.RequestServices.GetRequiredService<DelegatingTokenValidator>();
                var token = context.Request.Headers.Authorization.ToString().Replace("Bearer ", "");

                var accessToken = await delegatingValidator.ValidateTokenAsync(token);
                if (accessToken == null)
                {
                    context.Fail("Token validation failed");
                }
                else
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("Token validated for client: {ClientId}", accessToken.ClientId);
                }
            },
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning(context.Exception, "JWT authentication failed");
                return Task.CompletedTask;
            }
        };
    })
    .AddMcp(options =>
    {
        // MCP-compliant Protected Resource Metadata (RFC 9728)
        options.ResourceMetadata = new ProtectedResourceMetadata
        {
            Resource = new Uri(serverUrl),
            AuthorizationServers = [new Uri(keycloakRealmUrl!)],
            ScopesSupported = ["openid", "profile", "email"],
            ResourceName = "MCP Authorization Server",
            ResourceDocumentation = new Uri("https://github.com/MBI-Devin-PoC/mcp-authz-server")
        };
    });

    // Authorization policies
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("McpAccess", policy =>
            policy.RequireAuthenticatedUser());
    });
}
else
{
    builder.Services.AddAuthorization();
}

// Configure MCP Server with HTTP transport
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

// Configure middleware
if (keycloakEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();

    // Map MCP endpoints with authorization
    app.MapMcp().RequireAuthorization("McpAccess");

    // Authentication info endpoint
    app.MapGet("/auth/config", () =>
    {
        return Results.Ok(new
        {
            Authority = keycloakAuthority,
            Realm = keycloakRealm,
            RealmUrl = keycloakRealmUrl,
            JwksUrl = keycloakJwksUrl,
            IntrospectionEnabled = enableIntrospection,
            IntrospectionEndpoint = enableIntrospection ? introspectionEndpoint : null
        });
    }).AllowAnonymous();

    // Token introspection endpoint (for debugging)
    app.MapGet("/auth/introspect", async (
        HttpContext context,
        DelegatingTokenValidator validator) =>
    {
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { Error = "Missing or invalid Authorization header" });
        }

        var token = authHeader["Bearer ".Length..].Trim();
        var accessToken = await validator.ValidateTokenAsync(token);

        if (accessToken == null)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new
        {
            Active = true,
            UserId = accessToken.UserId,
            accessToken.ClientId,
            Scopes = accessToken.Scopes,
            ExpiresAt = accessToken.ExpiresAt.HasValue
                ? DateTimeOffset.FromUnixTimeSeconds(accessToken.ExpiresAt.Value).ToString("o")
                : null,
            Resource = accessToken.Resource,
            IsExpired = accessToken.IsExpired(),
            AdditionalClaims = accessToken.AdditionalClaims
        });
    });

    // User info endpoint
    app.MapGet("/auth/userinfo", (HttpContext context) =>
    {
        var user = context.User;
        if (!user.Identity?.IsAuthenticated ?? true)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new
        {
            IsAuthenticated = true,
            Name = user.Identity?.Name,
            Claims = user.Claims.Select(c => new { c.Type, c.Value })
        });
    }).RequireAuthorization();

    app.Logger.LogInformation("Keycloak OAuth2/OpenID Connect authentication enabled");
    app.Logger.LogInformation("Authority: {Authority}", keycloakAuthority);
    app.Logger.LogInformation("Realm: {Realm}", keycloakRealm);
    app.Logger.LogInformation("Realm URL: {RealmUrl}", keycloakRealmUrl);
    if (enableIntrospection)
    {
        app.Logger.LogInformation("Token introspection enabled: {Endpoint}", introspectionEndpoint);
    }
}
else
{
    app.MapMcp();
    app.Logger.LogWarning("Keycloak authentication is not configured. Running without authentication.");
    app.Logger.LogWarning("To enable authentication, configure the 'Keycloak' section in appsettings.json");
}

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Timestamp = DateTime.UtcNow,
    Server = "MCP Authorization Server"
}));

// Info endpoint
app.MapGet("/info", () => Results.Ok(new
{
    Name = "MCP Authorization Server",
    Version = "1.0.0",
    Description = "Model Context Protocol server with OAuth2/Keycloak authentication and DCR support",
    AuthenticationEnabled = keycloakEnabled,
    ServerUrl = serverUrl,
    Endpoints = new
    {
        MCP = "/",
        Health = "/health",
        Info = "/info",
        ProtectedResourceMetadata = keycloakEnabled ? "/.well-known/oauth-protected-resource" : null,
        AuthConfig = keycloakEnabled ? "/auth/config" : null,
        AuthIntrospect = keycloakEnabled ? "/auth/introspect" : null,
        AuthUserInfo = keycloakEnabled ? "/auth/userinfo" : null
    }
}));

// Startup logging
app.Logger.LogInformation("MCP Authorization Server starting on {ServerUrl}", serverUrl);
app.Logger.LogInformation("Token cache size: {CacheSize}", tokenCacheSize);

app.Run($"{protocol}://{serverHost}:{serverPort}");
