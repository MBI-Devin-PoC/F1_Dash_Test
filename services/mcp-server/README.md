# F1 MCP Server

A Model Context Protocol (MCP) server for Formula 1 data, built with C# using the official MCP SDK. Features comprehensive OAuth2/OpenID Connect authentication with Keycloak and token introspection.

## Overview

This MCP server provides tools for accessing Formula 1 information including driver standings, constructor standings, race calendar, circuit information, and more. It can be integrated with AI assistants that support the Model Context Protocol.

## Features

- 8 F1-related tools for accessing race data, standings, and statistics
- OAuth2/OpenID Connect authentication with Keycloak
- Token introspection for real-time token validation
- JWT Bearer authentication with configurable validation
- Role-based authorization policies
- Caching for token introspection results
- Health check endpoint

## Prerequisites

- .NET 8.0 SDK or later
- An MCP-compatible client (e.g., Claude Desktop, VS Code with MCP extension)
- Keycloak server (optional, for authentication)

## Building

```bash
cd services/mcp-server
dotnet restore
dotnet build
```

## Running

```bash
dotnet run
```

The server runs as an HTTP server with MCP endpoints. Without Keycloak configuration, it runs without authentication.

## Available Tools

### GetSeasonInfo
Gets information about the current F1 season including race calendar and upcoming events.

### GetDriverStandings
Gets the current driver standings for the F1 championship.

### GetConstructorStandings
Gets the constructor (team) standings for the F1 championship.

### GetCircuitInfo
Gets information about a specific F1 circuit by name.
- **Parameters**: `circuitName` - The name of the circuit (e.g., 'Monaco', 'Silverstone', 'Spa')

### GetDriverInfo
Gets information about a specific F1 driver by name.
- **Parameters**: `driverName` - The name of the driver (e.g., 'Verstappen', 'Hamilton', 'Leclerc')

### GetRaceCalendar
Gets the race calendar for the F1 season with all scheduled races.

### CalculatePoints
Calculates championship points based on race position.
- **Parameters**:
  - `position` - The finishing position in the race (1-20)
  - `fastestLap` - Whether the driver set the fastest lap (optional, default: false)
  - `isSprint` - Whether this is a sprint race (optional, default: false)

### GetTireInfo
Gets tire compound information and their characteristics.
- **Parameters**: `compound` - The tire compound (soft, medium, hard, intermediate, wet, or 'all')

## Configuration for MCP Clients

### Claude Desktop

Add the following to your Claude Desktop configuration file:

**macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`
**Windows**: `%APPDATA%\Claude\claude_desktop_config.json`

```json
{
  "mcpServers": {
    "f1-mcp-server": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/services/mcp-server/F1McpServer.csproj"]
    }
  }
}
```

Or if you've published the executable:

```json
{
  "mcpServers": {
    "f1-mcp-server": {
      "command": "/path/to/F1McpServer"
    }
  }
}
```

## MCP-Compliant OAuth2/OpenID Connect Authentication

This server implements the MCP Authorization Specification with OAuth2/OpenID Connect authentication using Keycloak. It supports Dynamic Client Registration (DCR) allowing MCP clients like VS Code to automatically register with Keycloak and receive dynamically generated client IDs.

### MCP Authorization Flow

The server follows the MCP authorization specification with these steps:

1. **Initial Handshake**: Client connects to the MCP server. Server responds with 401 and WWW-Authenticate header containing `resource_metadata` parameter pointing to the Protected Resource Metadata endpoint.

2. **Protected Resource Metadata Discovery**: Client fetches `/.well-known/oauth-protected-resource` to discover the authorization server URL and supported scopes.

3. **Authorization Server Discovery**: Client fetches Keycloak's OpenID Connect discovery document at `/.well-known/openid-configuration`.

4. **Dynamic Client Registration**: Client registers itself with Keycloak via the DCR endpoint, receiving a dynamically generated client ID.

5. **User Authorization**: User is redirected to Keycloak login page. After authentication, client receives an authorization code via PKCE flow.

6. **Authenticated Requests**: Client includes the access token in the Authorization header for all subsequent requests.

### Server Configuration

Configure the server in `appsettings.json`:

```json
{
  "ServerUrl": "http://localhost:3001",
  "Keycloak": {
    "Authority": "https://your-keycloak-server.com",
    "Realm": "your-realm",
    "ClientId": "f1-mcp-server",
    "ClientSecret": "your-client-secret",
    "RequireHttpsMetadata": true,
    "ValidateAudience": true,
    "ValidateIssuer": true,
    "ValidateLifetime": true,
    "EnableTokenIntrospection": true,
    "TokenIntrospectionCacheSeconds": 60
  }
}
```

### Configuration Options

| Option | Description | Default |
|--------|-------------|---------|
| `ServerUrl` | MCP server URL (used as resource identifier and token audience) | `http://localhost:3001` |
| `Authority` | Keycloak server URL | Required |
| `Realm` | Keycloak realm name | Required |
| `ClientId` | Client ID for the MCP server (used for token introspection) | Required |
| `ClientSecret` | Client secret for token introspection | Required |
| `RequireHttpsMetadata` | Require HTTPS for metadata endpoints | `true` |
| `ValidateAudience` | Validate token audience | `true` |
| `ValidateIssuer` | Validate token issuer | `true` |
| `ValidateLifetime` | Validate token expiration | `true` |
| `EnableTokenIntrospection` | Enable real-time token validation | `true` |
| `TokenIntrospectionCacheSeconds` | Cache duration for introspection results | `60` |

### Keycloak Setup for Dynamic Client Registration

To enable MCP clients to automatically register with Keycloak:

1. **Enable Client Registration in Keycloak**:
   - Go to Realm Settings > Client Registration
   - Under "Initial Access Tokens" tab, create a new token (optional, for authenticated DCR)
   - Or configure "Anonymous Access Policies" for public DCR

2. **Configure Trusted Hosts** (for anonymous DCR):
   - Go to Realm Settings > Client Registration > Anonymous Access Policies
   - Add a policy with:
     - Protocol: openid-connect
     - Client Scope: Leave empty or specify allowed scopes
     - Trusted Hosts: Add your MCP client hosts (e.g., `localhost`, `127.0.0.1`)

3. **Create Custom Scopes** (optional):
   - Go to Client Scopes > Create
   - Create scopes like `mcp:tools`, `f1:read`, `f1:write`
   - These scopes will be advertised in the Protected Resource Metadata

4. **Configure Audience Mapper** (important for token validation):
   - For dynamically registered clients, tokens need the correct audience
   - Go to Client Scopes > Create a new scope (e.g., `f1-mcp-audience`)
   - Add a mapper: Type = "Audience", Included Custom Audience = your ServerUrl (e.g., `http://localhost:3001`)
   - Add this scope as a default scope in Realm Settings > Client Scopes > Default Client Scopes

### Protected Resource Metadata Endpoint

The server exposes `/.well-known/oauth-protected-resource` which returns:

```json
{
  "resource": "http://localhost:3001",
  "authorization_servers": ["https://your-keycloak-server.com/realms/your-realm"],
  "scopes_supported": ["mcp:tools", "f1:read", "f1:write", "openid", "profile", "email"],
  "resource_name": "F1 MCP Server",
  "resource_documentation": "https://github.com/MBI-Devin-PoC/F1_Dash_Test"
}
```

### Token Introspection

When `EnableTokenIntrospection` is enabled, the server performs real-time token validation against Keycloak's introspection endpoint. This ensures tokens are validated against the authorization server, revoked tokens are immediately rejected, and token claims are verified server-side. Introspection results are cached to reduce load on Keycloak.

### Authorization Policies

The server defines three authorization policies:

- `F1DataAccess` - Requires authenticated user (default for MCP endpoints)
- `F1AdminAccess` - Requires `f1-admin` role
- `F1ReadOnly` - Requires `f1:read` scope

### Authentication Endpoints

When Keycloak is configured, the following endpoints are available:

| Endpoint | Description |
|----------|-------------|
| `GET /.well-known/oauth-protected-resource` | Protected Resource Metadata (MCP spec) |
| `GET /auth/config` | Returns Keycloak configuration (public) |
| `GET /auth/introspect` | Introspects the provided Bearer token |
| `GET /auth/userinfo` | Returns authenticated user information |
| `GET /health` | Health check endpoint |

### Running Without Authentication

If the `Keycloak` section is not configured or `Authority` is empty, the server runs without authentication. This is useful for development and testing.

## License

This project is part of the F1 Dash project and follows the same license terms.
