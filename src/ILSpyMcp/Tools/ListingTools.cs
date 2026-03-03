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

    [McpServerTool(Name = "list_members"), Description("List all members (methods, properties, fields, events) of a specific type in a .NET assembly.")]
    public static string ListMembers(
        DecompilerService decompilerService,
        [Description("Full path to the .NET assembly (.dll or .exe)")] string assemblyPath,
        [Description("Fully qualified type name (e.g. 'MyNamespace.MyClass')")] string typeName)
    {
        try
        {
            var members = decompilerService.ListMembers(assemblyPath, typeName);
            if (members.Count == 0)
                return "No members found.";

            var sb = new StringBuilder();
            sb.AppendLine($"Members of {typeName} ({members.Count} total):");
            sb.AppendLine();

            foreach (var group in members.GroupBy(m => m.MemberType))
            {
                sb.AppendLine($"  {group.Key}s:");
                foreach (var member in group)
                {
                    sb.AppendLine($"    {member.Signature}");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
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
}
