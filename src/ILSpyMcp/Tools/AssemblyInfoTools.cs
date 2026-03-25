using System.ComponentModel;
using System.Text;

using ILSpyMcp.Services;

using ModelContextProtocol.Server;

namespace ILSpyMcp.Tools;

[McpServerToolType]
public sealed class AssemblyInfoTools
{
    [McpServerTool(Name = "get_assembly_info", Title = "Get Assembly Info", ReadOnly = true, OpenWorld = false), Description("Get metadata information about a .NET assembly including name, version, strong name, target framework, and referenced assemblies with their resolution status.")]
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
            sb.AppendLine($"Public Key Token: {info.PublicKeyToken ?? "none (not strong-named)"}");
            sb.AppendLine($"Target Framework: {info.TargetFramework ?? "Unknown"}");
            sb.AppendLine($"PE Flags: {info.PEKind}");
            sb.AppendLine();

            var resolved = info.References.Count(r => r.ResolvedPath != null);
            sb.AppendLine($"Referenced Assemblies ({info.References.Count} total, {resolved} resolvable):");
            foreach (var reference in info.References)
            {
                var status = reference.ResolvedPath != null ? "OK" : "MISSING";
                var token = reference.PublicKeyToken != null ? $", PublicKeyToken={reference.PublicKeyToken}" : "";
                sb.AppendLine($"  [{status}] {reference.Name}, Version={reference.Version}{token}");
                if (reference.ResolvedPath != null)
                    sb.AppendLine($"         -> {reference.ResolvedPath}");
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
