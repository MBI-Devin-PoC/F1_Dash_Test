using McpAuthzServer.Core.Interfaces;
using McpAuthzServer.Core.Models;
using Microsoft.Extensions.Caching.Memory;

namespace McpAuthzServer.Auth;

/// <summary>
/// In-memory token cache implementation
/// </summary>
public class TokenCache : ITokenCache
{
    private readonly MemoryCache _cache;
    private readonly int _maxSize;

    public TokenCache(int maxSize = 100)
    {
        _maxSize = maxSize;
        _cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = maxSize
        });
    }

    public void Add(AccessToken token)
    {
        var options = new MemoryCacheEntryOptions
        {
            Size = 1,
            AbsoluteExpiration = token.ExpiresAt.HasValue
                ? DateTimeOffset.FromUnixTimeSeconds(token.ExpiresAt.Value)
                : DateTimeOffset.UtcNow.AddHours(1)
        };
        _cache.Set(token.Token, token, options);
    }

    public AccessToken? Get(string token)
    {
        return _cache.TryGetValue(token, out AccessToken? accessToken) ? accessToken : null;
    }

    public void Remove(string token)
    {
        _cache.Remove(token);
    }

    public void Clear()
    {
        _cache.Clear();
    }
}
