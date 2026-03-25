using System.ComponentModel;

using ILSpyMcp.Services;

using ModelContextProtocol.Server;

namespace ILSpyMcp.Tools;

[McpServerToolType]
public sealed class DecompileTools
{
    [McpServerTool(Name = "decompile_type", Title = "Decompile Type", ReadOnly = true, OpenWorld = false), Description("Decompile a specific .NET type from an assembly to C# source code.")]
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

    [McpServerTool(Name = "decompile_member", Title = "Decompile Member", ReadOnly = true, OpenWorld = false), Description("Decompile a specific member (method, property, field, or event) from a .NET type to C# source code. Useful for inspecting a single function without decompiling the entire type.")]
    public static string DecompileMember(
        DecompilerService decompilerService,
        [Description("Full path to the .NET assembly (.dll or .exe)")] string assemblyPath,
        [Description("Fully qualified type name (e.g. 'MyNamespace.MyClass')")] string typeName,
        [Description("Member name (e.g. 'ToString', 'Count', 'MyMethod'). For overloaded methods, all overloads are returned.")] string memberName)
    {
        try
        {
            return decompilerService.DecompileMember(assemblyPath, typeName, memberName);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
