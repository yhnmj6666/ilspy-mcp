# Copilot Instructions for ILSpy MCP Server

## Build

```bash
dotnet build
```

No tests or linter configured.

## Architecture

This is an MCP (Model Context Protocol) server that wraps the ILSpy decompiler engine, exposing .NET assembly analysis as tools for AI agents over stdio.

**Layers:**

- `Program.cs` — Hosts the MCP server on a dedicated 16MB-stack thread (required by the decompiler for deeply nested IL). Uses `Microsoft.Extensions.Hosting` with `AddMcpServer()` + `WithStdioServerTransport()`. Tools are auto-discovered via `WithToolsFromAssembly()`.
- `Services/DecompilerService.cs` — Singleton service injected into all tools. Wraps `ICSharpCode.Decompiler` APIs: `CSharpDecompiler` for C# output, `ReflectionDisassembler` for IL output, and raw `System.Reflection.Metadata` for type/member enumeration. Caches `PEFile` instances per assembly path in a `ConcurrentDictionary`.
- `Tools/*.cs` — MCP tool classes grouped by function (decompilation, listing, IL, assembly info). Each is a static method class discovered at startup.

**Data flow for a tool call:** MCP JSON-RPC request → stdio transport → DI resolves `DecompilerService` → tool method loads/caches assembly → returns string result → JSON-RPC response on stdout.

## Conventions

### Adding a new MCP tool

1. Create or extend a class in `Tools/` with `[McpServerToolType]`
2. Add a `public static string` method with `[McpServerTool(Name = "tool_name")]` and `[Description("...")]`
3. Use `DecompilerService` as the first parameter (injected by the MCP SDK via DI)
4. Add `[Description("...")]` to every parameter for the tool's JSON schema
5. Wrap the body in try/catch returning `$"Error: {ex.Message}"` — tool methods should not throw

### CSharpDecompiler thread safety

`CSharpDecompiler` instances are **not thread-safe**. `DecompilerService.CreateDecompiler()` creates a new instance per call. Only `PEFile` objects are cached and shared.

### MCP SDK attributes

This project uses the official `ModelContextProtocol` NuGet package (v1.0.0). The attribute names are:
- `[McpServerToolType]` on the class (not `[McpToolType]`)
- `[McpServerTool(Name = "...")]` on methods — name is set via property, not constructor argument
- `[Description("...")]` from `System.ComponentModel` for both tool and parameter descriptions
