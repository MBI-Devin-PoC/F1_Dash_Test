using McpAuthzServer.Core.Models;

namespace McpAuthzServer.Core.Interfaces;

/// <summary>
/// Interface for token caching
/// </summary>
public interface ITokenCache
{
    void Add(AccessToken token);
    AccessToken? Get(string token);
    void Remove(string token);
    void Clear();
}
