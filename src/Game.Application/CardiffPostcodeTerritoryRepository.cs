using System.Globalization;
using System.Text.Json;
using Microsoft.VisualBasic.FileIO;

namespace Game.Application;

public sealed class CardiffPostcodeTerritoryRepository
{
    private const double BoundaryPaddingDegrees = 0.003;
    private const string DataFileName = "cardiff-postal-sectors.geojson";

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

        return LoadFromGeoJson(File.ReadAllText(path));
    }

    public static IReadOnlyList<PostcodeTerritoryFeature> LoadFromGeoJson(string geoJson)
    {
        using var document = JsonDocument.Parse(geoJson);
        var root = document.RootElement;
        if (!root.TryGetProperty("features", out var featuresElement) ||
            featuresElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Cardiff postal sector GeoJSON must be a FeatureCollection.");
        }

        var features = new List<PostcodeTerritoryFeature>();
        foreach (var featureElement in featuresElement.EnumerateArray())
        {
            var properties = featureElement.GetProperty("properties");
            var sector = RequiredString(properties, "postcodeSector");
            var name = OptionalString(properties, "name") ?? sector;
            var road = OptionalString(properties, "locale");
            var boundary = ReadBoundaryCoordinates(featureElement.GetProperty("geometry"), sector);
            var center = CalculateCenter(boundary);
            features.Add(new PostcodeTerritoryFeature(
                sector,
                name,
                boundary,
                center.Latitude,
                center.Longitude,
                road));
        }

        return features
            .OrderBy(feature => feature.Postcode, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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

        var rows = new List<PostcodeRow>();
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
            rows.Add(new PostcodeRow(
                postcode,
                string.IsNullOrWhiteSpace(road) ? postcode : $"{postcode} - {road}",
                latitude,
                longitude,
                string.IsNullOrWhiteSpace(road) ? null : road));
        }

        return CreateTessellatedFeatures(rows)
            .OrderBy(feature => feature.Postcode, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string RequiredString(JsonElement properties, string name)
    {
        var value = OptionalString(properties, name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Postal sector GeoJSON feature is missing '{name}'.");
        }

        return value;
    }

    private static string? OptionalString(JsonElement properties, string name)
    {
        return properties.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static IReadOnlyList<MapCoordinateDto> ReadBoundaryCoordinates(JsonElement geometry, string sector)
    {
        var type = RequiredGeometryType(geometry, sector);
        var coordinates = geometry.GetProperty("coordinates");
        JsonElement ringElement = type switch
        {
            "Polygon" => coordinates[0],
            "MultiPolygon" => coordinates[0][0],
            _ => throw new InvalidOperationException($"Postal sector '{sector}' has unsupported geometry type '{type}'.")
        };

        var points = ringElement.EnumerateArray()
            .Select(point => new MapCoordinateDto(point[0].GetDouble(), point[1].GetDouble()))
            .ToList();

        if (points.Count < 4)
        {
            throw new InvalidOperationException($"Postal sector '{sector}' must have at least four boundary points.");
        }

        if (points[0] != points[^1])
        {
            points.Add(points[0]);
        }

        return points;
    }

    private static string RequiredGeometryType(JsonElement geometry, string sector)
    {
        if (!geometry.TryGetProperty("type", out var typeElement) ||
            typeElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(typeElement.GetString()))
        {
            throw new InvalidOperationException($"Postal sector '{sector}' is missing a geometry type.");
        }

        return typeElement.GetString()!;
    }

    private static (double Latitude, double Longitude) CalculateCenter(IReadOnlyList<MapCoordinateDto> boundary)
    {
        var usablePoints = boundary.Count > 1 && boundary[0] == boundary[^1]
            ? boundary.Take(boundary.Count - 1)
            : boundary;
        return (
            usablePoints.Average(point => point.Latitude),
            usablePoints.Average(point => point.Longitude));
    }

    private static IReadOnlyList<PostcodeTerritoryFeature> CreateTessellatedFeatures(IReadOnlyList<PostcodeRow> rows)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var orderedRows = rows
            .OrderByDescending(row => row.Latitude)
            .ThenBy(row => row.Longitude)
            .ThenBy(row => row.Postcode, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var columns = ChooseColumnCount(orderedRows.Length);
        var rowCount = orderedRows.Length / columns;
        var minLongitude = orderedRows.Min(row => row.Longitude) - BoundaryPaddingDegrees;
        var maxLongitude = orderedRows.Max(row => row.Longitude) + BoundaryPaddingDegrees;
        var minLatitude = orderedRows.Min(row => row.Latitude) - BoundaryPaddingDegrees;
        var maxLatitude = orderedRows.Max(row => row.Latitude) + BoundaryPaddingDegrees;
        var cellWidth = (maxLongitude - minLongitude) / columns;
        var cellHeight = (maxLatitude - minLatitude) / rowCount;
        var features = new List<PostcodeTerritoryFeature>(orderedRows.Length);

        for (var index = 0; index < orderedRows.Length; index++)
        {
            var row = orderedRows[index];
            var gridRow = index / columns;
            var gridColumn = index % columns;
            var west = minLongitude + gridColumn * cellWidth;
            var east = west + cellWidth;
            var north = maxLatitude - gridRow * cellHeight;
            var south = north - cellHeight;
            var middleLongitude = (west + east) / 2;
            var middleLatitude = (south + north) / 2;

            features.Add(new PostcodeTerritoryFeature(
                row.Postcode,
                row.Name,
                [
                    new(west, south),
                    new(middleLongitude, south),
                    new(east, south),
                    new(east, middleLatitude),
                    new(east, north),
                    new(middleLongitude, north),
                    new(west, north),
                    new(west, middleLatitude),
                    new(west, south)
                ],
                row.Latitude,
                row.Longitude,
                row.Road));
        }

        return features;
    }

    private static int ChooseColumnCount(int territoryCount)
    {
        var target = Math.Sqrt(territoryCount);
        var best = 1;
        var bestDistance = double.MaxValue;
        for (var candidate = 1; candidate <= territoryCount; candidate++)
        {
            if (territoryCount % candidate != 0)
            {
                continue;
            }

            var distance = Math.Abs(candidate - target);
            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
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

    private sealed record PostcodeRow(
        string Postcode,
        string Name,
        double Latitude,
        double Longitude,
        string? Road);
}
