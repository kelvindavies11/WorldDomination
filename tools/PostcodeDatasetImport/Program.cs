using System.Buffers.Binary;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Application;
using Game.Domain;

var overpassEndpoints = new[]
{
    "https://overpass.private.coffee/api/interpreter",
    "https://overpass-api.de/api/interpreter",
    "https://lz4.overpass-api.de/api/interpreter",
    "https://overpass.kumi.systems/api/interpreter"
};
using var httpClient = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(15)
};

var legacyPlaceholderSectors = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "NP10 8", "NP18 2", "NP10 4", "NP20 4", "NP19 8", "NP19 4",
    "NP10 3", "NP20 1", "NP20 0", "NP19 0", "NP19 9", "NP10 0",
    "NP20 2", "NP20 3", "NP19 7", "NP18 1"
};

// Non-geographic SA districts to exclude (no real polygons)
var nonGeographicSaDistricts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SA80", "SA99" };

var selectedDistricts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "NP10", "NP18", "NP19", "NP20"
};

var root = FindWorkspaceRoot();
var datasetRoot = Path.Combine(root, "tmp", "GB_Postcodes");
var dbfPath = Path.Combine(datasetRoot, "PostalSector.dbf");
var shpPath = Path.Combine(datasetRoot, "PostalSector.shp");

if (!File.Exists(dbfPath) || !File.Exists(shpPath))
{
    Console.Error.WriteLine($"Dataset files not found under: {datasetRoot}");
    return;
}

var records = ReadDbfRecords(dbfPath);

if (args.Contains("--schema", StringComparer.OrdinalIgnoreCase))
{
    foreach (var field in ReadDbfFields(dbfPath))
    {
        Console.WriteLine($"{field.Name} [{field.Type}] len={field.Length}");
    }

    return;
}

if (args.Contains("--list-np", StringComparer.OrdinalIgnoreCase))
{
    foreach (var record in records
        .Where(r => string.Equals(r["PostArea"], "NP", StringComparison.OrdinalIgnoreCase))
        .Where(r => string.Equals(r["Sprawl"], "Newport", StringComparison.OrdinalIgnoreCase))
        .OrderBy(r => r["GISSect"], StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine($"{record["GISSect"],-6} | RefPC={record["RefPC"],-8} | Count={record["PCCnt"],-4} | Locale={record["Locale"]}");
    }

    return;
}

if (args.Contains("--list-targets", StringComparer.OrdinalIgnoreCase))
{
    foreach (var sector in legacyPlaceholderSectors.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
    {
        var matches = records
            .Where(r => r.Values.Any(value => string.Equals(value, sector, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Console.WriteLine($"[{sector}] matches={matches.Count}");
        foreach (var record in matches)
        {
            Console.WriteLine(
                $"  RMSect={record["RMSect"]}, GISSect={record["GISSect"]}, StrSect={record["StrSect"]}, PostDist={record["PostDist"]}, PostArea={record["PostArea"]}, DistNum={record["DistNum"]}, SecNum={record["SecNum"]}, RefPC={record["RefPC"]}, Sprawl={record["Sprawl"]}, Locale={record["Locale"]}");
        }
    }

    return;
}

var districtArgIndex = Array.FindIndex(args, value => string.Equals(value, "--list-postdist", StringComparison.OrdinalIgnoreCase));
if (districtArgIndex >= 0)
{
    if (districtArgIndex == args.Length - 1)
    {
        throw new ArgumentException("--list-postdist requires a district value, such as NP10.");
    }

    var postDistrict = args[districtArgIndex + 1];
    foreach (var record in records
        .Where(r => string.Equals(r["PostDist"], postDistrict, StringComparison.OrdinalIgnoreCase))
        .OrderBy(r => r["GISSect"], StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine(
            $"RMSect={record["RMSect"]}, GISSect={record["GISSect"]}, StrSect={record["StrSect"]}, SecNum={record["SecNum"]}, RefPC={record["RefPC"]}, Sprawl={record["Sprawl"]}, Locale={record["Locale"]}");
    }

    return;
}

if (args.Contains("--generate-wales-west", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine("Generating West & South West Wales (SA) data using seed metrics...");
    var geoJsonOutputPath = Path.Combine(root, "src", "Game.Application", "Data", "wales-west-postal-sectors.geojson");
    var featuresOutputPath = Path.Combine(root, "src", "Game.Application", "Data", "wales-west-territory-features.json");

    var allGeometries = ReadShapefilePolygons(shpPath).ToList();
    if (allGeometries.Count != records.Count)
    {
        throw new InvalidOperationException($"DBF/Shapefile record count mismatch: dbf={records.Count}, shp={allGeometries.Count}");
    }

    var walesFeatures = new List<JsonObject>();
    var walesMetrics = new JsonObject();

    for (var index = 0; index < records.Count; index++)
    {
        var record = records[index];
        if (!string.Equals(record["PostArea"], "SA", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        var district = record["PostDist"];
        if (nonGeographicSaDistricts.Contains(district))
        {
            continue;
        }

        var sector = record["GISSect"];
        var geometry = allGeometries[index];
        if (geometry.Rings.Count == 0)
        {
            continue;
        }

        var postcodeCount = ParseInt(record["PCCnt"]);

        walesFeatures.Add(new JsonObject
        {
            ["type"] = "Feature",
            ["properties"] = new JsonObject
            {
                ["postcodeSector"] = sector,
                ["name"] = BuildName(sector, record["Locale"]),
                ["postDistrict"] = district,
                ["postArea"] = record["PostArea"],
                ["refPostcode"] = record["RefPC"],
                ["postcodeCount"] = postcodeCount,
                ["sprawl"] = record["Sprawl"],
                ["locale"] = record["Locale"]
            },
            ["geometry"] = geometry.ToJson()
        });

        var seedMetrics = CreateSeedMetrics(geometry, postcodeCount, 5);
        walesMetrics[sector] = ToJson(seedMetrics);
    }

    if (walesFeatures.Count == 0)
    {
        throw new InvalidOperationException("No SA sectors were found in the dataset.");
    }

    var orderedFeatures = walesFeatures
        .OrderBy(f => f["properties"]?["postcodeSector"]?.GetValue<string>(), StringComparer.OrdinalIgnoreCase)
        .ToList();

    var geoJsonObject = new JsonObject
    {
        ["type"] = "FeatureCollection",
        ["features"] = new JsonArray(orderedFeatures.Select(f => (JsonNode?)f.DeepClone()).ToArray())
    };

    File.WriteAllText(
        geoJsonOutputPath,
        geoJsonObject.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) + Environment.NewLine,
        Encoding.UTF8);

    File.WriteAllText(
        featuresOutputPath,
        walesMetrics.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine,
        Encoding.UTF8);

    Console.WriteLine($"Generated {walesFeatures.Count} SA sectors.");
    Console.WriteLine($"  GeoJSON  -> {geoJsonOutputPath}");
    Console.WriteLine($"  Features -> {featuresOutputPath}");
    return;
}

if (args.Contains("--generate-wales", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine("Generating All-Wales data using seed metrics...");
    var geoJsonOutputPath = Path.Combine(root, "src", "Game.Application", "Data", "wales-postal-sectors.geojson");
    var featuresOutputPath = Path.Combine(root, "src", "Game.Application", "Data", "wales-territory-features.json");

    // Welsh postcode areas (by PostArea field)
    var welshAreas = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CF", "NP", "SA", "LL", "LD" };
    // Non-geographic districts to skip (no real polygon boundaries)
    var nonGeographicDistricts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "SA80", "SA99", "LL77", "LL78"
    };
    // Welsh SY districts (Powys/Ceredigion border areas)
    var welshSyDistricts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "SY15", "SY16", "SY17", "SY18", "SY19", "SY20", "SY21", "SY22", "SY23", "SY24", "SY25"
    };
    // Welsh CH districts (Flintshire/Deeside)
    var welshChDistricts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "CH5", "CH6", "CH7", "CH8"
    };

    var allGeometries = ReadShapefilePolygons(shpPath).ToList();
    if (allGeometries.Count != records.Count)
    {
        throw new InvalidOperationException($"DBF/Shapefile record count mismatch: dbf={records.Count}, shp={allGeometries.Count}");
    }

    var allWalesFeatures = new List<JsonObject>();
    var allWalesMetrics = new JsonObject();

    for (var index = 0; index < records.Count; index++)
    {
        var record = records[index];
        var postArea = record["PostArea"];
        var district = record["PostDist"];

        var isWelsh =
            welshAreas.Contains(postArea) ||
            welshSyDistricts.Contains(district) ||
            welshChDistricts.Contains(district);

        if (!isWelsh)
        {
            continue;
        }

        if (nonGeographicDistricts.Contains(district))
        {
            continue;
        }

        var sector = record["GISSect"];
        var geometry = allGeometries[index];
        if (geometry.Rings.Count == 0)
        {
            continue;
        }

        var postcodeCount = ParseInt(record["PCCnt"]);

        allWalesFeatures.Add(new JsonObject
        {
            ["type"] = "Feature",
            ["properties"] = new JsonObject
            {
                ["postcodeSector"] = sector,
                ["name"] = BuildName(sector, record["Locale"]),
                ["postDistrict"] = district,
                ["postArea"] = postArea,
                ["refPostcode"] = record["RefPC"],
                ["postcodeCount"] = postcodeCount,
                ["sprawl"] = record["Sprawl"],
                ["locale"] = record["Locale"]
            },
            ["geometry"] = geometry.ToJson()
        });

        var seedMetrics = CreateSeedMetrics(geometry, postcodeCount, 5);
        allWalesMetrics[sector] = ToJson(seedMetrics);
    }

    if (allWalesFeatures.Count == 0)
    {
        throw new InvalidOperationException("No Welsh sectors were found in the dataset.");
    }

    var orderedWalesFeatures = allWalesFeatures
        .OrderBy(f => f["properties"]?["postcodeSector"]?.GetValue<string>(), StringComparer.OrdinalIgnoreCase)
        .ToList();

    var walesGeoJson = new JsonObject
    {
        ["type"] = "FeatureCollection",
        ["features"] = new JsonArray(orderedWalesFeatures.Select(f => (JsonNode?)f.DeepClone()).ToArray())
    };

    File.WriteAllText(
        geoJsonOutputPath,
        walesGeoJson.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) + Environment.NewLine,
        Encoding.UTF8);

    File.WriteAllText(
        featuresOutputPath,
        allWalesMetrics.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine,
        Encoding.UTF8);

    Console.WriteLine($"Generated {allWalesFeatures.Count} Welsh sectors.");
    Console.WriteLine($"  GeoJSON  -> {geoJsonOutputPath}");
    Console.WriteLine($"  Features -> {featuresOutputPath}");
    return;
}

var geometries = ReadShapefilePolygons(shpPath).ToList();
if (geometries.Count != records.Count)
{
    throw new InvalidOperationException($"DBF/Shapefile record count mismatch: dbf={records.Count}, shp={geometries.Count}");
}

if (args.Contains("--list-bboxes", StringComparer.OrdinalIgnoreCase))
{
    for (var index = 0; index < records.Count; index++)
    {
        var record = records[index];
        if (!string.Equals(record["PostArea"], "NP", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        var sector = record["GISSect"];
        var bounds = geometries[index].GetBounds();
        Console.WriteLine($"{sector,-6} | x=[{bounds.MinX:F5},{bounds.MaxX:F5}] y=[{bounds.MinY:F5},{bounds.MaxY:F5}] | Sprawl={record["Sprawl"]}");
    }

    return;
}

var rewriteGeoJson = args.Contains("--rewrite-geojson", StringComparer.OrdinalIgnoreCase);
var refreshMetricsOnly = args.Contains("--refresh-metrics", StringComparer.OrdinalIgnoreCase);
if (!rewriteGeoJson && !refreshMetricsOnly)
{
    Console.WriteLine("Use --schema, --list-np, --list-targets, --list-postdist, --list-bboxes, --refresh-metrics, --rewrite-geojson, --generate-wales-west, or --generate-wales.");
    return;
}

var replacementFeatures = new List<JsonObject>();
var replacementSectorCodes = new List<string>();
for (var index = 0; index < records.Count; index++)
{
    var record = records[index];
    var sector = record["GISSect"];
    if (!selectedDistricts.Contains(record["PostDist"]))
    {
        continue;
    }

    replacementSectorCodes.Add(sector);

    replacementFeatures.Add(new JsonObject
    {
        ["type"] = "Feature",
        ["properties"] = new JsonObject
        {
            ["postcodeSector"] = sector,
            ["name"] = BuildName(sector, record["Locale"]),
            ["postDistrict"] = record["PostDist"],
            ["postArea"] = record["PostArea"],
            ["refPostcode"] = record["RefPC"],
            ["postcodeCount"] = ParseInt(record["PCCnt"]),
            ["sprawl"] = record["Sprawl"],
            ["locale"] = record["Locale"]
        },
        ["geometry"] = geometries[index].ToJson()
    });
}

if (replacementFeatures.Count == 0)
{
    throw new InvalidOperationException("No Newport replacement sectors were found in the DataShare dataset.");
}

if (rewriteGeoJson)
{
    Console.WriteLine("Refreshing Newport GeoJSON sectors...");
    var geoJsonPath = Path.Combine(root, "src", "Game.Application", "Data", "cardiff-postal-sectors.geojson");
    var geoJsonContent = File.ReadAllText(geoJsonPath);
    var orderedReplacementFeatures = replacementFeatures
        .OrderBy(f => f["properties"]?["postcodeSector"]?.GetValue<string>(), StringComparer.OrdinalIgnoreCase)
        .ToList();
    var replacementGeoJson = string.Join(
        ",",
        orderedReplacementFeatures.Select(feature => feature.ToJsonString(new JsonSerializerOptions { WriteIndented = false })));

    string rewrittenGeoJson;
    try
    {
        var rootNode = JsonNode.Parse(geoJsonContent)?.AsObject()
            ?? throw new InvalidOperationException("Failed to parse GeoJSON.");
        var features = rootNode["features"]?.AsArray()
            ?? throw new InvalidOperationException("GeoJSON does not contain a features array.");

        for (var index = features.Count - 1; index >= 0; index--)
        {
            var feature = features[index]?.AsObject();
            var properties = feature?["properties"]?.AsObject();
            if (string.Equals(properties?["postArea"]?.GetValue<string>(), "NP", StringComparison.OrdinalIgnoreCase))
            {
                features.RemoveAt(index);
            }
        }

        foreach (var feature in orderedReplacementFeatures)
        {
            features.Add(feature);
        }

        rewrittenGeoJson = rootNode.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }
    catch (JsonException)
    {
        const string invalidPlaceholderMarker = ",{\"type\":\"Feature\",\"geometry\":{\"type\":\"Polygon\",\"coordinates\":[[[-3.09,51.49]";
        var markerIndex = geoJsonContent.IndexOf(invalidPlaceholderMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            throw;
        }

        var validPrefix = geoJsonContent[..markerIndex];
        rewrittenGeoJson = validPrefix + "," + replacementGeoJson + "]}";
    }

    File.WriteAllText(geoJsonPath, rewrittenGeoJson + Environment.NewLine, Encoding.UTF8);
}

var featuresJsonPath = Path.Combine(root, "src", "Game.Application", "Data", "cardiff-territory-features.json");
var featuresRoot = JsonNode.Parse(File.ReadAllText(featuresJsonPath))?.AsObject()
    ?? throw new InvalidOperationException("Failed to parse territory features JSON.");
var warnings = new List<string>();

for (var index = 0; index < records.Count; index++)
{
    var record = records[index];
    if (!selectedDistricts.Contains(record["PostDist"]))
    {
        continue;
    }

    var sector = record["GISSect"];
    var geometry = geometries[index];
    var existingConnections = featuresRoot[sector]?["Connections"]?.GetValue<int>() ?? 5;
    var postcodeCount = ParseInt(record["PCCnt"]);
    Console.WriteLine($"Deriving metrics for {sector}...");

    TerritoryFeatureSummary featureSummary;
    try
    {
        featureSummary = await CreateDerivedMetricsAsync(
            httpClient,
            overpassEndpoints,
            sector,
            geometry,
            postcodeCount,
            existingConnections);
    }
    catch (Exception ex)
    {
        warnings.Add($"{sector}: fell back to seed metrics ({ex.Message})");
        featureSummary = CreateSeedMetrics(geometry, postcodeCount, existingConnections);
    }

    featuresRoot[sector] = ToJson(featureSummary);
    await Task.Delay(TimeSpan.FromMilliseconds(400));
}

File.WriteAllText(
    featuresJsonPath,
    featuresRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine,
    Encoding.UTF8);

Console.WriteLine(
    rewriteGeoJson
        ? $"Rewrote GeoJSON with {replacementFeatures.Count} Newport sectors from DataShare and synced metrics entries."
        : $"Refreshed metrics for {replacementFeatures.Count} Newport sectors without rewriting GeoJSON.");
if (warnings.Count > 0)
{
    Console.WriteLine("Warnings:");
    foreach (var warning in warnings)
    {
        Console.WriteLine($"  - {warning}");
    }
}

static string FindWorkspaceRoot()
{
    var current = AppContext.BaseDirectory;
    while (!string.IsNullOrEmpty(current))
    {
        if (File.Exists(Path.Combine(current, "Game.sln")))
        {
            return current;
        }

        current = Path.GetDirectoryName(current);
    }

    throw new InvalidOperationException("Could not locate workspace root.");
}

static string BuildName(string sector, string locale)
    => string.IsNullOrWhiteSpace(locale) ? sector : $"{sector} - {locale}";

static int ParseInt(string value)
    => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0;

static TerritoryFeatureSummary CreateSeedMetrics(ShapeGeometry geometry, int postcodeCount, int connections)
{
    var areaSquareKm = Math.Round(geometry.GetAreaSquareKm(), 3, MidpointRounding.AwayFromZero);
    return TerritoryFeatureSummary.Empty with
    {
        AreaSquareKm = areaSquareKm,
        Connections = connections,
        PopulationSupport = postcodeCount
    };
}

static JsonObject ToJson(TerritoryFeatureSummary summary)
{
    return new JsonObject
    {
        ["Airports"] = summary.Airports,
        ["AreaSquareKm"] = summary.AreaSquareKm,
        ["BridgesOrTunnels"] = summary.BridgesOrTunnels,
        ["CastlesOrForts"] = summary.CastlesOrForts,
        ["Chokepoints"] = summary.Chokepoints,
        ["CommercialAreas"] = summary.CommercialAreas,
        ["Connections"] = summary.Connections,
        ["Factories"] = summary.Factories,
        ["FarmlandOrResources"] = summary.FarmlandOrResources,
        ["GovernmentSites"] = summary.GovernmentSites,
        ["Hills"] = summary.Hills,
        ["IndustrialSites"] = summary.IndustrialSites,
        ["MilitarySites"] = summary.MilitarySites,
        ["Mountains"] = summary.Mountains,
        ["Offices"] = summary.Offices,
        ["PopulationSupport"] = summary.PopulationSupport,
        ["Ports"] = summary.Ports,
        ["Railways"] = summary.Railways,
        ["Roads"] = summary.Roads,
        ["Shops"] = summary.Shops,
        ["SpecialFeatures"] = summary.SpecialFeatures,
        ["UrbanDensity"] = summary.UrbanDensity
    };
}

static async Task<TerritoryFeatureSummary> CreateDerivedMetricsAsync(
    HttpClient httpClient,
    IReadOnlyList<string> overpassEndpoints,
    string sector,
    ShapeGeometry geometry,
    int postcodeCount,
    int connections)
{
    var areaSquareKm = Math.Round(geometry.GetAreaSquareKm(), 3, MidpointRounding.AwayFromZero);
    var bounds = geometry.GetWgs84Bounds();
    var tagSets = await FetchTagSetsAsync(httpClient, overpassEndpoints, bounds);
    var mapped = OsmTerritoryFeatureMapper.Map(tagSets, areaSquareKm, connections);
    return mapped with
    {
        PopulationSupport = Math.Max(mapped.PopulationSupport, postcodeCount)
    };
}

static async Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> FetchTagSetsAsync(
    HttpClient httpClient,
    IReadOnlyList<string> overpassEndpoints,
    (double South, double West, double North, double East) bounds)
{
    var query = FormattableString.Invariant(
        $"[out:json][timeout:12];(node({bounds.South:F6},{bounds.West:F6},{bounds.North:F6},{bounds.East:F6});way({bounds.South:F6},{bounds.West:F6},{bounds.North:F6},{bounds.East:F6}););out tags;"
    );

    Exception? lastError = null;
    foreach (var endpoint in overpassEndpoints)
    {
        try
        {
            using var response = await httpClient.PostAsync(
                endpoint,
                new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("data", query) }));
            response.EnsureSuccessStatusCode();

            var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())?.AsObject()
                ?? throw new InvalidOperationException("Overpass returned an empty response.");
            var elements = json["elements"]?.AsArray()
                ?? throw new InvalidOperationException("Overpass response did not contain an elements array.");

            return elements
                .Select(element => element?["tags"]?.AsObject())
                .Where(tags => tags is not null)
                .Select(tags => (IReadOnlyDictionary<string, string>)tags!
                    .ToDictionary(property => property.Key, property => property.Value?.GetValue<string>() ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }
        catch (Exception ex)
        {
            lastError = ex;
        }
    }

    throw new InvalidOperationException($"All Overpass endpoints failed. Last error: {lastError?.Message}", lastError);
}

static IReadOnlyList<DbfField> ReadDbfFields(string path)
{
    using var stream = File.OpenRead(path);
    using var reader = new BinaryReader(stream, Encoding.ASCII);
    var header = reader.ReadBytes(32);
    var headerLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(8, 2));
    var fieldBytes = headerLength - 33;
    var fieldCount = fieldBytes / 32;
    var fields = new List<DbfField>(fieldCount);
    for (var index = 0; index < fieldCount; index++)
    {
        var field = reader.ReadBytes(32);
        var nameBytes = field.TakeWhile(b => b != 0).ToArray();
        fields.Add(new DbfField(Encoding.ASCII.GetString(nameBytes), (char)field[11], field[16]));
    }

    return fields;
}

static List<Dictionary<string, string>> ReadDbfRecords(string path)
{
    using var stream = File.OpenRead(path);
    using var reader = new BinaryReader(stream, Encoding.ASCII);
    var header = reader.ReadBytes(32);
    var recordCount = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4, 4));
    var headerLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(8, 2));
    var recordLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(10, 2));
    var fields = ReadDbfFields(path);
    stream.Position = headerLength;

    var records = new List<Dictionary<string, string>>(recordCount);
    for (var recordIndex = 0; recordIndex < recordCount; recordIndex++)
    {
        var buffer = reader.ReadBytes(recordLength);
        if (buffer.Length < recordLength || buffer[0] == (byte)'*')
        {
            continue;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var offset = 1;
        foreach (var field in fields)
        {
            var raw = Encoding.ASCII.GetString(buffer, offset, field.Length);
            values[field.Name] = raw.Trim();
            offset += field.Length;
        }

        records.Add(values);
    }

    return records;
}

static IEnumerable<ShapeGeometry> ReadShapefilePolygons(string path)
{
    using var stream = File.OpenRead(path);
    using var reader = new BinaryReader(stream, Encoding.ASCII);
    _ = reader.ReadBytes(100);

    while (stream.Position < stream.Length)
    {
        var recordHeader = reader.ReadBytes(8);
        if (recordHeader.Length == 0)
        {
            yield break;
        }

        var contentLengthWords = BinaryPrimitives.ReadInt32BigEndian(recordHeader.AsSpan(4, 4));
        var content = reader.ReadBytes(contentLengthWords * 2);
        if (content.Length == 0)
        {
            yield break;
        }

        var shapeType = BinaryPrimitives.ReadInt32LittleEndian(content.AsSpan(0, 4));
        if (shapeType == 0)
        {
            yield return new ShapeGeometry(Array.Empty<List<(double X, double Y)>>());
            continue;
        }

        if (shapeType != 5)
        {
            throw new InvalidOperationException($"Unsupported shape type: {shapeType}");
        }

        var numParts = BinaryPrimitives.ReadInt32LittleEndian(content.AsSpan(36, 4));
        var numPoints = BinaryPrimitives.ReadInt32LittleEndian(content.AsSpan(40, 4));
        var partOffsets = new int[numParts + 1];
        for (var index = 0; index < numParts; index++)
        {
            partOffsets[index] = BinaryPrimitives.ReadInt32LittleEndian(content.AsSpan(44 + (index * 4), 4));
        }

        partOffsets[numParts] = numPoints;
        var pointsStart = 44 + (numParts * 4);
        var rings = new List<List<(double X, double Y)>>(numParts);
        for (var partIndex = 0; partIndex < numParts; partIndex++)
        {
            var ring = new List<(double X, double Y)>();
            for (var pointIndex = partOffsets[partIndex]; pointIndex < partOffsets[partIndex + 1]; pointIndex++)
            {
                var pointOffset = pointsStart + (pointIndex * 16);
                var x = BitConverter.ToDouble(content, pointOffset);
                var y = BitConverter.ToDouble(content, pointOffset + 8);
                ring.Add((x, y));
            }

            rings.Add(ring);
        }

        yield return new ShapeGeometry(rings);
    }
}

sealed record DbfField(string Name, char Type, int Length);

sealed class ShapeGeometry(IReadOnlyList<List<(double X, double Y)>> rings)
{
    public IReadOnlyList<List<(double X, double Y)>> Rings { get; } = rings;

    public JsonObject ToJson()
    {
        if (Rings.Count == 0)
        {
            return new JsonObject
            {
                ["type"] = "Polygon",
                ["coordinates"] = new JsonArray()
            };
        }

        if (Rings.Count == 1)
        {
            return new JsonObject
            {
                ["type"] = "Polygon",
                ["coordinates"] = new JsonArray(ToRingArray(Rings[0]))
            };
        }

        var polygons = new JsonArray();
        foreach (var ring in Rings)
        {
            polygons.Add(new JsonArray(ToRingArray(ring)));
        }

        return new JsonObject
        {
            ["type"] = "MultiPolygon",
            ["coordinates"] = polygons
        };
    }

    public (double MinX, double MinY, double MaxX, double MaxY) GetBounds()
    {
        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;

        foreach (var ring in Rings)
        {
            foreach (var (x, y) in ring)
            {
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (minX == double.MaxValue)
        {
            return (0, 0, 0, 0);
        }

        return (minX, minY, maxX, maxY);
    }

    public (double South, double West, double North, double East) GetWgs84Bounds()
    {
        var bounds = GetBounds();
        if (bounds.MinX == 0 && bounds.MinY == 0 && bounds.MaxX == 0 && bounds.MaxY == 0)
        {
            return (0, 0, 0, 0);
        }

        var corners = new[]
        {
            EastingNorthingToWgs84(bounds.MinX, bounds.MinY),
            EastingNorthingToWgs84(bounds.MinX, bounds.MaxY),
            EastingNorthingToWgs84(bounds.MaxX, bounds.MinY),
            EastingNorthingToWgs84(bounds.MaxX, bounds.MaxY)
        };

        return (
            corners.Min(corner => corner.Latitude),
            corners.Min(corner => corner.Longitude),
            corners.Max(corner => corner.Latitude),
            corners.Max(corner => corner.Longitude));
    }

    public double GetAreaSquareKm()
    {
        double area = 0;
        foreach (var ring in Rings)
        {
            area += Math.Abs(GetRingAreaSquareMeters(ring));
        }

        return area / 1_000_000d;
    }

    private static double GetRingAreaSquareMeters(IReadOnlyList<(double X, double Y)> ring)
    {
        if (ring.Count < 4)
        {
            return 0;
        }

        double sum = 0;
        for (var index = 0; index < ring.Count - 1; index++)
        {
            var current = ring[index];
            var next = ring[index + 1];
            sum += (current.X * next.Y) - (next.X * current.Y);
        }

        return sum / 2d;
    }

    private static JsonArray ToRingArray(IEnumerable<(double X, double Y)> ring)
    {
        var result = new JsonArray();
        foreach (var (x, y) in ring)
        {
            var (lat, lon) = EastingNorthingToWgs84(x, y);
            result.Add(new JsonArray(lon, lat));
        }

        return result;
    }

    private static (double Latitude, double Longitude) EastingNorthingToWgs84(double easting, double northing)
    {
        const double airyA = 6377563.396;
        const double airyB = 6356256.909;
        const double scaleFactor = 0.9996012717;
        const double trueOriginLat = 49d * Math.PI / 180d;
        const double trueOriginLon = -2d * Math.PI / 180d;
        const double trueOriginNorthing = -100000d;
        const double trueOriginEasting = 400000d;
        var eccentricitySquared = 1d - ((airyB * airyB) / (airyA * airyA));
        var n = (airyA - airyB) / (airyA + airyB);

        var latitude = trueOriginLat;
        double meridionalArc;
        do
        {
            latitude = (northing - trueOriginNorthing - MeridionalArc(airyB, scaleFactor, n, trueOriginLat, latitude)) / (airyA * scaleFactor) + latitude;
            meridionalArc = MeridionalArc(airyB, scaleFactor, n, trueOriginLat, latitude);
        }
        while (northing - trueOriginNorthing - meridionalArc >= 0.00001d);

        var sinLatitude = Math.Sin(latitude);
        var cosLatitude = Math.Cos(latitude);
        var tanLatitude = Math.Tan(latitude);
        var nu = airyA * scaleFactor / Math.Sqrt(1d - eccentricitySquared * sinLatitude * sinLatitude);
        var rho = airyA * scaleFactor * (1d - eccentricitySquared) / Math.Pow(1d - eccentricitySquared * sinLatitude * sinLatitude, 1.5d);
        var etaSquared = nu / rho - 1d;
        var tan2 = tanLatitude * tanLatitude;
        var tan4 = tan2 * tan2;
        var tan6 = tan4 * tan2;
        var secLatitude = 1d / cosLatitude;
        var nu3 = nu * nu * nu;
        var nu5 = nu3 * nu * nu;
        var nu7 = nu5 * nu * nu;
        var deltaEasting = easting - trueOriginEasting;

        var vii = tanLatitude / (2d * rho * nu);
        var viii = tanLatitude / (24d * rho * nu3) * (5d + 3d * tan2 + etaSquared - 9d * tan2 * etaSquared);
        var ix = tanLatitude / (720d * rho * nu5) * (61d + 90d * tan2 + 45d * tan4);
        var x = secLatitude / nu;
        var xi = secLatitude / (6d * nu3) * (nu / rho + 2d * tan2);
        var xii = secLatitude / (120d * nu5) * (5d + 28d * tan2 + 24d * tan4);
        var xiia = secLatitude / (5040d * nu7) * (61d + 662d * tan2 + 1320d * tan4 + 720d * tan6);

        var osgbLatitude = latitude - vii * deltaEasting * deltaEasting + viii * Math.Pow(deltaEasting, 4d) - ix * Math.Pow(deltaEasting, 6d);
        var osgbLongitude = trueOriginLon + x * deltaEasting - xi * Math.Pow(deltaEasting, 3d) + xii * Math.Pow(deltaEasting, 5d) - xiia * Math.Pow(deltaEasting, 7d);

        return HelmertToWgs84(osgbLatitude, osgbLongitude);
    }

    private static double MeridionalArc(double semiMinorAxis, double scaleFactor, double n, double trueOriginLat, double latitude)
    {
        return semiMinorAxis * scaleFactor *
            ((1d + n + (5d / 4d) * n * n + (5d / 4d) * Math.Pow(n, 3d)) * (latitude - trueOriginLat)
            - (3d * n + 3d * n * n + (21d / 8d) * Math.Pow(n, 3d)) * Math.Sin(latitude - trueOriginLat) * Math.Cos(latitude + trueOriginLat)
            + ((15d / 8d) * n * n + (15d / 8d) * Math.Pow(n, 3d)) * Math.Sin(2d * (latitude - trueOriginLat)) * Math.Cos(2d * (latitude + trueOriginLat))
            - (35d / 24d) * Math.Pow(n, 3d) * Math.Sin(3d * (latitude - trueOriginLat)) * Math.Cos(3d * (latitude + trueOriginLat)));
    }

    private static (double Latitude, double Longitude) HelmertToWgs84(double latitude, double longitude)
    {
        const double airyA = 6377563.396;
        const double airyB = 6356256.909;
        const double wgs84A = 6378137d;
        const double wgs84B = 6356752.3141;
        const double tx = 446.448;
        const double ty = -125.157;
        const double tz = 542.06;
        const double scale = 20.4894e-6;
        const double rx = 0.1502 * Math.PI / (180d * 3600d);
        const double ry = 0.2470 * Math.PI / (180d * 3600d);
        const double rz = 0.8421 * Math.PI / (180d * 3600d);

        var airyEccentricitySquared = 1d - ((airyB * airyB) / (airyA * airyA));
        var wgs84EccentricitySquared = 1d - ((wgs84B * wgs84B) / (wgs84A * wgs84A));

        var sinLatitude = Math.Sin(latitude);
        var cosLatitude = Math.Cos(latitude);
        var sinLongitude = Math.Sin(longitude);
        var cosLongitude = Math.Cos(longitude);
        var nu = airyA / Math.Sqrt(1d - airyEccentricitySquared * sinLatitude * sinLatitude);

        var x1 = nu * cosLatitude * cosLongitude;
        var y1 = nu * cosLatitude * sinLongitude;
        var z1 = (nu * (1d - airyEccentricitySquared)) * sinLatitude;

        var x2 = tx + (1d + scale) * x1 - rz * y1 + ry * z1;
        var y2 = ty + rz * x1 + (1d + scale) * y1 - rx * z1;
        var z2 = tz - ry * x1 + rx * y1 + (1d + scale) * z1;

        var convertedLongitude = Math.Atan2(y2, x2);
        var p = Math.Sqrt(x2 * x2 + y2 * y2);
        var convertedLatitude = Math.Atan2(z2, p * (1d - wgs84EccentricitySquared));
        double previousLatitude;

        do
        {
            previousLatitude = convertedLatitude;
            var nu2 = wgs84A / Math.Sqrt(1d - wgs84EccentricitySquared * Math.Pow(Math.Sin(convertedLatitude), 2d));
            convertedLatitude = Math.Atan2(z2 + wgs84EccentricitySquared * nu2 * Math.Sin(convertedLatitude), p);
        }
        while (Math.Abs(convertedLatitude - previousLatitude) > 1e-12);

        return (convertedLatitude * 180d / Math.PI, convertedLongitude * 180d / Math.PI);
    }
}