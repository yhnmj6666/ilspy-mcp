using System.ComponentModel;
using System.Text.Json;

using ILSpyMcp.Services;

using ModelContextProtocol.Server;

namespace ILSpyMcp.Tools;

[McpServerToolType]
public sealed class SearchPathTools
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    [McpServerTool(Name = "get_search_paths", Title = "Get Search Paths", ReadOnly = true, OpenWorld = false), Description("Get the configured assembly search paths used for dependency resolution and cross-assembly searches. Returns a JSON array.")]
    public static string GetSearchPaths(DecompilerService decompilerService)
    {
        var paths = decompilerService.SearchPaths;
        if (paths.Count == 0)
            return "No search paths configured. Use set_search_paths to add directories containing .NET assemblies.";

        var result = paths.Select(path =>
        {
            if (Directory.Exists(path))
            {
                var dlls = Directory.GetFiles(path, "*.dll").Length;
                var exes = Directory.GetFiles(path, "*.exe").Length;
                return new { Path = path, DllCount = dlls, ExeCount = exes, Exists = true };
            }
            return new { Path = path, DllCount = 0, ExeCount = 0, Exists = false };
        });

        return JsonSerializer.Serialize(result, s_jsonOptions);
    }

    [McpServerTool(Name = "set_search_paths", Title = "Set Search Paths", Destructive = false, Idempotent = true, OpenWorld = false), Description("Set the assembly search paths. These directories are used to resolve dependencies during decompilation and to search across assemblies (e.g. find_implementations). Replaces any previously configured paths. Pass an empty array to clear.")]
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

    [McpServerTool(Name = "list_loaded_assemblies", Title = "List Loaded Assemblies", ReadOnly = true, OpenWorld = false), Description("List all assemblies currently loaded in memory. Assemblies are cached when first accessed by any tool and remain loaded for the session. Returns a JSON array.")]
    public static string ListLoadedAssemblies(DecompilerService decompilerService)
    {
        var assemblies = decompilerService.GetLoadedAssemblies();
        if (assemblies.Count == 0)
            return "[]";

        return JsonSerializer.Serialize(
            assemblies.Select(a => new { a.Name, a.Version, a.Path }),
            s_jsonOptions);
    }
}
