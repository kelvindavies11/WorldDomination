using System.Text.Json;

namespace Game.Application;

public sealed class WalesWestPostcodeTerritoryRepository
{
    private const string DataFileName = "wales-west-postal-sectors.geojson";

    public IReadOnlyList<PostcodeTerritoryFeature> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", DataFileName);
        if (!File.Exists(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, DataFileName);
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Wales West postcode data file was not found: {DataFileName}", path);
        }

        return CardiffPostcodeTerritoryRepository.LoadFromGeoJson(File.ReadAllText(path));
    }
}
