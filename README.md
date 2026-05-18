# ILSpy MCP Server

An MCP (Model Context Protocol) server that exposes [ILSpy](https://github.com/icsharpcode/ILSpy)'s .NET decompilation capabilities as tools for AI agents.

## Tools

| Tool | Description |
|------|-------------|
| `decompile_type` | Decompile a specific .NET type to C# source code |
| `decompile_member` | Decompile a single member (method/property/field/event) to C# source code |
| `list_types` | List all types in an assembly (with optional namespace filter) |
| `list_members` | List methods, properties, fields, and events of a type |
| `list_namespaces` | List all namespaces defined in an assembly |
| `get_il` | Get IL disassembly for a type or method |
| `search_types_and_members` | Search for types and members by regex pattern (case-insensitive) |
| `search_string` | Search for string literals (IL `ldstr` and metadata constants) by regex |
| `find_implementations` | Find types that implement an interface or extend a base class |
| `find_references` | Find IL-level cross-references (callers/users) of a member or type across assemblies |
| `get_assembly_info` | Get assembly metadata (name, version, framework, references) |
| `get_search_paths` / `set_search_paths` | Get / set assembly search paths used for cross-assembly resolution |
| `list_loaded_assemblies` | List assemblies currently held in the in-memory cache |
| `reset_cache` | Evict cached assemblies so they are reloaded from disk |

## Build

```bash
dotnet build
```

## Configuration

### VS Code (GitHub Copilot)

Add to your `.vscode/settings.json` or user settings:

```json
{
  "mcp": {
    "servers": {
      "ilspy": {
        "type": "stdio",
        "command": "dotnet",
        "args": ["run", "--project", "/path/to/ilspy-mcp/src/ILSpyMcp"]
      }
    }
  }
}
```

Or use the compiled binary directly:

```json
{
  "mcp": {
    "servers": {
      "ilspy": {
        "type": "stdio",
        "command": "/path/to/ilspy-mcp/src/ILSpyMcp/bin/Debug/net9.0/ILSpyMcp.exe"
      }
    }
  }
}
```

### Claude Desktop

Add to `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "ilspy": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/ilspy-mcp/src/ILSpyMcp"]
    }
  }
}
```

## Usage Examples

Once connected, the AI agent can use these tools to analyze .NET assemblies:

- **List types**: "What types are in `MyLibrary.dll`?"
- **Decompile**: "Show me the source code of `MyNamespace.MyClass`"
- **Get IL**: "Show the IL code for the `Process` method in `MyNamespace.MyClass`"
- **Search**: "Find all types related to 'Controller' in this assembly"
- **Find references**: "Where is `MyLibrary.MyType.DoWork` called from?" — set search paths to the consumer assemblies' directory first, then call `find_references`

## Tech Stack

- .NET 9.0
- [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) v1.0.0 (Official C# MCP SDK)
- [ICSharpCode.Decompiler](https://www.nuget.org/packages/ICSharpCode.Decompiler) v9.1.0 (ILSpy decompiler engine)
