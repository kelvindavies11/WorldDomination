using System.Text.Json;
using Game.Domain;

namespace Game.Application;

public sealed class WalesWestTerritoryFeatureRepository
{
    private const string DataFileName = "wales-west-territory-features.json";

    public IReadOnlyDictionary<string, TerritoryFeatureSummary> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", DataFileName);
        if (!File.Exists(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, DataFileName);
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Wales West territory feature data file was not found: {DataFileName}", path);
        }

        return CardiffTerritoryFeatureRepository.LoadFromJson(File.ReadAllText(path));
    }
}
