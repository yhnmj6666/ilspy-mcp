using System.ComponentModel;
using System.Text.Json;

using ILSpyMcp.Services;

using ModelContextProtocol.Server;

namespace ILSpyMcp.Tools;

[McpServerToolType]
public sealed class ListingTools
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    [McpServerTool(Name = "list_types", Title = "List Types", ReadOnly = true, OpenWorld = false), Description("List all types defined in a .NET assembly, optionally filtered by namespace. Returns a JSON array.")]
    public static string ListTypes(
        DecompilerService decompilerService,
        [Description("Full path to the .NET assembly (.dll or .exe)")] string assemblyPath,
        [Description("Optional namespace prefix to filter types (e.g. 'System.Collections')")] string? namespaceFilter = null)
    {
        try
        {
            var types = decompilerService.ListTypes(assemblyPath, namespaceFilter);
            if (types.Count == 0)
                return "[]";

            return JsonSerializer.Serialize(
                types.Select(t => new { t.Kind, t.FullName }),
                s_jsonOptions);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "list_members", Title = "List Members", ReadOnly = true, OpenWorld = false), Description("List all members (methods, properties, fields, events) of one or more types in a .NET assembly. Returns a JSON array.")]
    public static string ListMembers(
        DecompilerService decompilerService,
        [Description("Full path to the .NET assembly (.dll or .exe)")] string assemblyPath,
        [Description("Fully qualified type name(s) (e.g. 'MyNamespace.MyClass'). Pass a single name or an array for batch listing.")] string[] typeNames)
    {
        var results = new List<object>();
        foreach (var typeName in typeNames)
        {
            try
            {
                var members = decompilerService.ListMembers(assemblyPath, typeName);
                results.Add(new
                {
                    TypeName = typeName,
                    Members = members.Select(m => new { m.MemberType, m.Name, m.Signature })
                });
            }
            catch (Exception ex)
            {
                results.Add(new { TypeName = typeName, Error = ex.Message });
            }
        }
        return JsonSerializer.Serialize(results, s_jsonOptions);
    }

    [McpServerTool(Name = "search_types", Title = "Search Types", ReadOnly = true, OpenWorld = false), Description("Search for types in a .NET assembly by name pattern (case-insensitive substring match). Returns a JSON array.")]
    public static string SearchTypes(
        DecompilerService decompilerService,
        [Description("Full path to the .NET assembly (.dll or .exe)")] string assemblyPath,
        [Description("Search pattern to match against type names (case-insensitive)")] string pattern)
    {
        try
        {
            var types = decompilerService.SearchTypes(assemblyPath, pattern);
            if (types.Count == 0)
                return "[]";

            return JsonSerializer.Serialize(
                types.Select(t => new { t.Kind, t.FullName }),
                s_jsonOptions);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "search_members", Title = "Search Members", ReadOnly = true, OpenWorld = false), Description("Search for members (methods, properties, fields, events) by name across all types in a .NET assembly. Case-insensitive substring match. Returns a JSON array.")]
    public static string SearchMembers(
        DecompilerService decompilerService,
        [Description("Full path to the .NET assembly (.dll or .exe)")] string assemblyPath,
        [Description("Search pattern to match against member names (case-insensitive)")] string pattern)
    {
        try
        {
            var results = decompilerService.SearchMembers(assemblyPath, pattern);
            if (results.Count == 0)
                return "[]";

            return JsonSerializer.Serialize(
                results.Select(r => new { r.DeclaringType, r.MemberType, r.Name }),
                s_jsonOptions);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "list_namespaces", Title = "List Namespaces", ReadOnly = true, OpenWorld = false), Description("List all namespaces defined in a .NET assembly. Returns a JSON array.")]
    public static string ListNamespaces(
        DecompilerService decompilerService,
        [Description("Full path to the .NET assembly (.dll or .exe)")] string assemblyPath)
    {
        try
        {
            var namespaces = decompilerService.ListNamespaces(assemblyPath);
            if (namespaces.Count == 0)
                return "[]";

            return JsonSerializer.Serialize(namespaces, s_jsonOptions);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "find_implementations", Title = "Find Implementations", ReadOnly = true, OpenWorld = false), Description("Find types that implement a specific interface or extend a specific base class. Searches a single assembly, or all assemblies across configured search paths if no assembly is specified. Returns a JSON array.")]
    public static string FindImplementations(
        DecompilerService decompilerService,
        [Description("Base class or interface name to search for (e.g. 'IDisposable', 'Controller')")] string baseOrInterfaceName,
        [Description("Optional full path to a .NET assembly. If omitted, searches all assemblies in configured search paths.")] string? assemblyPath = null)
    {
        try
        {
            var types = decompilerService.FindImplementations(assemblyPath, baseOrInterfaceName);
            if (types.Count == 0)
                return "[]";

            return JsonSerializer.Serialize(
                types.Select(t => new { t.Kind, t.FullName, Assembly = t.AssemblyName }),
                s_jsonOptions);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
