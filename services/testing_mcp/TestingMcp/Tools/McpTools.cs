using System.ComponentModel;
using ModelContextProtocol.Server;
using TestingMcp.Core.Interfaces;
using TestingMcp.Core.Models;
using TestingMcp.Services;

namespace TestingMcp.Tools;

[McpServerToolType]
public class McpTools
{
    private readonly ITokenCache _tokenCache;
    private readonly TokenExchangeService _tokenExchangeService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<McpTools> _logger;
    private readonly string _userInfoEndpoint;

    public McpTools(
        ITokenCache tokenCache,
        TokenExchangeService tokenExchangeService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<McpTools> logger,
        IConfiguration configuration)
    {
        _tokenCache = tokenCache;
        _tokenExchangeService = tokenExchangeService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _userInfoEndpoint = configuration["UserInfo:Endpoint"] ?? "https://sso/idp/userinfo.openid";
    }

    [McpServerTool, Description("A simple hello world tool.")]
    public string HelloWorld()
    {
        return "Hello, World!";
    }

    [McpServerTool, Description("Get the access token information of the authenticated user.")]
    public Dictionary<string, object> GetAccessTokenInfo()
    {
        var accessToken = GetCurrentAccessToken();

        if (accessToken == null)
        {
            return new Dictionary<string, object>
            {
                ["error"] = "No access token found",
                ["authenticated"] = false
            };
        }

        var maskedToken = MaskToken(accessToken.Token);
        _logger.LogInformation("Access token info: {MaskedToken}", maskedToken);

        return new Dictionary<string, object>
        {
            ["authenticated"] = true,
            ["token_value"] = maskedToken,
            ["client_id"] = accessToken.ClientId,
            ["scopes"] = accessToken.Scopes,
            ["expires_at"] = accessToken.ExpiresAt ?? 0
        };
    }

    [McpServerTool, Description("Get user information from GAS OIDC userinfo endpoint.")]
    public async Task<Dictionary<string, object>> GetGasOidcUserInfo()
    {
        var accessToken = GetCurrentAccessToken();

        if (accessToken == null)
        {
            return new Dictionary<string, object>
            {
                ["error"] = "No access token found"
            };
        }

        _logger.LogInformation("Using MCP token for exchange: {Token}", MaskToken(accessToken.Token));

        // Exchange MCP access token for GAS access token
        var gasToken = await _tokenExchangeService.ExchangeForGasTokenAsync(accessToken.Token);

        if (string.IsNullOrEmpty(gasToken))
        {
            return new Dictionary<string, object>
            {
                ["error"] = "Failed to exchange MCP token for GAS access token"
            };
        }

        // Get user info from GAS OIDC endpoint
        var userInfo = await _tokenExchangeService.GetUserInfoAsync(gasToken, _userInfoEndpoint);

        if (userInfo == null)
        {
            return new Dictionary<string, object>
            {
                ["error"] = "Failed to get user info from GAS OIDC endpoint"
            };
        }

        return userInfo;
    }

    private AccessToken? GetCurrentAccessToken()
    {
        // Try to get the client ID from the current user's claims
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var clientId = user.FindFirst("azp")?.Value ?? user.FindFirst("client_id")?.Value;
            if (!string.IsNullOrEmpty(clientId))
            {
                return _tokenCache.Get(clientId);
            }
        }

        // Try to get from Authorization header
        var authHeader = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader["Bearer ".Length..].Trim();
            return _tokenCache.GetByToken(token);
        }

        return null;
    }

    private static string MaskToken(string token, int visible = 4)
    {
        if (string.IsNullOrEmpty(token))
        {
            return "<empty>";
        }

        if (token.Length <= visible * 2)
        {
            return token;
        }

        return $"{token[..visible]}...{token[^visible..]}";
    }
}
