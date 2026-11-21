using System.Reflection;
using Catalyst.Generators.Builders;
using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecGraph.Properties;
using Microsoft.Extensions.Logging;

namespace Catalyst.Generators;

public record CompilerOptions(
    string EnumBuilderName, 
    string DefinitionBuilderName, 
    string? ClientServiceBuilderName, 
    string? ServerServiceBuilderName,
    string? ValidatorBuilderName,
    string? TypeScriptSchema = null,
    bool NoTimestamp = false);

public abstract class Compiler
{
    public required ILogger<Compiler> Logger { get; init; }

    public abstract string Name { get; }
    
    protected IEnumBuilder EnumBuilder { get; }
    protected IDefinitionBuilder DefinitionBuilder { get; }
    protected IClientServiceBuilder? ClientServiceBuilder { get; }
    protected IServerServiceBuilder? ServerServiceBuilder { get; }
    protected IValidatorBuilder? ValidatorBuilder { get; }

    List<IEnumBuilder> AllEnumBuilders { get; } = [];
    List<IDefinitionBuilder> AllDefinitionBuilders { get; } = [];
    List<IClientServiceBuilder> AllClientServiceBuilders { get; } = [];
    List<IServerServiceBuilder> AllServerServiceBuilders { get; } = [];
    List<IValidatorBuilder> AllValidatorBuilders { get; } = [];

    /// <summary>
    /// When true, generated file headers leave out the generation time, so the same spec
    /// always produces byte-identical output.
    /// </summary>
    protected bool NoTimestamp { get; }

    /// <summary>
    /// The " at {time}" part of a generated file header, or nothing when timestamps are off.
    /// </summary>
    protected string GeneratedAt => NoTimestamp ? string.Empty : $" at {DateTime.Now}";

    /// <summary>Whether this compiler can emit discriminated unions.</summary>
    protected virtual bool SupportsUnions => false;

    /// <summary>Whether this compiler can emit enums whose wire values are strings.</summary>
    protected virtual bool SupportsStringEnums => false;

    /// <summary>Whether this compiler honours a property's 'presence'.</summary>
    protected virtual bool SupportsPropertyPresence => false;

    protected Compiler(CompilerOptions options)
    {
        NoTimestamp = options.NoTimestamp;
        FindAllBuildersForCompiler();
        
        EnumBuilder = 
            AllEnumBuilders.Find(b => b.Name.Split(';').Contains(options.EnumBuilderName))
            ?? throw new NullReferenceException($"Failed to find Enum Builder {options.EnumBuilderName}");

        DefinitionBuilder = 
            AllDefinitionBuilders.Find(b => b.Name.Split(';').Contains(options.DefinitionBuilderName)) 
            ?? throw new NullReferenceException($"Failed to find Definition Builder {options.DefinitionBuilderName}");

        if (!string.IsNullOrEmpty(options.ClientServiceBuilderName))
        {
            ClientServiceBuilder = 
                AllClientServiceBuilders.Find(b => b.Name.Split(';').Contains(options.ClientServiceBuilderName))
                ?? throw new NullReferenceException($"Failed to find Client Service Builder {options.ClientServiceBuilderName}");
        }
        
        if (!string.IsNullOrEmpty(options.ServerServiceBuilderName))
        {
            ServerServiceBuilder = 
                AllServerServiceBuilders.Find(b => b.Name.Split(';').Contains(options.ServerServiceBuilderName))
                ?? throw new NullReferenceException($"Failed to find Server Service Builder {options.ServerServiceBuilderName}");
        }

        if (!string.IsNullOrEmpty(options.ValidatorBuilderName))
        {
            IValidatorBuilder? foundBuilder = AllValidatorBuilders.Find(b => b.Name.Split(';').Contains(options.ValidatorBuilderName));
            if (foundBuilder is not null)
                ValidatorBuilder = foundBuilder;
        }
    }

    /// <summary>
    /// Adds a union to the file it should be emitted in. Compilers that support unions override this.
    /// </summary>
    protected virtual void BuildUnion(BuildContext context, UnionNode unionNode)
    {
        throw new NotSupportedException($"The {Name} compiler does not support unions ('{unionNode.FullName}').");
    }

    /// <summary>
    /// Fails early, with a clear message, when a spec uses a feature this compiler can't emit,
    /// rather than silently generating code with the wrong wire format.
    /// </summary>
    void EnsureFeaturesSupported(FileNode fileNode)
    {
        if (!SupportsUnions && fileNode.Unions.Count > 0)
            throw new NotSupportedException(
                $"The {Name} compiler does not support unions ('{fileNode.Unions.First().Value.FullName}').");

        if (!SupportsStringEnums)
        {
            EnumNode? stringEnum = fileNode.Enums.Values.FirstOrDefault(enumNode => enumNode.IsString);
            if (stringEnum is not null)
                throw new NotSupportedException(
                    $"The {Name} compiler does not support string enums ('{stringEnum.FullName}').");
        }

        // A file can also use a union or string enum declared in another file it includes.
        IEnumerable<DataMemberNode> dataMembers = fileNode.Definitions.Values
            .SelectMany(definition => definition.Properties.Values.Cast<DataMemberNode>()
                .Concat(definition.Constants.Values));
        foreach (DataMemberNode dataMember in dataMembers)
        {
            if (dataMember.BuiltType is null)
                continue;

            if (!SupportsUnions && UsesType(dataMember.BuiltType, type => type is UnionType))
                throw new NotSupportedException(
                    $"The {Name} compiler does not support unions ('{dataMember.FullName}' uses one).");

            if (!SupportsStringEnums && UsesType(dataMember.BuiltType, type => type is EnumType { OwnedEnum.IsString: true }))
                throw new NotSupportedException(
                    $"The {Name} compiler does not support string enums ('{dataMember.FullName}' uses one).");
        }

        if (!SupportsPropertyPresence)
        {
            PropertyNode? presenceProperty = fileNode.Definitions.Values
                .SelectMany(definition => definition.Properties.Values)
                .FirstOrDefault(property => property.Presence is not PropertyPresence.Default);
            if (presenceProperty is not null)
                throw new NotSupportedException(
                    $"The {Name} compiler does not support 'presence' ('{presenceProperty.FullName}').");
        }
    }

    static bool UsesType(IDataType dataType, Func<IDataType, bool> predicate)
    {
        return predicate(dataType) || dataType switch
        {
            IPropertyContainer1InnerType container => UsesType(container.InnerType, predicate),
            IPropertyContainer2InnerTypes container => UsesType(container.InnerTypeA, predicate) ||
                                                       UsesType(container.InnerTypeB, predicate),
            _ => false
        };
    }

    T? TryCreateBuilder<T>(Type type, Type builderType)
    {
        if (type.GetInterfaces().All(i => i != builderType))
            return default;
        
        if (Activator.CreateInstance(type) is not T createdBuilder)
            throw new NullReferenceException($"Failed to create instance of {type}");

        PropertyInfo? compilerProperty = type.GetProperty("Compiler");
        if (compilerProperty is null)
            throw new NullReferenceException($"Type {type} doesn't have a Compiler property");
        
        if (!compilerProperty.PropertyType.IsAssignableFrom(GetType()))
            throw new InvalidCastException($"Type {type} doesn't have a convertable Compiler property. Expected {GetType()}. Received {compilerProperty.PropertyType}");
        
        compilerProperty.SetValue(createdBuilder, this);

        return (T?)createdBuilder;
    }

    void FindAllBuildersForCompiler()
    {
        Type enumBuilderInterface = typeof(IEnumBuilder<>).MakeGenericType(GetType());
        Type definitionBuilderInterface = typeof(IDefinitionBuilder<>).MakeGenericType(GetType());
        Type clientServiceBuilderInterface = typeof(IClientServiceBuilder<>).MakeGenericType(GetType());
        Type serverServiceBuilderInterface = typeof(IServerServiceBuilder<>).MakeGenericType(GetType());
        Type validatorBuilderInterface = typeof(IValidatorBuilder<>).MakeGenericType(GetType());
        
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        foreach (Type type in assembly.GetTypes())
        {
            if (TryCreateBuilder<IEnumBuilder>(type, enumBuilderInterface) is { } enumBuilder)
                AllEnumBuilders.Add(enumBuilder);
            else if (TryCreateBuilder<IDefinitionBuilder>(type, definitionBuilderInterface) is { } definitionBuilder)
                AllDefinitionBuilders.Add(definitionBuilder);
            else if (TryCreateBuilder<IClientServiceBuilder>(type, clientServiceBuilderInterface) is { } clientBuilder)
                AllClientServiceBuilders.Add(clientBuilder);
            else if (TryCreateBuilder<IServerServiceBuilder>(type, serverServiceBuilderInterface) is { } serviceBuilder)
                AllServerServiceBuilders.Add(serviceBuilder);
            else if (TryCreateBuilder<IValidatorBuilder>(type, validatorBuilderInterface) is { } validatorBuilder)
                AllValidatorBuilders.Add(validatorBuilder);
        }
    }
    
    public IEnumerable<BuiltFile> Build(FileNode fileNode)
    {
        EnsureFeaturesSupported(fileNode);

        BuildContext buildContext = new(fileNode, []);

        foreach (KeyValuePair<string, EnumNode> enumNode in fileNode.Enums)
            EnumBuilder.Build(buildContext, enumNode.Value);
        
        foreach (KeyValuePair<string, DefinitionNode> definitionNode in fileNode.Definitions)
            DefinitionBuilder.Build(buildContext, definitionNode.Value);

        foreach (KeyValuePair<string, UnionNode> unionNode in fileNode.Unions)
            BuildUnion(buildContext, unionNode.Value);
        
        if (ClientServiceBuilder is not null)
            foreach (KeyValuePair<string, ServiceNode> serviceNode in fileNode.Services)
                ClientServiceBuilder.Build(buildContext, serviceNode.Value);
        
        if (ServerServiceBuilder is not null)
            foreach (KeyValuePair<string, ServiceNode> serviceNode in fileNode.Services)
                ServerServiceBuilder.Build(buildContext, serviceNode.Value);

        if (ValidatorBuilder is not null)
            foreach (KeyValuePair<string, DefinitionNode> definitionNode in fileNode.Definitions)
                ValidatorBuilder.Build(buildContext, definitionNode.Value);

        foreach (BuiltFile builtFile in buildContext.Files)
            BuildIncludesForFile(builtFile);

        return buildContext.Files;
    }

    public IEnumerable<CompiledFile> Compile(IEnumerable<BuiltFile> files)
    {
        return files.Select(Compile);
    }

    /// <summary>
    /// Compiles a Built File into one or more source files. Most generators emit a single
    /// file per Built File, but some also need shared or package files.
    /// </summary>
    public virtual IEnumerable<CompiledFile> CompileFiles(BuiltFile file)
    {
        return [Compile(file)];
    }
    
    public abstract CompiledFile Compile(BuiltFile file);
    
    public abstract BuiltInclude? GetCompiledIncludeForType(BuiltFile file, IDataType dataType);
    
    public abstract BuiltDataType GetCompiledDataType(IDataType dataType);
    
    /// <summary>
    /// Ensures the Built File has all the necessary includes it needs based on used Types.
    /// It will not add duplicates.
    /// </summary>
    private void BuildIncludesForFile(BuiltFile builtFile)
    {
        foreach (BuiltDefinition definition in builtFile.Definitions)
        foreach (BuiltConstant constant in definition.Constants)
        {
            BuiltInclude? typeInclude = GetCompiledIncludeForType(builtFile, constant.Node.BuiltType!);
            if (typeInclude is not null && !builtFile.Includes.Contains(typeInclude))
                builtFile.Includes.Add(typeInclude);
        }
        
        foreach (BuiltDefinition definition in builtFile.Definitions)
        foreach (BuiltProperty property in definition.Properties)
        {
            BuiltInclude? typeInclude = GetCompiledIncludeForType(builtFile, property.Node.BuiltType!);
            if (typeInclude is not null && !builtFile.Includes.Contains(typeInclude))
                builtFile.Includes.Add(typeInclude);
        }

        foreach (BuiltService service in builtFile.Services)
        foreach (BuiltEndpoint property in service.Endpoints)
        {
            BuiltInclude? requestTypeInclude = GetCompiledIncludeForType(builtFile, property.Node.BuiltRequestType!);
            BuiltInclude? responseTypeInclude = GetCompiledIncludeForType(builtFile, property.Node.BuiltResponseType!);
            
            if (requestTypeInclude is not null && !builtFile.Includes.Contains(requestTypeInclude))
                builtFile.Includes.Add(requestTypeInclude);

            if (responseTypeInclude is not null && !builtFile.Includes.Contains(responseTypeInclude))
                builtFile.Includes.Add(responseTypeInclude);
        }
        
        builtFile.Includes.Sort((x, y) => string.Compare(x.Path, y.Path, StringComparison.OrdinalIgnoreCase));
        
        // Bit hacky but BuiltFile.Includes doesn't have a setter.
        List<BuiltInclude> distinctIncludes = builtFile.Includes.Distinct().ToList();
        builtFile.Includes.Clear();
        builtFile.Includes.AddRange(distinctIncludes);
    }
    
    public abstract string? GetCompiledNamespace(string? namespaceName);
    public abstract string GetCompiledClassName(string className);
}