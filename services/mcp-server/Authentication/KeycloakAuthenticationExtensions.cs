using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace F1McpServer.Authentication;

public static class KeycloakAuthenticationExtensions
{
    public const string KeycloakScheme = "Keycloak";
    public const string JwtBearerScheme = JwtBearerDefaults.AuthenticationScheme;

    public static IServiceCollection AddKeycloakAuthentication(
        this IServiceCollection services,
        Action<KeycloakOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddMemoryCache();

        services.AddHttpClient<ITokenIntrospectionService, TokenIntrospectionService>()
            .ConfigureHttpClient((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<KeycloakOptions>>().Value;
                client.BaseAddress = new Uri(options.Authority);
                client.Timeout = TimeSpan.FromSeconds(30);
            });

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = KeycloakScheme;
            options.DefaultChallengeScheme = KeycloakScheme;
        })
        .AddScheme<KeycloakAuthenticationOptions, KeycloakAuthenticationHandler>(
            KeycloakScheme, 
            options => { });

        return services;
    }

    public static IServiceCollection AddKeycloakJwtBearerAuthentication(
        this IServiceCollection services,
        Action<KeycloakOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddMemoryCache();

        services.AddHttpClient<ITokenIntrospectionService, TokenIntrospectionService>()
            .ConfigureHttpClient((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<KeycloakOptions>>().Value;
                client.BaseAddress = new Uri(options.Authority);
                client.Timeout = TimeSpan.FromSeconds(30);
            });

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerScheme;
            options.DefaultChallengeScheme = JwtBearerScheme;
        })
        .AddJwtBearer(JwtBearerScheme, options =>
        {
            var sp = services.BuildServiceProvider();
            var keycloakOptions = sp.GetRequiredService<IOptions<KeycloakOptions>>().Value;

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
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    if (keycloakOptions.EnableTokenIntrospection)
                    {
                        var introspectionService = context.HttpContext.RequestServices
                            .GetRequiredService<ITokenIntrospectionService>();

                        var token = context.SecurityToken?.ToString();
                        if (!string.IsNullOrEmpty(token))
                        {
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
                        .GetRequiredService<ILogger<JwtBearerHandler>>();
                    logger.LogWarning(context.Exception, "JWT authentication failed");
                    return Task.CompletedTask;
                }
            };
        });

        return services;
    }

    public static IServiceCollection AddKeycloakHybridAuthentication(
        this IServiceCollection services,
        Action<KeycloakOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddMemoryCache();

        services.AddHttpClient<ITokenIntrospectionService, TokenIntrospectionService>()
            .ConfigureHttpClient((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<KeycloakOptions>>().Value;
                client.BaseAddress = new Uri(options.Authority);
                client.Timeout = TimeSpan.FromSeconds(30);
            });

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerScheme;
            options.DefaultChallengeScheme = JwtBearerScheme;
        })
        .AddJwtBearer(JwtBearerScheme, options =>
        {
            var sp = services.BuildServiceProvider();
            var keycloakOptions = sp.GetRequiredService<IOptions<KeycloakOptions>>().Value;

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
                        .GetRequiredService<ILogger<JwtBearerHandler>>();
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
            KeycloakScheme,
            options => { });

        return services;
    }
}
