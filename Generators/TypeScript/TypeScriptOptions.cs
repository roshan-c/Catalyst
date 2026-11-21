namespace Catalyst.Generators.TypeScript;

public enum TypeScriptSchemaMode
{
    Class,
    Effect
}

public static class TypeScriptSchemaModeParser
{
    public static TypeScriptSchemaMode Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("class", StringComparison.OrdinalIgnoreCase))
            return TypeScriptSchemaMode.Class;

        if (value.Equals("effect", StringComparison.OrdinalIgnoreCase))
            return TypeScriptSchemaMode.Effect;

        throw new InvalidOperationException(
            $"Unsupported TypeScript schema mode '{value}'. Expected 'class' or 'effect'.");
    }
}
