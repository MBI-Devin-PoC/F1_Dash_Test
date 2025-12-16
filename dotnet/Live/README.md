# Live Service (C#)

This is a C# ASP.NET Core port of the Rust `live` service. It connects to the F1 SignalR WebSocket endpoint (or the simulator) and maintains the full current state, then serves it via SSE to frontend clients.

## Requirements

- .NET 8.0 SDK

## Usage

```bash
cd dotnet/Live
dotnet run
```

## Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `LIVE_ADDRESS` | The address and port where the service starts | `0.0.0.0:4000` |
| `ORIGIN` | The allowed origins for CORS (semicolon-separated) | `http://localhost:3000` |
| `WS_URL` | Custom WebSocket URL (for simulator) | - |

## Endpoints

- `GET /api/health` - Health check endpoint
- `GET /api/sse` - Server-Sent Events stream for real-time data
- `GET /api/drivers` - Get the current driver list

## Architecture

The service consists of:

- **F1Client**: Connects to the F1 SignalR WebSocket, negotiates connection, subscribes to data feeds, and parses incoming messages
- **StateManager**: Maintains the current state by merging updates, broadcasts to SSE subscribers
- **SSE Endpoint**: Streams initial state and updates to connected clients with optional gzip compression

## Protocol Compatibility

This C# implementation is designed to be a drop-in replacement for the Rust `live` service. It:

- Uses the same SSE event names (`initial`, `update`)
- Produces the same JSON structure (camelCase keys)
- Supports gzip compression when client accepts it
- Maintains the same endpoint paths (`/api/health`, `/api/sse`, `/api/drivers`)
