using System.Globalization;

namespace Xanax.Core;

/// <summary>
/// Deterministic permutations over an ordered fixture selection.
/// This is the canonical layer: it does not assume a console or a screen.
/// </summary>
public static class SelectionTransformMath
{
    public static IReadOnlyList<string> Apply(
        IReadOnlyList<string> selection,
        IReadOnlyDictionary<string, string> arguments)
    {
        if (selection.Count == 0)
            return Array.Empty<string>();

        var result = selection.ToList();
        var transform = arguments.TryGetValue("transform", out var rawTransform)
            ? rawTransform.Trim().ToLowerInvariant()
            : "reverse";

        switch (transform)
        {
            case "reverse":
                result.Reverse();
                break;
            case "rotate":
                Rotate(result, ParseInt(arguments, "amount", 1));
                break;
            case "shuffle":
                Shuffle(result, ParseInt(arguments, "seed", 0));
                break;
            case "wings":
                result = Wings(result);
                break;
            case "blocks":
                result = Blocks(result, Math.Max(1, ParseInt(arguments, "size", 1)));
                break;
            default:
                throw new ArgumentException($"Unknown selection transform '{transform}'.", nameof(arguments));
        }

        return result;
    }

    private static int ParseInt(IReadOnlyDictionary<string, string> arguments, string key, int fallback)
    {
        return arguments.TryGetValue(key, out var raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private static void Rotate(List<string> values, int amount)
    {
        if (values.Count == 0)
            return;
        var normalized = ((amount % values.Count) + values.Count) % values.Count;
        if (normalized == 0)
            return;
        var tail = values.TakeLast(normalized).ToArray();
        values.RemoveRange(values.Count - normalized, normalized);
        values.InsertRange(0, tail);
    }

    private static void Shuffle(List<string> values, int seed)
    {
        // A supplied seed makes the permutation reproducible and learnable.
        // No seed means a stable canonical seed, never process-time randomness.
        var random = new Random(seed);
        for (var index = values.Count - 1; index > 0; index--)
        {
            var swapIndex = random.Next(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }

    private static List<string> Wings(IReadOnlyList<string> values)
    {
        var result = new List<string>(values.Count);
        var left = 0;
        var right = values.Count - 1;
        while (left <= right)
        {
            result.Add(values[left]);
            if (left != right)
                result.Add(values[right]);
            left++;
            right--;
        }
        return result;
    }

    private static List<string> Blocks(IReadOnlyList<string> values, int size)
    {
        var result = new List<string>(values.Count);
        for (var start = 0; start < values.Count; start += size)
        {
            var count = Math.Min(size, values.Count - start);
            for (var offset = count - 1; offset >= 0; offset--)
                result.Add(values[start + offset]);
        }
        return result;
    }
}
