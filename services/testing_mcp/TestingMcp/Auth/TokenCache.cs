using Microsoft.Extensions.Caching.Memory;
using TestingMcp.Core.Interfaces;
using TestingMcp.Core.Models;

namespace TestingMcp.Auth;

public class TokenCache : ITokenCache
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<TokenCache> _logger;
    private readonly int _maxSize;
    private readonly HashSet<string> _keys = new();
    private readonly object _lock = new();

    public TokenCache(IMemoryCache cache, ILogger<TokenCache> logger, int maxSize = 100)
    {
        _cache = cache;
        _logger = logger;
        _maxSize = maxSize;
    }

    public void Add(AccessToken token)
    {
        if (string.IsNullOrEmpty(token.ClientId)) return;

        lock (_lock)
        {
            // Maintain size limit by removing oldest entries
            while (_keys.Count >= _maxSize)
            {
                var oldestKey = _keys.First();
                _cache.Remove($"client:{oldestKey}");
                _cache.Remove($"token:{oldestKey}");
                _keys.Remove(oldestKey);
            }

            var expiration = token.ExpiresAt.HasValue
                ? DateTimeOffset.FromUnixTimeSeconds(token.ExpiresAt.Value)
                : DateTimeOffset.UtcNow.AddHours(1);

            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = expiration
            };

            _cache.Set($"client:{token.ClientId}", token, options);
            _cache.Set($"token:{token.Token}", token, options);
            _keys.Add(token.ClientId);

            _logger.LogDebug("Cached access token for client_id: {ClientId}", token.ClientId);
        }
    }

    public AccessToken? Get(string clientId)
    {
        return _cache.TryGetValue($"client:{clientId}", out AccessToken? token) ? token : null;
    }

    public AccessToken? GetByToken(string tokenValue)
    {
        return _cache.TryGetValue($"token:{tokenValue}", out AccessToken? token) ? token : null;
    }

    public void Remove(string clientId)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue($"client:{clientId}", out AccessToken? token))
            {
                _cache.Remove($"client:{clientId}");
                _cache.Remove($"token:{token!.Token}");
                _keys.Remove(clientId);
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            foreach (var key in _keys)
            {
                if (_cache.TryGetValue($"client:{key}", out AccessToken? token))
                {
                    _cache.Remove($"client:{key}");
                    _cache.Remove($"token:{token!.Token}");
                }
            }
            _keys.Clear();
        }
    }
}
