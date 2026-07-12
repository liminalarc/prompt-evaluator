using System.Text.Json;
using Application.Ports;
using Domain;

namespace Application.Scoring;

/// <summary>
/// Passes when the model output is well-formed JSON that conforms to the configured schema.
/// Supports a pragmatic subset of JSON Schema — <c>type</c>, <c>required</c>, <c>properties</c>
/// (recursive), <c>items</c>, and <c>enum</c> — which covers the shapes an SLM/LLM prompt is
/// asked to emit. Kept dependency-free so the scorer stays pure and fast to unit-test.
/// </summary>
public sealed class JsonSchemaScorer : IScorer
{
    private readonly JsonElement _schema;

    public JsonSchemaScorer(ScorerDescriptor descriptor)
    {
        Descriptor = descriptor;
        _schema = JsonDocument.Parse(descriptor.Config).RootElement.Clone();
    }

    public ScorerDescriptor Descriptor { get; }

    public Task<ScoreOutcome> ScoreAsync(ScoringContext context, CancellationToken ct = default)
    {
        JsonElement instance;
        try
        {
            using var doc = JsonDocument.Parse(context.ModelOutput);
            instance = doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            return Task.FromResult(new ScoreOutcome(0.0, false, $"invalid JSON: {ex.Message}"));
        }

        var violation = Validate(_schema, instance, "$");
        return Task.FromResult(violation is null
            ? new ScoreOutcome(1.0, true, null)
            : new ScoreOutcome(0.0, false, violation));
    }

    /// <summary>Returns null when the instance conforms, or the first violation path/reason.</summary>
    private static string? Validate(JsonElement schema, JsonElement instance, string path)
    {
        if (schema.TryGetProperty("enum", out var allowed) && allowed.ValueKind == JsonValueKind.Array)
        {
            var ok = allowed.EnumerateArray().Any(a => JsonElement.DeepEquals(a, instance));
            if (!ok)
                return $"{path}: value not in enum";
        }

        if (schema.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
        {
            var type = typeEl.GetString()!;
            if (!MatchesType(type, instance))
                return $"{path}: expected type '{type}'";

            if (type == "object")
            {
                if (schema.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.Array)
                {
                    foreach (var req in required.EnumerateArray())
                    {
                        var name = req.GetString();
                        if (name is not null && !instance.TryGetProperty(name, out _))
                            return $"{path}: missing required property '{name}'";
                    }
                }

                if (schema.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in props.EnumerateObject())
                    {
                        if (instance.TryGetProperty(prop.Name, out var child))
                        {
                            var childViolation = Validate(prop.Value, child, $"{path}.{prop.Name}");
                            if (childViolation is not null)
                                return childViolation;
                        }
                    }
                }
            }
            else if (type == "array" && schema.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Object)
            {
                var index = 0;
                foreach (var element in instance.EnumerateArray())
                {
                    var itemViolation = Validate(items, element, $"{path}[{index}]");
                    if (itemViolation is not null)
                        return itemViolation;
                    index++;
                }
            }
        }

        return null;
    }

    private static bool MatchesType(string type, JsonElement instance) => type switch
    {
        "object" => instance.ValueKind == JsonValueKind.Object,
        "array" => instance.ValueKind == JsonValueKind.Array,
        "string" => instance.ValueKind == JsonValueKind.String,
        "boolean" => instance.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => instance.ValueKind == JsonValueKind.Null,
        "integer" => instance.ValueKind == JsonValueKind.Number
            && instance.TryGetInt64(out _),
        "number" => instance.ValueKind == JsonValueKind.Number,
        _ => true,
    };
}
