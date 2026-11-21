namespace Catalyst.SpecGraph.Nodes;

/// <summary>
/// Represents an Enum within a Spec File.
/// </summary>
public class EnumNode : Node, INodeDescription, ICompilerOptions
{
    public string? Description { get; set; }
    public Dictionary<string, int> Values { get; set; } = [];
    public bool? Flags { get; set; }

    /// <summary>
    /// True when the enum is written to the wire as strings rather than integers.
    /// </summary>
    public bool IsString { get; set; }

    /// <summary>
    /// Enum value label to its wire string. Only used by string enums.
    /// </summary>
    public Dictionary<string, string> WireValues { get; set; } = [];
    public Dictionary<string, GeneratorOptionsNode> CompilerOptions { get; } = [];
}