using System.ComponentModel;
using System.Text;

using ILSpyMcp.Services;

using ModelContextProtocol.Server;

namespace ILSpyMcp.Tools;

[McpServerToolType]
public sealed class AssemblyInfoTools
{
    [McpServerTool(Name = "get_assembly_info"), Description("Get metadata information about a .NET assembly including name, version, target framework, and referenced assemblies.")]
    public static string GetAssemblyInfo(
        DecompilerService decompilerService,
        [Description("Full path to the .NET assembly (.dll or .exe)")] string assemblyPath)
    {
        try
        {
            var info = decompilerService.GetAssemblyInfo(assemblyPath);
            var sb = new StringBuilder();
            sb.AppendLine($"Assembly: {info.Name}");
            sb.AppendLine($"Version: {info.Version}");
            sb.AppendLine($"Culture: {(string.IsNullOrEmpty(info.Culture) ? "neutral" : info.Culture)}");
            sb.AppendLine($"Target Framework: {info.TargetFramework ?? "Unknown"}");
            sb.AppendLine($"PE Flags: {info.PEKind}");
            sb.AppendLine();
            sb.AppendLine($"Referenced Assemblies ({info.References.Count}):");
            foreach (var reference in info.References)
            {
                sb.AppendLine($"  {reference}");
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
