using System.ComponentModel;
using System.Text;

using ILSpyMcp.Services;

using ModelContextProtocol.Server;

namespace ILSpyMcp.Tools;

[McpServerToolType]
public sealed class ListingTools
{
    [McpServerTool(Name = "list_types"), Description("List all types defined in a .NET assembly, optionally filtered by namespace.")]
    public static string ListTypes(
        DecompilerService decompilerService,
        [Description("Full path to the .NET assembly (.dll or .exe)")] string assemblyPath,
        [Description("Optional namespace prefix to filter types (e.g. 'System.Collections')")] string? namespaceFilter = null)
    {
        try
        {
            var types = decompilerService.ListTypes(assemblyPath, namespaceFilter);
            if (types.Count == 0)
                return "No types found.";

            var sb = new StringBuilder();
            sb.AppendLine($"Found {types.Count} type(s):");
            sb.AppendLine();
            foreach (var type in types)
            {
                sb.AppendLine($"  [{type.Kind}] {type.FullName}");
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "list_members"), Description("List all members (methods, properties, fields, events) of one or more types in a .NET assembly.")]
    public static string ListMembers(
        DecompilerService decompilerService,
        [Description("Full path to the .NET assembly (.dll or .exe)")] string assemblyPath,
        [Description("Fully qualified type name(s) (e.g. 'MyNamespace.MyClass'). Pass a single name or an array for batch listing.")] string[] typeNames)
    {
        var sb = new StringBuilder();
        foreach (var typeName in typeNames)
        {
            try
            {
                var members = decompilerService.ListMembers(assemblyPath, typeName);
                sb.AppendLine($"Members of {typeName} ({members.Count} total):");
                sb.AppendLine();

                if (members.Count == 0)
                {
                    sb.AppendLine("  (none)");
                    sb.AppendLine();
                    continue;
                }

                foreach (var group in members.GroupBy(m => m.MemberType))
                {
                    sb.AppendLine($"  {group.Key}s:");
                    foreach (var member in group)
                    {
                        sb.AppendLine($"    {member.Signature}");
                    }
                    sb.AppendLine();
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Members of {typeName}: Error: {ex.Message}");
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

    [McpServerTool(Name = "search_types"), Description("Search for types in a .NET assembly by name pattern (case-insensitive substring match).")]
    public static string SearchTypes(
        DecompilerService decompilerService,
        [Description("Full path to the .NET assembly (.dll or .exe)")] string assemblyPath,
        [Description("Search pattern to match against type names (case-insensitive)")] string pattern)
    {
        try
        {
            var types = decompilerService.SearchTypes(assemblyPath, pattern);
            if (types.Count == 0)
                return $"No types matching '{pattern}' found.";

            var sb = new StringBuilder();
            sb.AppendLine($"Found {types.Count} type(s) matching '{pattern}':");
            sb.AppendLine();
            foreach (var type in types)
            {
                sb.AppendLine($"  [{type.Kind}] {type.FullName}");
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "search_members"), Description("Search for members (methods, properties, fields, events) by name across all types in a .NET assembly. Case-insensitive substring match.")]
    public static string SearchMembers(
        DecompilerService decompilerService,
        [Description("Full path to the .NET assembly (.dll or .exe)")] string assemblyPath,
        [Description("Search pattern to match against member names (case-insensitive)")] string pattern)
    {
        try
        {
            var results = decompilerService.SearchMembers(assemblyPath, pattern);
            if (results.Count == 0)
                return $"No members matching '{pattern}' found.";

            var sb = new StringBuilder();
            sb.AppendLine($"Found {results.Count} member(s) matching '{pattern}':");
            sb.AppendLine();
            foreach (var group in results.GroupBy(r => r.DeclaringType))
            {
                sb.AppendLine($"  {group.Key}:");
                foreach (var member in group)
                {
                    sb.AppendLine($"    [{member.MemberType}] {member.Name}");
                }
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "list_namespaces"), Description("List all namespaces defined in a .NET assembly.")]
    public static string ListNamespaces(
        DecompilerService decompilerService,
        [Description("Full path to the .NET assembly (.dll or .exe)")] string assemblyPath)
    {
        try
        {
            var namespaces = decompilerService.ListNamespaces(assemblyPath);
            if (namespaces.Count == 0)
                return "No namespaces found (all types are in the global namespace).";

            var sb = new StringBuilder();
            sb.AppendLine($"Found {namespaces.Count} namespace(s):");
            sb.AppendLine();
            foreach (var ns in namespaces)
            {
                sb.AppendLine($"  {ns}");
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "find_implementations"), Description("Find types that implement a specific interface or extend a specific base class. Searches a single assembly, or all assemblies across configured search paths if no assembly is specified.")]
    public static string FindImplementations(
        DecompilerService decompilerService,
        [Description("Base class or interface name to search for (e.g. 'IDisposable', 'Controller')")] string baseOrInterfaceName,
        [Description("Optional full path to a .NET assembly. If omitted, searches all assemblies in configured search paths.")] string? assemblyPath = null)
    {
        try
        {
            var types = decompilerService.FindImplementations(assemblyPath, baseOrInterfaceName);
            if (types.Count == 0)
                return $"No types implementing or extending '{baseOrInterfaceName}' found.";

            var sb = new StringBuilder();
            sb.AppendLine($"Found {types.Count} type(s) implementing/extending '{baseOrInterfaceName}':");
            sb.AppendLine();
            foreach (var group in types.GroupBy(t => t.AssemblyName))
            {
                sb.AppendLine($"  [{group.Key}]:");
                foreach (var type in group)
                {
                    sb.AppendLine($"    [{type.Kind}] {type.FullName}");
                }
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
