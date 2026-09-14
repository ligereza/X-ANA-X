namespace Xanax.Core;

public delegate CanonicalExecutionResult CanonicalOperationHandler(
    CanonicalSessionState state,
    IReadOnlyDictionary<string, string> arguments);

public sealed record CanonicalOperationDefinition(
    string Intent,
    string Description,
    CanonicalOperationHandler Handler,
    bool Reversible = false);

/// <summary>
/// Extension point for canonical behavior. Host adapters register missions here;
/// they do not add host-specific branches to the state engine.
/// </summary>
public sealed class CanonicalOperationRegistry
{
    private readonly Dictionary<string, CanonicalOperationDefinition> definitions =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<CanonicalOperationDefinition> Definitions => definitions.Values.ToArray();

    public void Register(CanonicalOperationDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Intent))
            throw new ArgumentException("An operation intent is required.", nameof(definition));
        ArgumentNullException.ThrowIfNull(definition.Handler);
        definitions[definition.Intent] = definition;
    }

    public bool TryApply(
        CanonicalSessionState state,
        CanonicalMissionOperation operation,
        out CanonicalExecutionResult result)
    {
        if (!definitions.TryGetValue(operation.CanonicalIntent, out var definition))
        {
            result = new(false, state, Array.Empty<string>(), new[] { $"unregistered_canonical_intent:{operation.CanonicalIntent}" });
            return false;
        }

        result = definition.Handler(state, operation.Arguments);
        return true;
    }
}
