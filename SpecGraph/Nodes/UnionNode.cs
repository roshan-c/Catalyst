using Catalyst.SpecGraph.Properties;

namespace Catalyst.SpecGraph.Nodes;

/// <summary>
/// Represents a discriminated union within a Spec File: one of several Definitions,
/// told apart on the wire by a string discriminator property.
/// </summary>
public class UnionNode : Node, INodeDescription, ICompilerOptions
{
    public string? Description { get; set; }

    /// <summary>
    /// The wire property that names the variant, such as <c>kind</c>.
    /// </summary>
    public required string Discriminator { get; init; }

    /// <summary>
    /// Discriminator value to Definition name, as written in the spec.
    /// </summary>
    public Dictionary<string, string> UnBuiltVariants { get; set; } = [];

    /// <summary>
    /// Discriminator value to the variant's Definition type. Set when the graph is built.
    /// </summary>
    public Dictionary<string, ObjectType> Variants { get; } = [];

    public Dictionary<string, GeneratorOptionsNode> CompilerOptions { get; } = [];
}
