using TestingMcp.Core.Models;

namespace TestingMcp.Core.Interfaces;

public interface ITokenCache
{
    void Add(AccessToken token);
    AccessToken? Get(string clientId);
    AccessToken? GetByToken(string token);
    void Remove(string clientId);
    void Clear();
}
