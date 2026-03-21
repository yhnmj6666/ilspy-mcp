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
    private readonly List<string> _searchPaths = new();
    private readonly object _searchPathLock = new();

    public IReadOnlyList<string> SearchPaths
    {
        get { lock (_searchPathLock) return _searchPaths.ToList(); }
    }

    public void SetSearchPaths(IEnumerable<string> directories)
    {
        var validated = new List<string>();
        var notFound = new List<string>();

        foreach (var dir in directories)
        {
            var fullPath = Path.GetFullPath(dir);
            if (Directory.Exists(fullPath))
                validated.Add(fullPath);
            else
                notFound.Add(fullPath);
        }

        if (notFound.Count > 0)
            throw new DirectoryNotFoundException($"Directories not found: {string.Join(", ", notFound)}");

        lock (_searchPathLock)
        {
            _searchPaths.Clear();
            _searchPaths.AddRange(validated.Distinct(StringComparer.OrdinalIgnoreCase));
        }
    }

    public IReadOnlyList<string> GetAssembliesInSearchPaths()
    {
        var assemblies = new List<string>();
        foreach (var dir in SearchPaths)
        {
            if (!Directory.Exists(dir))
                continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*.dll"))
                assemblies.Add(file);
            foreach (var file in Directory.EnumerateFiles(dir, "*.exe"))
                assemblies.Add(file);
        }
        return assemblies;
    }

    public IReadOnlyList<LoadedAssemblyInfo> GetLoadedAssemblies()
    {
        return _peFileCache.Select(kvp =>
        {
            var metadata = kvp.Value.Metadata;
            string name;
            string version;
            try
            {
                var asmDef = metadata.GetAssemblyDefinition();
                name = metadata.GetString(asmDef.Name);
                version = asmDef.Version.ToString();
            }
            catch
            {
                name = Path.GetFileNameWithoutExtension(kvp.Key);
                version = "?";
            }
            return new LoadedAssemblyInfo(name, version, kvp.Key);
        }).OrderBy(a => a.Name).ToList();
    }

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
        settings.ThrowOnAssemblyResolveErrors = false;
        var fullPath = Path.GetFullPath(assemblyPath);

        var resolver = new UniversalAssemblyResolver(fullPath, false, null);
        foreach (var dir in SearchPaths)
            resolver.AddSearchDirectory(dir);

        return new CSharpDecompiler(fullPath, resolver, settings);
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

    public string DecompileMember(string assemblyPath, string typeName, string memberName)
    {
        var peFile = LoadAssembly(assemblyPath);
        var metadata = peFile.Metadata;

        var typeDefHandle = FindTypeDefinition(metadata, typeName);
        if (typeDefHandle.IsNil)
            throw new ArgumentException($"Type '{typeName}' not found in assembly.");

        var typeDef = metadata.GetTypeDefinition(typeDefHandle);
        var handles = new List<EntityHandle>();

        foreach (var h in typeDef.GetMethods())
        {
            if (metadata.GetString(metadata.GetMethodDefinition(h).Name) == memberName)
                handles.Add(h);
        }

        foreach (var h in typeDef.GetProperties())
        {
            if (metadata.GetString(metadata.GetPropertyDefinition(h).Name) == memberName)
                handles.Add(h);
        }

        foreach (var h in typeDef.GetFields())
        {
            if (metadata.GetString(metadata.GetFieldDefinition(h).Name) == memberName)
                handles.Add(h);
        }

        foreach (var h in typeDef.GetEvents())
        {
            if (metadata.GetString(metadata.GetEventDefinition(h).Name) == memberName)
                handles.Add(h);
        }

        if (handles.Count == 0)
            throw new ArgumentException($"Member '{memberName}' not found in type '{typeName}'.");

        var decompiler = CreateDecompiler(assemblyPath);
        var results = new List<string>();
        foreach (var handle in handles)
        {
            results.Add(decompiler.DecompileAsString(handle));
        }

        return string.Join("\n", results);
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

    public IReadOnlyList<MemberSearchResult> SearchMembers(string assemblyPath, string pattern)
    {
        var peFile = LoadAssembly(assemblyPath);
        var metadata = peFile.Metadata;
        var results = new List<MemberSearchResult>();

        foreach (var typeDefHandle in metadata.TypeDefinitions)
        {
            var typeDef = metadata.GetTypeDefinition(typeDefHandle);
            var typeName = metadata.GetString(typeDef.Name);
            if (typeName == "<Module>")
                continue;

            var typeNs = metadata.GetString(typeDef.Namespace);
            var fullTypeName = string.IsNullOrEmpty(typeNs) ? typeName : $"{typeNs}.{typeName}";

            foreach (var h in typeDef.GetMethods())
            {
                var name = metadata.GetString(metadata.GetMethodDefinition(h).Name);
                if (name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    results.Add(new MemberSearchResult(fullTypeName, "Method", name));
            }

            foreach (var h in typeDef.GetProperties())
            {
                var name = metadata.GetString(metadata.GetPropertyDefinition(h).Name);
                if (name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    results.Add(new MemberSearchResult(fullTypeName, "Property", name));
            }

            foreach (var h in typeDef.GetFields())
            {
                var name = metadata.GetString(metadata.GetFieldDefinition(h).Name);
                if (name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    results.Add(new MemberSearchResult(fullTypeName, "Field", name));
            }

            foreach (var h in typeDef.GetEvents())
            {
                var name = metadata.GetString(metadata.GetEventDefinition(h).Name);
                if (name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    results.Add(new MemberSearchResult(fullTypeName, "Event", name));
            }
        }

        return results;
    }

    public IReadOnlyList<string> ListNamespaces(string assemblyPath)
    {
        var peFile = LoadAssembly(assemblyPath);
        var metadata = peFile.Metadata;
        var namespaces = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var typeDefHandle in metadata.TypeDefinitions)
        {
            var typeDef = metadata.GetTypeDefinition(typeDefHandle);
            if (typeDef.IsNested)
                continue;
            var name = metadata.GetString(typeDef.Name);
            if (name == "<Module>")
                continue;
            var ns = metadata.GetString(typeDef.Namespace);
            if (!string.IsNullOrEmpty(ns))
                namespaces.Add(ns);
        }

        return namespaces.ToList();
    }

    public IReadOnlyList<ImplementationInfo> FindImplementations(string? assemblyPath, string baseOrInterfaceName)
    {
        var assemblyPaths = new List<string>();

        if (assemblyPath != null)
            assemblyPaths.Add(Path.GetFullPath(assemblyPath));
        else
            assemblyPaths.AddRange(GetAssembliesInSearchPaths());

        if (assemblyPaths.Count == 0)
            throw new InvalidOperationException("No assembly specified and no search paths configured. Use add_search_path first or provide an assemblyPath.");

        var results = new List<ImplementationInfo>();

        foreach (var asmPath in assemblyPaths)
        {
            PEFile peFile;
            MetadataReader metadata;
            try
            {
                peFile = LoadAssembly(asmPath);
                metadata = peFile.Metadata;
            }
            catch
            {
                continue; // skip non-.NET files
            }

            var asmName = Path.GetFileName(asmPath);

            foreach (var typeDefHandle in metadata.TypeDefinitions)
            {
                var typeDef = metadata.GetTypeDefinition(typeDefHandle);
                var name = metadata.GetString(typeDef.Name);
                if (name == "<Module>")
                    continue;

                if (MatchesBaseType(typeDef, metadata, baseOrInterfaceName) ||
                    MatchesInterface(typeDef, metadata, baseOrInterfaceName))
                {
                    var ns = metadata.GetString(typeDef.Namespace);
                    var fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
                    var kind = GetTypeKind(typeDef, metadata);
                    results.Add(new ImplementationInfo(fullName, ns, name, kind, asmName, asmPath));
                }
            }
        }

        return results.OrderBy(t => t.FullName).ToList();
    }

    private static bool MatchesBaseType(TypeDefinition typeDef, MetadataReader metadata, string pattern)
    {
        var baseType = typeDef.BaseType;
        if (baseType.IsNil)
            return false;

        var baseTypeName = ResolveTypeName(baseType, metadata);
        return baseTypeName.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesInterface(TypeDefinition typeDef, MetadataReader metadata, string pattern)
    {
        foreach (var ifaceHandle in typeDef.GetInterfaceImplementations())
        {
            var iface = metadata.GetInterfaceImplementation(ifaceHandle);
            var ifaceName = ResolveTypeName(iface.Interface, metadata);
            if (ifaceName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string ResolveTypeName(EntityHandle handle, MetadataReader metadata)
    {
        return handle.Kind switch
        {
            HandleKind.TypeReference => GetRefFullName(metadata.GetTypeReference((TypeReferenceHandle)handle), metadata),
            HandleKind.TypeDefinition => GetDefFullName(metadata.GetTypeDefinition((TypeDefinitionHandle)handle), metadata),
            _ => ""
        };

        static string GetRefFullName(TypeReference typeRef, MetadataReader m)
        {
            var ns = m.GetString(typeRef.Namespace);
            var name = m.GetString(typeRef.Name);
            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }

        static string GetDefFullName(TypeDefinition typeDef, MetadataReader m)
        {
            var ns = m.GetString(typeDef.Namespace);
            var name = m.GetString(typeDef.Name);
            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }
    }

    public AssemblyInfo GetAssemblyInfo(string assemblyPath)
    {
        var peFile = LoadAssembly(assemblyPath);
        var metadata = peFile.Metadata;

        var assemblyDef = metadata.GetAssemblyDefinition();
        var name = metadata.GetString(assemblyDef.Name);
        var version = assemblyDef.Version.ToString();
        var culture = metadata.GetString(assemblyDef.Culture);

        // Strong name
        var publicKeyBlob = metadata.GetBlobBytes(assemblyDef.PublicKey);
        string? publicKeyToken = null;
        if (publicKeyBlob.Length > 0)
        {
            using var sha1 = System.Security.Cryptography.SHA1.Create();
            var hash = sha1.ComputeHash(publicKeyBlob);
            var token = hash[^8..];
            Array.Reverse(token);
            publicKeyToken = Convert.ToHexString(token).ToLowerInvariant();
        }

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

        var peKind = peFile.Reader.PEHeaders.CorHeader?.Flags.ToString() ?? "Unknown";

        // Build reference list with resolvability check
        var searchPaths = SearchPaths;
        var assemblyDir = Path.GetDirectoryName(Path.GetFullPath(assemblyPath)) ?? "";
        var references = new List<AssemblyReferenceInfo>();

        foreach (var refHandle in metadata.AssemblyReferences)
        {
            var asmRef = metadata.GetAssemblyReference(refHandle);
            var refName = metadata.GetString(asmRef.Name);
            var refVersion = asmRef.Version.ToString();
            var refKeyBlob = metadata.GetBlobBytes(asmRef.PublicKeyOrToken);
            string? refToken = null;
            if (refKeyBlob.Length > 0)
                refToken = Convert.ToHexString(refKeyBlob).ToLowerInvariant();

            // Check if resolvable
            string? resolvedPath = null;
            var candidates = searchPaths.Prepend(assemblyDir);
            foreach (var dir in candidates)
            {
                var candidate = Path.Combine(dir, $"{refName}.dll");
                if (File.Exists(candidate))
                {
                    resolvedPath = candidate;
                    break;
                }
            }

            references.Add(new AssemblyReferenceInfo(refName, refVersion, refToken, resolvedPath));
        }

        return new AssemblyInfo(name, version, culture, publicKeyToken, targetFramework, peKind, references);
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
public record MemberSearchResult(string DeclaringType, string MemberType, string Name);
public record ImplementationInfo(string FullName, string Namespace, string Name, string Kind, string AssemblyName, string AssemblyPath);
public record LoadedAssemblyInfo(string Name, string Version, string Path);
public record AssemblyReferenceInfo(string Name, string Version, string? PublicKeyToken, string? ResolvedPath);
public record AssemblyInfo(
    string Name, string Version, string Culture,
    string? PublicKeyToken, string? TargetFramework, string PEKind,
    IReadOnlyList<AssemblyReferenceInfo> References);
