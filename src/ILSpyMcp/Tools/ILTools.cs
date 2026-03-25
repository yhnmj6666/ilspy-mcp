using System.ComponentModel;

using ILSpyMcp.Services;

using ModelContextProtocol.Server;

namespace ILSpyMcp.Tools;

[McpServerToolType]
public sealed class ILTools
{
    [McpServerTool(Name = "get_il", Title = "Get IL Disassembly", ReadOnly = true, OpenWorld = false), Description("Get the IL (Intermediate Language) disassembly for a type or a specific method in a .NET assembly.")]
    public static string GetIL(
        DecompilerService decompilerService,
        [Description("Full path to the .NET assembly (.dll or .exe)")] string assemblyPath,
        [Description("Fully qualified type name (e.g. 'MyNamespace.MyClass')")] string typeName,
        [Description("Optional method name to get IL for a specific method only")] string? memberName = null)
    {
        try
        {
            return decompilerService.GetIL(assemblyPath, typeName, memberName);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
