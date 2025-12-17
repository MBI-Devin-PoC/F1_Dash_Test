using F1McpServer.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

var keycloakSection = builder.Configuration.GetSection(KeycloakOptions.SectionName);
var keycloakEnabled = keycloakSection.Exists() && !string.IsNullOrEmpty(keycloakSection["Authority"]);

if (keycloakEnabled)
{
    builder.Services.Configure<KeycloakOptions>(keycloakSection);
    builder.Services.AddMemoryCache();

    builder.Services.AddHttpClient<ITokenIntrospectionService, TokenIntrospectionService>()
        .ConfigureHttpClient((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<KeycloakOptions>>().Value;
            client.BaseAddress = new Uri(options.Authority);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        var keycloakOptions = keycloakSection.Get<KeycloakOptions>()!;

        options.Authority = keycloakOptions.RealmUrl;
        options.Audience = keycloakOptions.ClientId;
        options.RequireHttpsMetadata = keycloakOptions.RequireHttpsMetadata;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = keycloakOptions.ValidateIssuer,
            ValidIssuer = keycloakOptions.RealmUrl,
            ValidateAudience = keycloakOptions.ValidateAudience,
            ValidAudience = keycloakOptions.ClientId,
            ValidateLifetime = keycloakOptions.ValidateLifetime,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = "realm_access.roles",
            NameClaimType = "preferred_username"
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                if (keycloakOptions.EnableTokenIntrospection)
                {
                    var introspectionService = context.HttpContext.RequestServices
                        .GetRequiredService<ITokenIntrospectionService>();

                    var authHeader = context.HttpContext.Request.Headers.Authorization.ToString();
                    if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        var token = authHeader["Bearer ".Length..].Trim();
                        var result = await introspectionService.IntrospectTokenAsync(token);
                        if (!result.Active)
                        {
                            context.Fail("Token introspection indicates token is not active");
                        }
                    }
                }
            },
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<Program>>();
                logger.LogWarning(context.Exception, "JWT authentication failed");
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                var keycloakOpts = context.HttpContext.RequestServices
                    .GetRequiredService<IOptions<KeycloakOptions>>().Value;
                context.Response.Headers.Append("WWW-Authenticate",
                    $"Bearer realm=\"{keycloakOpts.Realm}\", " +
                    $"authorization_uri=\"{keycloakOpts.RealmUrl}/protocol/openid-connect/auth\"");
                return Task.CompletedTask;
            }
        };
    })
    .AddScheme<KeycloakAuthenticationOptions, KeycloakAuthenticationHandler>(
        KeycloakAuthenticationExtensions.KeycloakScheme,
        options => { });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("F1DataAccess", policy =>
            policy.RequireAuthenticatedUser());

        options.AddPolicy("F1AdminAccess", policy =>
            policy.RequireRole("f1-admin"));

        options.AddPolicy("F1ReadOnly", policy =>
            policy.RequireAuthenticatedUser()
                  .RequireClaim("scope", "f1:read"));
    });
}

builder.Services.AddHttpClient();

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

if (keycloakEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapMcp().RequireAuthorization("F1DataAccess");

    app.MapGet("/auth/config", (IOptions<KeycloakOptions> options) =>
    {
        var config = options.Value;
        return Results.Ok(new
        {
            Authority = config.Authority,
            Realm = config.Realm,
            ClientId = config.ClientId,
            OpenIdConfigurationEndpoint = config.OpenIdConfigurationEndpoint,
            TokenEndpoint = config.TokenEndpoint,
            UserInfoEndpoint = config.UserInfoEndpoint
        });
    }).AllowAnonymous();

    app.MapGet("/auth/introspect", async (
        HttpContext context,
        ITokenIntrospectionService introspectionService) =>
    {
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { Error = "Missing or invalid Authorization header" });
        }

        var token = authHeader["Bearer ".Length..].Trim();
        var result = await introspectionService.IntrospectTokenAsync(token);

        return Results.Ok(new
        {
            result.Active,
            result.Username,
            result.Email,
            result.Scope,
            result.ClientId,
            Roles = result.RealmAccess?.Roles,
            ExpiresAt = result.ExpirationTime.HasValue
                ? DateTimeOffset.FromUnixTimeSeconds(result.ExpirationTime.Value).ToString("o")
                : null
        });
    });

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
    app.Logger.LogInformation("Authority: {Authority}", keycloakSection["Authority"]);
    app.Logger.LogInformation("Realm: {Realm}", keycloakSection["Realm"]);
}
else
{
    app.MapMcp();
    app.Logger.LogWarning("Keycloak authentication is not configured. Running without authentication.");
    app.Logger.LogWarning("To enable authentication, configure the 'Keycloak' section in appsettings.json");
}

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }));

app.MapGet("/", () => Results.Ok(new
{
    Name = "F1 MCP Server",
    Version = "1.0.0",
    Description = "Model Context Protocol server for Formula 1 data",
    AuthenticationEnabled = keycloakEnabled,
    Endpoints = new
    {
        MCP = "/mcp",
        Health = "/health",
        AuthConfig = keycloakEnabled ? "/auth/config" : null,
        AuthIntrospect = keycloakEnabled ? "/auth/introspect" : null,
        AuthUserInfo = keycloakEnabled ? "/auth/userinfo" : null
    }
}));

await app.RunAsync();
