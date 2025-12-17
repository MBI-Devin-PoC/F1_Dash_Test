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

## OAuth2/OpenID Connect Authentication with Keycloak

The server supports comprehensive OAuth2/OpenID Connect authentication using Keycloak. When configured, all MCP endpoints require authentication.

### Keycloak Configuration

Configure Keycloak in `appsettings.json`:

```json
{
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
| `Authority` | Keycloak server URL | Required |
| `Realm` | Keycloak realm name | Required |
| `ClientId` | Client ID for the MCP server | Required |
| `ClientSecret` | Client secret for token introspection | Required |
| `RequireHttpsMetadata` | Require HTTPS for metadata endpoints | `true` |
| `ValidateAudience` | Validate token audience | `true` |
| `ValidateIssuer` | Validate token issuer | `true` |
| `ValidateLifetime` | Validate token expiration | `true` |
| `EnableTokenIntrospection` | Enable real-time token validation | `true` |
| `TokenIntrospectionCacheSeconds` | Cache duration for introspection results | `60` |

### Token Introspection

When `EnableTokenIntrospection` is enabled, the server performs real-time token validation against Keycloak's introspection endpoint. This ensures:

- Tokens are validated against the authorization server
- Revoked tokens are immediately rejected
- Token claims are verified server-side

Introspection results are cached to reduce load on Keycloak.

### Authorization Policies

The server defines three authorization policies:

- `F1DataAccess` - Requires authenticated user (default for MCP endpoints)
- `F1AdminAccess` - Requires `f1-admin` role
- `F1ReadOnly` - Requires `f1:read` scope

### Authentication Endpoints

When Keycloak is configured, the following endpoints are available:

| Endpoint | Description |
|----------|-------------|
| `GET /auth/config` | Returns Keycloak configuration (public) |
| `GET /auth/introspect` | Introspects the provided Bearer token |
| `GET /auth/userinfo` | Returns authenticated user information |
| `GET /health` | Health check endpoint |

### Keycloak Setup

1. Create a new client in your Keycloak realm
2. Set the client access type to "confidential"
3. Enable "Service Accounts Enabled" for token introspection
4. Configure valid redirect URIs if using authorization code flow
5. Copy the client secret to your configuration

### Running Without Authentication

If the `Keycloak` section is not configured or `Authority` is empty, the server runs without authentication. This is useful for development and testing.

## License

This project is part of the F1 Dash project and follows the same license terms.
