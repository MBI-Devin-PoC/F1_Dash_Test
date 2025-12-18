using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;
using ModelContextProtocol.Server;
using TestingMcp.Auth;
using TestingMcp.Auth.Validators;
using TestingMcp.Services;
using TestingMcp.Tools;
using ITokenCache = TestingMcp.Core.Interfaces.ITokenCache;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Configuration (matching Python config.py)
var config = builder.Configuration;

// SSL configuration
var sslKeyFile = config["Ssl:KeyFile"];
var sslCertFile = config["Ssl:CertFile"];
var sslEnabled = !string.IsNullOrEmpty(sslKeyFile) && !string.IsNullOrEmpty(sslCertFile) &&
                 File.Exists(sslKeyFile) && File.Exists(sslCertFile);
var protocol = sslEnabled ? "https" : "http";

// Server configuration
var serverHost = config["McpServer:Host"] ?? "localhost";
var serverName = config["McpServer:ServerName"] ?? serverHost;
var serverPort = int.Parse(config["McpServer:Port"] ?? "8001");
var serverUrl = config["McpServer:Url"] ?? $"{protocol}://{serverName}:{serverPort}";

// Authorization server configuration
var authzServerUrl = config["Authz:ServerUrl"] ?? "https://keycloak/realms/master";
var authzRealm = config["Authz:Realm"] ?? "master";
var authzScopes = config["Authz:Scopes"] ?? "gas-token offline_access";
var authzJwksUrl = config["Authz:JwksUrl"] ?? $"{authzServerUrl}/protocol/openid-connect/certs";

// Introspection configuration (optional)
var introspectionEndpoint = config["Introspection:Endpoint"];
var introspectionClientId = config["Introspection:ClientId"];
var introspectionClientSecret = config["Introspection:ClientSecret"];
var enableIntrospection = !string.IsNullOrEmpty(introspectionEndpoint) &&
                          !string.IsNullOrEmpty(introspectionClientId) &&
                          !string.IsNullOrEmpty(introspectionClientSecret);

// UserInfo endpoint
var userInfoEndpoint = config["UserInfo:Endpoint"] ?? "https://sso/idp/userinfo.openid";

// Log level
var logLevel = config["Logging:LogLevel:Default"] ?? "Information";

// Configure services
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

// Token cache
builder.Services.AddSingleton<ITokenCache>(sp =>
{
    var cache = sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
    var logger = sp.GetRequiredService<ILogger<TokenCache>>();
    return new TokenCache(cache, logger, 100);
});

// JWT Token Validator
builder.Services.AddSingleton(sp => new JwtTokenValidator(
    authzServerUrl,
    authzJwksUrl,
    serverUrl,
    sp.GetRequiredService<ILogger<JwtTokenValidator>>()));

// Introspection Token Validator (optional)
if (enableIntrospection)
{
    builder.Services.AddSingleton(sp => new IntrospectionTokenValidator(
        introspectionEndpoint!,
        introspectionClientId!,
        introspectionClientSecret!,
        serverUrl,
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

// Token Exchange Service
builder.Services.AddSingleton(sp => new TokenExchangeService(
    sp.GetRequiredService<IHttpClientFactory>(),
    sp.GetRequiredService<ILogger<TokenExchangeService>>(),
    authzServerUrl));

// MCP Tools (register as scoped for DI)
builder.Services.AddScoped<McpTools>();

// Authentication with MCP SDK's built-in handler
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = McpAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.Authority = authzServerUrl;
    options.RequireHttpsMetadata = protocol == "https";
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = authzServerUrl,
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
        AuthorizationServers = [new Uri(authzServerUrl)],
        ScopesSupported = authzScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList(),
        ResourceName = "MCP Resource Server",
        ResourceDocumentation = new Uri("https://github.com/MBI-Devin-PoC/F1_Dash_Test")
    };
});

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("McpAccess", policy =>
        policy.RequireAuthenticatedUser());
});

// Configure MCP Server with HTTP transport
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

// Configure middleware
app.UseAuthentication();
app.UseAuthorization();

// Map MCP endpoints with authorization at root path (matching Python: streamable_http_path="/")
app.MapMcp().RequireAuthorization("McpAccess");

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Timestamp = DateTime.UtcNow,
    Server = "MCP Resource Server"
}));

// Info endpoint
app.MapGet("/info", () => Results.Ok(new
{
    Name = "MCP Resource Server",
    Version = "1.0.0",
    Description = "OAuth2 protected resource server for MCP tools (migrated from Python)",
    ServerUrl = serverUrl,
    AuthzServerUrl = authzServerUrl,
    IntrospectionEnabled = enableIntrospection,
    Endpoints = new
    {
        MCP = "/",
        Health = "/health",
        Info = "/info",
        ProtectedResourceMetadata = "/.well-known/oauth-protected-resource"
    }
}));

// Auth config endpoint
app.MapGet("/auth/config", () => Results.Ok(new
{
    AuthzServerUrl = authzServerUrl,
    Realm = authzRealm,
    JwksUrl = authzJwksUrl,
    IntrospectionEnabled = enableIntrospection,
    IntrospectionEndpoint = enableIntrospection ? introspectionEndpoint : null,
    UserInfoEndpoint = userInfoEndpoint
})).AllowAnonymous();

// Startup logging
app.Logger.LogInformation("MCP Resource Server running on {ServerUrl}", serverUrl);
app.Logger.LogInformation("MCP authz server: {AuthzServerUrl}", authzServerUrl);
app.Logger.LogInformation("Database will be initialized on first use");

if (sslEnabled)
{
    app.Logger.LogDebug("HTTPS enabled with cert: {CertFile}", sslCertFile);
    app.Logger.LogDebug("Using key: {KeyFile}", sslKeyFile);
}
else
{
    app.Logger.LogInformation("Running in HTTP mode");
}

app.Run($"{protocol}://{serverHost}:{serverPort}");
