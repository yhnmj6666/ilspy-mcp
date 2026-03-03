using System.ComponentModel;

using ILSpyMcp.Services;

using ModelContextProtocol.Server;

namespace ILSpyMcp.Tools;

[McpServerToolType]
public sealed class DecompileTools
{
    [McpServerTool(Name = "decompile_type"), Description("Decompile a specific .NET type from an assembly to C# source code.")]
    public static string DecompileType(
        DecompilerService decompilerService,
        [Description("Full path to the .NET assembly (.dll or .exe)")] string assemblyPath,
        [Description("Fully qualified type name (e.g. 'System.Collections.Generic.List`1')")] string typeName)
    {
        try
        {
            return decompilerService.DecompileType(assemblyPath, typeName);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "decompile_assembly"), Description("Decompile an entire .NET assembly to C# source code. Warning: output may be very large for big assemblies.")]
    public static string DecompileAssembly(
        DecompilerService decompilerService,
        [Description("Full path to the .NET assembly (.dll or .exe)")] string assemblyPath)
    {
        try
        {
            var result = decompilerService.DecompileAssembly(assemblyPath);
            const int maxLength = 100_000;
            if (result.Length > maxLength)
                return result[..maxLength] + $"\n\n... [Output truncated at {maxLength} characters. Total: {result.Length} characters]";
            return result;
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
