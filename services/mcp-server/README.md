# F1 MCP Server

A Model Context Protocol (MCP) server for Formula 1 data, built with C# using the official MCP SDK.

## Overview

This MCP server provides tools for accessing Formula 1 information including driver standings, constructor standings, race calendar, circuit information, and more. It can be integrated with AI assistants that support the Model Context Protocol.

## Prerequisites

- .NET 8.0 SDK or later
- An MCP-compatible client (e.g., Claude Desktop, VS Code with MCP extension)

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

The server communicates via stdio (standard input/output) as per the MCP specification.

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

## License

This project is part of the F1 Dash project and follows the same license terms.
