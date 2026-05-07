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

    [McpServerTool(Name = "search_types_and_members", Title = "Search Types and Members", ReadOnly = true, OpenWorld = false), Description("Search for types and members (methods, properties, fields, events) in a .NET assembly by regex pattern. The pattern is a case-insensitive .NET regular expression matched against the type's full name and each member's simple name; it is unanchored, so plain substrings (e.g. 'Controller') work as before. Returns a JSON object with 'Types' and 'Members' arrays.")]
    public static string SearchTypesAndMembers(
        DecompilerService decompilerService,
        [Description("Full path to the .NET assembly (.dll or .exe)")] string assemblyPath,
        [Description("Case-insensitive .NET regex pattern. Matched against type full names and member simple names. Unanchored, so a plain substring works. Note that '.' matches any character — use '\\.' for a literal dot.")] string pattern)
    {
        try
        {
            var result = decompilerService.SearchTypesAndMembers(assemblyPath, pattern);
            return JsonSerializer.Serialize(
                new
                {
                    Types = result.Types.Select(t => new { t.Kind, t.FullName }),
                    Members = result.Members.Select(r => new { r.DeclaringType, r.MemberType, r.Name }),
                },
                s_jsonOptions);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "search_string", Title = "Search String", ReadOnly = true, OpenWorld = false), Description("Search for string literals in a .NET assembly. Scans IL `ldstr` instructions in every method body, and string-typed default values on fields, parameters, and properties. The pattern is a case-insensitive .NET regex (unanchored, so a plain substring works). Returns a JSON array of { DeclaringType, Member, MemberKind, Value } where MemberKind is one of 'Method' (IL ldstr), 'Field', 'Parameter', or 'Property'. Method members include a parenthesized parameter-type list to disambiguate overloads. Results are deduplicated.")]
    public static string SearchString(
        DecompilerService decompilerService,
        [Description("Full path to the .NET assembly (.dll or .exe)")] string assemblyPath,
        [Description("Case-insensitive .NET regex pattern matched against each candidate string literal. Unanchored, so a plain substring works. Note that '.' matches any character — use '\\.' for a literal dot.")] string pattern)
    {
        try
        {
            var results = decompilerService.SearchStrings(assemblyPath, pattern);
            if (results.Count == 0)
                return "[]";

            return JsonSerializer.Serialize(
                results.Select(r => new { r.DeclaringType, r.Member, r.MemberKind, r.Value }),
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
