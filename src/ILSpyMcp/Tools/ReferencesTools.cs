using System.ComponentModel;
using System.Text.Json;

using ILSpyMcp.Services;

using ModelContextProtocol.Server;

namespace ILSpyMcp.Tools;

[McpServerToolType]
public sealed class ReferencesTools
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    [McpServerTool(Name = "find_references", Title = "Find References", ReadOnly = true, OpenWorld = false), Description(
        "Find IL-level cross-references (callers/users) of a member or type across one or more .NET assemblies. " +
        "Walks every method body in the scope assemblies and inspects operand tokens of reference opcodes " +
        "(call, callvirt, newobj, jmp, ldftn, ldvirtftn, ldfld, ldflda, stfld, ldsfld, ldsflda, stsfld, ldtoken, " +
        "castclass, isinst, box, unbox, unbox.any, newarr, initobj, ldobj, stobj, cpobj, mkrefany, refanyval, " +
        "sizeof, ldelem, stelem, ldelema, constrained.). " +
        "For properties/events the accessor methods (get_/set_, add_/remove_/raise_) are matched automatically. " +
        "Returns a JSON object with ResolvedTargets (the descriptors used for matching), ScannedAssemblies, " +
        "SkippedAssemblies, and Hits (one entry per referencing method, with distinct opcode Kinds, MatchedTargets, " +
        "and per-instruction Sites with ILOffset). " +
        "Limitations: matches by metadata type-name + assembly simple name (no PKT/version check); generic instantiations " +
        "are matched against the open-generic name (e.g. List`1); virtual dispatch is not resolved (callvirt Base.M is " +
        "reported against Base.M, not derived overrides); references that appear only in metadata signatures " +
        "(parameter types, base types, attribute usages, custom-attribute constructors, etc.) and reflection-based usage " +
        "are not detected.")]
    public static string FindReferences(
        DecompilerService decompilerService,
        [Description("Full path to the .NET assembly (.dll or .exe) that defines the member being referenced.")] string targetAssembly,
        [Description("Fully-qualified declaring type name (e.g. 'MyNamespace.MyClass'). For nested types use '.', for generic types use the backtick-arity form (e.g. 'System.Collections.Generic.List`1').")] string typeName,
        [Description("Simple name of the member. For properties/events pass the property/event name; accessor methods (get_/set_, add_/remove_/raise_) will be expanded automatically. May be empty when memberKind is 'type'.")] string memberName,
        [Description("Member kind disambiguator: 'method', 'field', 'property', 'event', 'type', or 'any' (default). Use 'type' to find references to the type itself.")] string memberKind = "any",
        [Description("Optional full path to a single .NET assembly to limit the search to. If omitted, searches the target assembly plus all assemblies in configured search paths.")] string? scopeAssembly = null)
    {
        try
        {
            var outcome = decompilerService.FindReferences(targetAssembly, typeName, memberName, memberKind, scopeAssembly);
            return JsonSerializer.Serialize(
                new
                {
                    outcome.ResolvedTargets,
                    outcome.ScannedAssemblies,
                    outcome.SkippedAssemblies,
                    outcome.Hits
                },
                s_jsonOptions);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
