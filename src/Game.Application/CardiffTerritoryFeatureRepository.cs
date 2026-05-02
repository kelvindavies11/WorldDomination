using System.Text.Json;
using Game.Domain;

namespace Game.Application;

public sealed class CardiffTerritoryFeatureRepository
{
    private const string DataFileName = "cardiff-territory-features.json";

    public IReadOnlyDictionary<string, TerritoryFeatureSummary> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", DataFileName);
        if (!File.Exists(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, DataFileName);
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Cardiff territory feature data file was not found: {DataFileName}", path);
        }

        return LoadFromJson(File.ReadAllText(path));
    }

    public static IReadOnlyDictionary<string, TerritoryFeatureSummary> LoadFromJson(string json)
    {
        var features = JsonSerializer.Deserialize<Dictionary<string, TerritoryFeatureSummary>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (features is null || features.Count == 0)
        {
            throw new InvalidOperationException("Cardiff territory feature data must contain at least one territory.");
        }

        return features.ToDictionary(
            pair => pair.Key.Trim(),
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }
}
