using McpAuthzServer.Core.Models;

namespace McpAuthzServer.Core.Interfaces;

/// <summary>
/// Interface for token validation
/// </summary>
public interface ITokenValidator
{
    Task<AccessToken?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
}
