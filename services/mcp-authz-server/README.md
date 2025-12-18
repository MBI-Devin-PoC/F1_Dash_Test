# MCP Authorization Server

A Model Context Protocol (MCP) server with OAuth2/OpenID Connect authentication using Keycloak and Dynamic Client Registration (DCR) support.

## Features

- MCP-compliant OAuth2 authentication with Protected Resource Metadata (RFC 9728)
- Dynamic Client Registration (DCR) support for automatic client registration
- JWT token validation via Keycloak JWKS
- Optional token introspection for opaque tokens
- Token caching for improved performance
- HelloWorld tool for testing

## Prerequisites

- .NET 8.0 SDK
- Keycloak server (for authentication)

## Quick Start

### Without Authentication

```bash
cd McpAuthzServer
dotnet run
```

The server will start at `http://localhost:5000` without authentication.

### With Keycloak Authentication

1. Configure `appsettings.Development.json`:

```json
{
  "Keycloak": {
    "Authority": "http://localhost:8080",
    "Realm": "mcp-realm"
  }
}
```

2. Run with Development environment:

```bash
dotnet run --environment Development
```

## Configuration

### appsettings.json

```json
{
  "McpServer": {
    "Host": "0.0.0.0",
    "Port": "5000",
    "ServerName": "localhost",
    "Protocol": "http"
  },
  "TokenCache": {
    "MaxSize": "100"
  },
  "Keycloak": {
    "Authority": "http://localhost:8080",
    "Realm": "mcp-realm"
  },
  "Introspection": {
    "Endpoint": "",
    "ClientId": "",
    "ClientSecret": ""
  }
}
```

### Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| `McpServer:Host` | Server bind address | `0.0.0.0` |
| `McpServer:Port` | Server port | `5000` |
| `McpServer:ServerName` | Server hostname for URLs | `localhost` |
| `McpServer:Protocol` | HTTP or HTTPS | `http` |
| `TokenCache:MaxSize` | Maximum cached tokens | `100` |
| `Keycloak:Authority` | Keycloak base URL | - |
| `Keycloak:Realm` | Keycloak realm name | - |
| `Introspection:Endpoint` | Token introspection endpoint (optional) | - |
| `Introspection:ClientId` | Introspection client ID (optional) | - |
| `Introspection:ClientSecret` | Introspection client secret (optional) | - |

## Endpoints

| Endpoint | Description |
|----------|-------------|
| `/` | MCP Streamable HTTP endpoint |
| `/health` | Health check |
| `/info` | Server information |
| `/.well-known/oauth-protected-resource` | OAuth2 Protected Resource Metadata |
| `/auth/config` | Authentication configuration |
| `/auth/introspect` | Token introspection (debug) |
| `/auth/userinfo` | User information |

## Keycloak Setup for DCR

1. Create a realm (e.g., `mcp-realm`)
2. Enable Dynamic Client Registration:
   - Realm Settings > Client Registration > Anonymous Access Policies
   - Add Trusted Hosts: `localhost`, `127.0.0.1`
3. (Optional) Add Audience Mapper:
   - Client Scopes > Create `mcp-audience`
   - Add Mapper: Type=Audience, Value=`http://localhost:5000`
   - Add to default scopes

## MCP Client Configuration

For VS Code or other MCP clients:

```json
{
  "mcpServers": {
    "mcp-authz-server": {
      "type": "http",
      "url": "http://localhost:5000"
    }
  }
}
```

## OAuth2 Flow

1. Client connects to MCP server
2. Server returns 401 with `WWW-Authenticate: Bearer resource_metadata="/.well-known/oauth-protected-resource"`
3. Client fetches Protected Resource Metadata to discover Keycloak URL
4. Client registers via DCR (if enabled) or uses pre-configured client
5. User authenticates via browser (Authorization Code + PKCE)
6. Client uses access token for authenticated MCP requests

## Project Structure

```
McpAuthzServer/
├── Auth/
│   ├── TokenCache.cs
│   └── Validators/
│       ├── DelegatingTokenValidator.cs
│       ├── IntrospectionTokenValidator.cs
│       └── JwtTokenValidator.cs
├── Core/
│   ├── Interfaces/
│   │   ├── ITokenCache.cs
│   │   └── ITokenValidator.cs
│   └── Models/
│       ├── AccessToken.cs
│       ├── IntrospectionResponse.cs
│       └── TokenExchangeResponse.cs
├── Tools/
│   └── HelloWorldTools.cs
├── Program.cs
├── appsettings.json
└── appsettings.Development.json
```

## License

MIT
