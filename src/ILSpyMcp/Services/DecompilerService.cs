using System.Collections.Concurrent;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSpyMcp.Services;

public class DecompilerService
{
    private readonly ConcurrentDictionary<string, PEFile> _peFileCache = new();

    public PEFile LoadAssembly(string assemblyPath)
    {
        var fullPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Assembly not found: {fullPath}");

        return _peFileCache.GetOrAdd(fullPath, path => new PEFile(path));
    }

    public CSharpDecompiler CreateDecompiler(string assemblyPath, DecompilerSettings? settings = null)
    {
        settings ??= new DecompilerSettings();
        var fullPath = Path.GetFullPath(assemblyPath);
        return new CSharpDecompiler(fullPath, settings);
    }

    public string DecompileType(string assemblyPath, string typeName)
    {
        var decompiler = CreateDecompiler(assemblyPath);
        var fullTypeName = new FullTypeName(typeName);
        return decompiler.DecompileTypeAsString(fullTypeName);
    }

    public string DecompileAssembly(string assemblyPath)
    {
        var decompiler = CreateDecompiler(assemblyPath);
        return decompiler.DecompileWholeModuleAsString();
    }

    public IReadOnlyList<TypeInfo> ListTypes(string assemblyPath, string? namespaceFilter = null)
    {
        var peFile = LoadAssembly(assemblyPath);
        var metadata = peFile.Metadata;
        var types = new List<TypeInfo>();

        foreach (var typeDefHandle in metadata.TypeDefinitions)
        {
            var typeDef = metadata.GetTypeDefinition(typeDefHandle);
            var name = metadata.GetString(typeDef.Name);
            var ns = metadata.GetString(typeDef.Namespace);

            if (name == "<Module>")
                continue;

            if (namespaceFilter != null && !ns.StartsWith(namespaceFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            // Skip compiler-generated nested types
            if (typeDef.IsNested)
                continue;

            var fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
            var kind = GetTypeKind(typeDef, metadata);

            types.Add(new TypeInfo(fullName, ns, name, kind));
        }

        return types.OrderBy(t => t.FullName).ToList();
    }

    public IReadOnlyList<MemberInfo> ListMembers(string assemblyPath, string typeName)
    {
        var peFile = LoadAssembly(assemblyPath);
        var metadata = peFile.Metadata;
        var members = new List<MemberInfo>();

        var typeDefHandle = FindTypeDefinition(metadata, typeName);
        if (typeDefHandle.IsNil)
            throw new ArgumentException($"Type '{typeName}' not found in assembly.");

        var typeDef = metadata.GetTypeDefinition(typeDefHandle);

        foreach (var methodHandle in typeDef.GetMethods())
        {
            var method = metadata.GetMethodDefinition(methodHandle);
            var name = metadata.GetString(method.Name);
            var sig = method.DecodeSignature(new SignatureTypeProvider(), default);
            var paramTypes = string.Join(", ", sig.ParameterTypes);
            members.Add(new MemberInfo("Method", name, $"{sig.ReturnType} {name}({paramTypes})"));
        }

        foreach (var propHandle in typeDef.GetProperties())
        {
            var prop = metadata.GetPropertyDefinition(propHandle);
            var name = metadata.GetString(prop.Name);
            members.Add(new MemberInfo("Property", name, name));
        }

        foreach (var fieldHandle in typeDef.GetFields())
        {
            var field = metadata.GetFieldDefinition(fieldHandle);
            var name = metadata.GetString(field.Name);
            members.Add(new MemberInfo("Field", name, name));
        }

        foreach (var eventHandle in typeDef.GetEvents())
        {
            var evt = metadata.GetEventDefinition(eventHandle);
            var name = metadata.GetString(evt.Name);
            members.Add(new MemberInfo("Event", name, name));
        }

        return members;
    }

    public string GetIL(string assemblyPath, string typeName, string? memberName = null)
    {
        var peFile = LoadAssembly(assemblyPath);
        var metadata = peFile.Metadata;
        var output = new PlainTextOutput();
        var disassembler = new ReflectionDisassembler(output, CancellationToken.None);

        var typeDefHandle = FindTypeDefinition(metadata, typeName);
        if (typeDefHandle.IsNil)
            throw new ArgumentException($"Type '{typeName}' not found in assembly.");

        if (memberName != null)
        {
            var typeDef = metadata.GetTypeDefinition(typeDefHandle);
            var methodHandle = typeDef.GetMethods()
                .FirstOrDefault(m => metadata.GetString(metadata.GetMethodDefinition(m).Name) == memberName);

            if (methodHandle.IsNil)
                throw new ArgumentException($"Method '{memberName}' not found in type '{typeName}'.");

            disassembler.DisassembleMethod(peFile, methodHandle);
        }
        else
        {
            disassembler.DisassembleType(peFile, typeDefHandle);
        }

        return output.ToString();
    }

    public IReadOnlyList<TypeInfo> SearchTypes(string assemblyPath, string pattern)
    {
        var allTypes = ListTypes(assemblyPath);
        return allTypes.Where(t =>
            t.FullName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public AssemblyInfo GetAssemblyInfo(string assemblyPath)
    {
        var peFile = LoadAssembly(assemblyPath);
        var metadata = peFile.Metadata;

        var assemblyDef = metadata.GetAssemblyDefinition();
        var name = metadata.GetString(assemblyDef.Name);
        var version = assemblyDef.Version.ToString();
        var culture = metadata.GetString(assemblyDef.Culture);

        string? targetFramework = null;
        foreach (var attrHandle in assemblyDef.GetCustomAttributes())
        {
            var attr = metadata.GetCustomAttribute(attrHandle);
            var ctorHandle = attr.Constructor;
            if (ctorHandle.Kind == HandleKind.MemberReference)
            {
                var memberRef = metadata.GetMemberReference((MemberReferenceHandle)ctorHandle);
                var typeRef = metadata.GetTypeReference((TypeReferenceHandle)memberRef.Parent);
                if (metadata.GetString(typeRef.Name) == "TargetFrameworkAttribute")
                {
                    var value = metadata.GetBlobReader(attr.Value);
                    value.ReadUInt16(); // prolog
                    targetFramework = value.ReadSerializedString();
                    break;
                }
            }
        }

        var references = new List<string>();
        foreach (var refHandle in metadata.AssemblyReferences)
        {
            var asmRef = metadata.GetAssemblyReference(refHandle);
            references.Add($"{metadata.GetString(asmRef.Name)}, Version={asmRef.Version}");
        }

        var peKind = peFile.Reader.PEHeaders.CorHeader?.Flags.ToString() ?? "Unknown";

        return new AssemblyInfo(name, version, culture, targetFramework, peKind, references);
    }

    private static TypeDefinitionHandle FindTypeDefinition(MetadataReader metadata, string typeName)
    {
        foreach (var handle in metadata.TypeDefinitions)
        {
            var typeDef = metadata.GetTypeDefinition(handle);
            var name = metadata.GetString(typeDef.Name);
            var ns = metadata.GetString(typeDef.Namespace);
            var fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
            if (fullName == typeName)
                return handle;
        }
        return default;
    }

    private static string GetTypeKind(TypeDefinition typeDef, MetadataReader metadata)
    {
        if ((typeDef.Attributes & System.Reflection.TypeAttributes.Interface) != 0)
            return "interface";
        if ((typeDef.Attributes & System.Reflection.TypeAttributes.Sealed) != 0 &&
            (typeDef.Attributes & System.Reflection.TypeAttributes.Abstract) != 0)
            return "static class";

        var baseType = typeDef.BaseType;
        if (!baseType.IsNil)
        {
            string baseTypeName = baseType.Kind switch
            {
                HandleKind.TypeReference => metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)baseType).Name),
                HandleKind.TypeDefinition => metadata.GetString(metadata.GetTypeDefinition((TypeDefinitionHandle)baseType).Name),
                _ => ""
            };

            return baseTypeName switch
            {
                "Enum" => "enum",
                "ValueType" => "struct",
                "MulticastDelegate" or "Delegate" => "delegate",
                _ => "class"
            };
        }

        return "class";
    }
}

// Simple signature decoder for display purposes
internal class SignatureTypeProvider : ISignatureTypeProvider<string, object?>
{
    public string GetArrayType(string elementType, ArrayShape shape) => $"{elementType}[]";
    public string GetByReferenceType(string elementType) => $"ref {elementType}";
    public string GetFunctionPointerType(MethodSignature<string> signature) => "delegate*";
    public string GetGenericInstantiation(string genericType, System.Collections.Immutable.ImmutableArray<string> typeArguments) =>
        $"{genericType}<{string.Join(", ", typeArguments)}>";
    public string GetGenericMethodParameter(object? genericContext, int index) => $"!!{index}";
    public string GetGenericTypeParameter(object? genericContext, int index) => $"!{index}";
    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
    public string GetPinnedType(string elementType) => elementType;
    public string GetPointerType(string elementType) => $"{elementType}*";
    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
    {
        PrimitiveTypeCode.Void => "void",
        PrimitiveTypeCode.Boolean => "bool",
        PrimitiveTypeCode.Byte => "byte",
        PrimitiveTypeCode.SByte => "sbyte",
        PrimitiveTypeCode.Char => "char",
        PrimitiveTypeCode.Int16 => "short",
        PrimitiveTypeCode.UInt16 => "ushort",
        PrimitiveTypeCode.Int32 => "int",
        PrimitiveTypeCode.UInt32 => "uint",
        PrimitiveTypeCode.Int64 => "long",
        PrimitiveTypeCode.UInt64 => "ulong",
        PrimitiveTypeCode.Single => "float",
        PrimitiveTypeCode.Double => "double",
        PrimitiveTypeCode.String => "string",
        PrimitiveTypeCode.Object => "object",
        PrimitiveTypeCode.IntPtr => "nint",
        PrimitiveTypeCode.UIntPtr => "nuint",
        _ => typeCode.ToString()
    };
    public string GetSZArrayType(string elementType) => $"{elementType}[]";
    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        var typeDef = reader.GetTypeDefinition(handle);
        return reader.GetString(typeDef.Name);
    }
    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        var typeRef = reader.GetTypeReference(handle);
        return reader.GetString(typeRef.Name);
    }
    public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) => "?";
}

public record TypeInfo(string FullName, string Namespace, string Name, string Kind);
public record MemberInfo(string MemberType, string Name, string Signature);
public record AssemblyInfo(
    string Name, string Version, string Culture,
    string? TargetFramework, string PEKind,
    IReadOnlyList<string> References);
