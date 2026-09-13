using System.Text.Json;

using VPNDetection.Middleware;

namespace VPNDetection.AspNetCore.Tests;

/// <summary>
/// The shared conformance corpus sdk/common generates into every SDK repo.
/// </summary>
/// <remarks>
/// It is language-neutral JSON, so a bound arrives as an object with gte/gt/lte/lt keys and an
/// any-of as an array. Rebuilding them into the .NET types here is what keeps the corpus readable
/// by twelve languages instead of carrying one language's spelling.
/// </remarks>
internal static class Corpus
{
    internal static readonly JsonElement Data = Load();

    internal static IReadOnlyList<Condition> Conditions(JsonElement raw)
        => raw.ValueKind == JsonValueKind.Array
            ? raw.EnumerateArray().Select(ToCondition).ToList()
            : new[] { ToCondition(raw) };

    private static Condition ToCondition(JsonElement raw)
    {
        var condition = new Condition();
        foreach (var property in raw.EnumerateObject())
        {
            condition[property.Name] = ToValue(property.Value);
        }
        return condition;
    }

    private static object? ToValue(JsonElement raw) => raw.ValueKind switch
    {
        JsonValueKind.Array => raw.EnumerateArray().Select(ToValue).ToList(),
        JsonValueKind.Object => IsBound(raw) ? ToBound(raw) : ToCondition(raw),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => raw.GetDouble(),
        JsonValueKind.Null => null,
        _ => raw.GetString(),
    };

    private static Bound ToBound(JsonElement raw)
    {
        Bound? bound = null;
        foreach (var property in raw.EnumerateObject())
        {
            var value = property.Value.GetDouble();
            bound = property.Name switch
            {
                "gte" => bound is null ? Bound.Gte(value) : bound.AndGte(value),
                "gt" => bound is null ? Bound.Gt(value) : bound.AndGt(value),
                "lte" => bound is null ? Bound.Lte(value) : bound.AndLte(value),
                _ => bound is null ? Bound.Lt(value) : bound.AndLt(value),
            };
        }
        return bound!;
    }

    private static readonly HashSet<string> BoundKeys = new() { "gte", "gt", "lte", "lt" };

    private static bool IsBound(JsonElement raw)
        => raw.EnumerateObject().Any() && raw.EnumerateObject().All(p => BoundKeys.Contains(p.Name));

    private static JsonElement Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "testdata.json");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
    }
}
