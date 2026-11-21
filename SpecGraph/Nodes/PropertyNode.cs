namespace Catalyst.SpecGraph.Nodes;

public class ValidationAttributes
{
    public double? Min { get; set; }
    public double? Max { get; set; }
    public string? PatternRaw { get; set; }
    public string? Pattern { get; set; }
    public bool MinInclusive { get; set; } = true;
    public bool MaxInclusive { get; set; } = false;
}

/// <summary>
/// How an optional (<c>T?</c>) Property appears on the wire.
/// </summary>
public enum PropertyPresence
{
    /// <summary>The key may be absent and the value may be null.</summary>
    Default,
    /// <summary>The key may be absent, but is never null when present.</summary>
    Optional,
    /// <summary>The key is always present, but the value may be null.</summary>
    Nullable
}

/// <summary>
/// Represents a Property within a Definition.
/// </summary>
public class PropertyNode : DataMemberNode
{
    public ValidationAttributes? Validation { get; set; }
    public PropertyPresence Presence { get; set; } = PropertyPresence.Default;
}