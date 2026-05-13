using Game.Domain;

namespace Game.Application;

public sealed class WalesTerritoryFeatureRepository
{
    private const string DataFileName = "wales-territory-features.json";

    public IReadOnlyDictionary<string, TerritoryFeatureSummary> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", DataFileName);
        if (!File.Exists(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, DataFileName);
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Wales territory feature data file was not found: {DataFileName}", path);
        }

        return CardiffTerritoryFeatureRepository.LoadFromJson(File.ReadAllText(path));
    }
}
