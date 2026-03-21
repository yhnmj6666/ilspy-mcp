using System.ComponentModel;
using System.Text;

using ILSpyMcp.Services;

using ModelContextProtocol.Server;

namespace ILSpyMcp.Tools;

[McpServerToolType]
public sealed class SearchPathTools
{
    [McpServerTool(Name = "get_search_paths"), Description("Get the configured assembly search paths used for dependency resolution and cross-assembly searches.")]
    public static string GetSearchPaths(DecompilerService decompilerService)
    {
        var paths = decompilerService.SearchPaths;
        if (paths.Count == 0)
            return "No search paths configured. Use set_search_paths to add directories containing .NET assemblies.";

        var sb = new StringBuilder();
        sb.AppendLine($"Configured search paths ({paths.Count}):");
        sb.AppendLine();
        foreach (var path in paths)
        {
            sb.Append($"  {path}");
            if (Directory.Exists(path))
            {
                var dlls = Directory.GetFiles(path, "*.dll").Length;
                var exes = Directory.GetFiles(path, "*.exe").Length;
                sb.AppendLine($"  ({dlls} .dll, {exes} .exe)");
            }
            else
            {
                sb.AppendLine("  (directory not found)");
            }
        }
        return sb.ToString();
    }

    [McpServerTool(Name = "set_search_paths"), Description("Set the assembly search paths. These directories are used to resolve dependencies during decompilation and to search across assemblies (e.g. find_implementations). Replaces any previously configured paths. Pass an empty array to clear.")]
    public static string SetSearchPaths(
        DecompilerService decompilerService,
        [Description("Array of full directory paths containing .NET assemblies (e.g. ['C:\\\\app\\\\bin', 'C:\\\\libs'])")] string[] directories)
    {
        try
        {
            decompilerService.SetSearchPaths(directories);
            var paths = decompilerService.SearchPaths;
            if (paths.Count == 0)
                return "Search paths cleared.";
            return $"Search paths set ({paths.Count}): {string.Join(", ", paths)}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "list_loaded_assemblies"), Description("List all assemblies currently loaded in memory. Assemblies are cached when first accessed by any tool and remain loaded for the session.")]
    public static string ListLoadedAssemblies(DecompilerService decompilerService)
    {
        var assemblies = decompilerService.GetLoadedAssemblies();
        if (assemblies.Count == 0)
            return "No assemblies loaded.";

        var sb = new StringBuilder();
        sb.AppendLine($"Loaded assemblies ({assemblies.Count}):");
        sb.AppendLine();
        foreach (var asm in assemblies)
        {
            sb.AppendLine($"  {asm.Name} v{asm.Version}");
            sb.AppendLine($"    {asm.Path}");
        }
        return sb.ToString();
    }
}
