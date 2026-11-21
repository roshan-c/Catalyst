namespace Catalyst.SpecGraph.Nodes;

/// <summary>
/// Represents a Definition within a Spec File.
/// </summary>
public class DefinitionNode : Node, INodeDescription, ICompilerOptions
{
    public string? Description { get; set; }
    public Dictionary<string, PropertyNode> Properties { get; set; } = [];
    public Dictionary<string, ConstantNode> Constants { get; set; } = [];
    public Dictionary<string, GeneratorOptionsNode> CompilerOptions { get; } = [];

    /// <summary>
    /// The union this Definition is a variant of, if any. Set when the graph is built.
    /// </summary>
    public UnionNode? Union { get; set; }

    /// <summary>
    /// This Definition's discriminator value within <see cref="Union"/>.
    /// </summary>
    public string? UnionTag { get; set; }
}