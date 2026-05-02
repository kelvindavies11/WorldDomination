using System.Globalization;
using Microsoft.VisualBasic.FileIO;

namespace Game.Application;

public sealed class CardiffPostcodeTerritoryRepository
{
    private const double PolygonHalfSizeDegrees = 0.00042;
    private const string DataFileName = "cardiff-postcodes.csv";

    public IReadOnlyList<PostcodeTerritoryFeature> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", DataFileName);
        if (!File.Exists(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, DataFileName);
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Cardiff postcode data file was not found: {DataFileName}", path);
        }

        return LoadFromCsv(File.ReadAllText(path));
    }

    public static IReadOnlyList<PostcodeTerritoryFeature> LoadFromCsv(string csv)
    {
        using var reader = new StringReader(csv);
        using var parser = new TextFieldParser(reader)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");

        var headers = parser.ReadFields() ?? throw new InvalidOperationException("Cardiff postcode CSV is empty.");
        var indexes = headers
            .Select((header, index) => new { Header = header, Index = index })
            .ToDictionary(item => item.Header, item => item.Index, StringComparer.OrdinalIgnoreCase);

        var features = new List<PostcodeTerritoryFeature>();
        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields is null || fields.Length == 0)
            {
                continue;
            }

            var postcode = Field(fields, indexes, "Postcode").Trim();
            var latitudeText = Field(fields, indexes, "Latitude").Trim();
            var longitudeText = Field(fields, indexes, "Longitude").Trim();
            if (string.IsNullOrWhiteSpace(postcode) ||
                string.IsNullOrWhiteSpace(latitudeText) ||
                string.IsNullOrWhiteSpace(longitudeText))
            {
                throw new InvalidOperationException($"Postcode row '{postcode}' is missing coordinates.");
            }

            if (!double.TryParse(latitudeText, CultureInfo.InvariantCulture, out var latitude) ||
                !double.TryParse(longitudeText, CultureInfo.InvariantCulture, out var longitude))
            {
                throw new InvalidOperationException($"Postcode row '{postcode}' has invalid coordinates.");
            }

            var road = FirstRoad(Field(fields, indexes, "Roads"));
            features.Add(new PostcodeTerritoryFeature(
                postcode,
                string.IsNullOrWhiteSpace(road) ? postcode : $"{postcode} - {road}",
                CreateSquare(longitude, latitude),
                latitude,
                longitude,
                string.IsNullOrWhiteSpace(road) ? null : road));
        }

        return features
            .OrderBy(feature => feature.Postcode, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string Field(string[] fields, IReadOnlyDictionary<string, int> indexes, string name)
    {
        if (!indexes.TryGetValue(name, out var index) || index >= fields.Length)
        {
            return string.Empty;
        }

        return fields[index];
    }

    private static string FirstRoad(string roads)
    {
        return roads
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;
    }

    private static IReadOnlyList<MapCoordinateDto> CreateSquare(double longitude, double latitude) =>
    [
        new(longitude - PolygonHalfSizeDegrees, latitude - PolygonHalfSizeDegrees),
        new(longitude + PolygonHalfSizeDegrees, latitude - PolygonHalfSizeDegrees),
        new(longitude + PolygonHalfSizeDegrees, latitude + PolygonHalfSizeDegrees),
        new(longitude - PolygonHalfSizeDegrees, latitude + PolygonHalfSizeDegrees),
        new(longitude - PolygonHalfSizeDegrees, latitude - PolygonHalfSizeDegrees)
    ];
}
