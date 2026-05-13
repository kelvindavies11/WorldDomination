namespace Game.Application;

public sealed class SouthWalesPostcodeTerritoryRepository
{
    private const string DataFileName = "wales-postal-sectors.geojson";
    private static readonly HashSet<string> PostAreas = new(StringComparer.OrdinalIgnoreCase) { "CF", "NP", "SA" };

    public IReadOnlyList<PostcodeTerritoryFeature> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", DataFileName);
        if (!File.Exists(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, DataFileName);
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Wales postcode data file was not found: {DataFileName}", path);
        }

        return CardiffPostcodeTerritoryRepository.LoadFromGeoJson(File.ReadAllText(path))
            .Where(f => PostAreas.Contains(new string(f.Postcode.TakeWhile(char.IsLetter).ToArray())))
            .ToArray();
    }
}
