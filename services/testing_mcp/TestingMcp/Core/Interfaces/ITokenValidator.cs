using TestingMcp.Core.Models;

namespace TestingMcp.Core.Interfaces;

public interface ITokenValidator
{
    Task<AccessToken?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
}
