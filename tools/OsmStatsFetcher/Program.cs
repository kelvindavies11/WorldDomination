using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true
};

var sectors = new[]
{
    new Sector("NP10 8", 51.490, -3.090, 51.540, -3.010),
    new Sector("NP18 2", 51.490, -3.010, 51.540, -2.900),
    new Sector("NP10 4", 51.540, -3.090, 51.580, -3.040),
    new Sector("NP20 4", 51.540, -3.040, 51.580, -2.990),
    new Sector("NP19 8", 51.540, -2.990, 51.580, -2.940),
    new Sector("NP19 4", 51.540, -2.940, 51.580, -2.900),
    new Sector("NP10 3", 51.580, -3.090, 51.620, -3.050),
    new Sector("NP20 1", 51.580, -3.050, 51.620, -3.010),
    new Sector("NP20 0", 51.580, -3.010, 51.620, -2.970),
    new Sector("NP19 0", 51.580, -2.970, 51.620, -2.930),
    new Sector("NP19 9", 51.580, -2.930, 51.620, -2.900),
    new Sector("NP10 0", 51.620, -3.090, 51.655, -3.050),
    new Sector("NP20 2", 51.620, -3.050, 51.655, -3.010),
    new Sector("NP20 3", 51.620, -3.010, 51.655, -2.970),
    new Sector("NP19 7", 51.620, -2.970, 51.655, -2.930),
    new Sector("NP18 1", 51.620, -2.930, 51.655, -2.900)
};

var checkOnly = args.Contains("--check", StringComparer.OrdinalIgnoreCase);
var rootPath = FindWorkspaceRoot();
var featuresPath = Path.Combine(rootPath, "src", "Game.Application", "Data", "cardiff-territory-features.json");

using var httpClient = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(60)
};
httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("game-osm-fetcher/1.0");

if (checkOnly)
{
    var sector = sectors.First(s => s.Postcode == "NP20 0");
    var probe = await FetchSectorMetricsAsync(httpClient, sector, CancellationToken.None);
    Console.WriteLine(JsonSerializer.Serialize(probe, jsonOptions));
    return;
}

var rootNode = JsonNode.Parse(await File.ReadAllTextAsync(featuresPath))?.AsObject()
    ?? throw new InvalidOperationException("Failed to parse features JSON.");

var fetched = new List<(Sector Sector, Metrics Metrics)>();
foreach (var sector in sectors)
{
    Console.WriteLine($"Querying {sector.Postcode}...");
    var metrics = await FetchSectorMetricsAsync(httpClient, sector, CancellationToken.None);
    fetched.Add((sector, metrics));
    Console.WriteLine($"  roads={metrics.Roads}, shops={metrics.Shops}, urban={metrics.UrbanDensity}, area={metrics.AreaSquareKm.ToString(CultureInfo.InvariantCulture)}");
    await Task.Delay(TimeSpan.FromMilliseconds(1500));
}

foreach (var (sector, metrics) in fetched)
{
    var existingConnections = rootNode[sector.Postcode]?["Connections"]?.GetValue<int>() ?? 5;
    rootNode[sector.Postcode] = metrics.ToJson(existingConnections);
}

await File.WriteAllTextAsync(
    featuresPath,
    rootNode.ToJsonString(jsonOptions) + Environment.NewLine,
    Encoding.UTF8);

Console.WriteLine($"Updated {fetched.Count} Newport sectors.");

static async Task<Metrics> FetchSectorMetricsAsync(HttpClient httpClient, Sector sector, CancellationToken cancellationToken)
{
    var query = BuildQuery(sector);
    using var content = new FormUrlEncodedContent(new[]
    {
        new KeyValuePair<string, string>("data", query)
    });

    using var response = await httpClient.PostAsync("https://overpass-api.de/api/interpreter", content, cancellationToken);
    var body = await response.Content.ReadAsStringAsync(cancellationToken);
    response.EnsureSuccessStatusCode();

    using var json = JsonDocument.Parse(body);
    if (!json.RootElement.TryGetProperty("elements", out var elements))
    {
        throw new InvalidOperationException("Overpass response did not contain elements.");
    }

    var metrics = new Metrics
    {
        AreaSquareKm = CalculateAreaSquareKm(sector)
    };

    foreach (var element in elements.EnumerateArray())
    {
        if (!element.TryGetProperty("tags", out var tags))
        {
            continue;
        }

        var hasShop = Has(tags, "shop") || HasValue(tags, "amenity", "marketplace");
        var hasFactory = HasValue(tags, "building", "industrial") || HasValue(tags, "man_made", "works", "factory");
        var hasCommercialArea = HasValue(tags, "landuse", "commercial", "retail");
        var hasOffice = Has(tags, "office");
        var hasIndustrialSite = HasValue(tags, "landuse", "industrial") || HasValue(tags, "building", "warehouse", "industrial");
        var hasResources = HasValue(tags, "landuse", "farmland", "farmyard", "quarry")
            || HasValue(tags, "man_made", "petroleum_well", "power_plant", "water_works")
            || Has(tags, "power");
        var hasPopulationSupport = HasValue(tags, "landuse", "residential")
            || HasValue(tags, "amenity", "school", "college", "university", "hospital", "clinic", "doctors");
        var hasMountain = HasValue(tags, "natural", "peak", "mountain_range");
        var hasHill = HasValue(tags, "natural", "hill", "ridge") || Has(tags, "ele");
        var hasMilitary = Has(tags, "military") || HasValue(tags, "landuse", "military");
        var hasCastle = HasValue(tags, "historic", "castle", "fort", "citywalls") || HasValue(tags, "building", "castle");
        var hasGovernment = HasValue(tags, "office", "government") || HasValue(tags, "amenity", "townhall", "courthouse", "police", "fire_station");
        var hasChokepoint = Has(tags, "waterway") || HasValue(tags, "natural", "water", "coastline") || HasValue(tags, "barrier", "retaining_wall", "city_wall");
        var hasUrbanDensity = Has(tags, "building") || HasValue(tags, "landuse", "residential", "commercial", "retail");
        var hasRoad = Has(tags, "highway");
        var hasRail = Has(tags, "railway");
        var hasBridgeOrTunnel = Has(tags, "bridge") || Has(tags, "tunnel");
        var hasAirport = Has(tags, "aeroway");
        var hasPort = HasValue(tags, "amenity", "ferry_terminal") || HasValue(tags, "harbour", "yes") || HasValue(tags, "leisure", "marina");
        var hasInfraSpecial = HasValue(tags, "amenity", "hospital", "university") || HasValue(tags, "power", "plant", "substation");

        if (hasShop) metrics.Shops++;
        if (hasFactory) metrics.Factories++;
        if (hasCommercialArea) metrics.CommercialAreas++;
        if (hasOffice) metrics.Offices++;
        if (hasIndustrialSite) metrics.IndustrialSites++;
        if (hasResources) metrics.FarmlandOrResources++;
        if (hasPopulationSupport) metrics.PopulationSupport++;
        if (hasMountain) metrics.Mountains++;
        if (hasHill) metrics.Hills++;
        if (hasMilitary)
        {
            metrics.MilitarySites++;
            metrics.SpecialFeatures++;
        }
        if (hasCastle)
        {
            metrics.CastlesOrForts++;
            metrics.SpecialFeatures++;
        }
        if (hasGovernment) metrics.GovernmentSites++;
        if (hasChokepoint) metrics.Chokepoints++;
        if (hasUrbanDensity) metrics.UrbanDensity++;
        if (hasRoad) metrics.Roads++;
        if (hasRail) metrics.Railways++;
        if (hasBridgeOrTunnel) metrics.BridgesOrTunnels++;
        if (hasAirport)
        {
            metrics.Airports++;
            metrics.SpecialFeatures++;
        }
        if (hasPort)
        {
            metrics.Ports++;
            metrics.SpecialFeatures++;
        }
        if (hasInfraSpecial) metrics.SpecialFeatures++;
    }

    return metrics;
}

static string BuildQuery(Sector sector)
{
    var south = sector.South.ToString(CultureInfo.InvariantCulture);
    var west = sector.West.ToString(CultureInfo.InvariantCulture);
    var north = sector.North.ToString(CultureInfo.InvariantCulture);
    var east = sector.East.ToString(CultureInfo.InvariantCulture);
    return $"[out:json][timeout:45];\n(\n"
        + $"  nwr[\"shop\"]({south},{west},{north},{east});\n"
        + $"  nwr[\"amenity\"~\"^(marketplace|school|college|university|hospital|clinic|doctors|townhall|courthouse|police|fire_station|ferry_terminal)$\"]({south},{west},{north},{east});\n"
        + $"  nwr[\"building\"]({south},{west},{north},{east});\n"
        + $"  nwr[\"man_made\"~\"^(works|factory|petroleum_well|power_plant|water_works)$\"]({south},{west},{north},{east});\n"
        + $"  nwr[\"landuse\"~\"^(commercial|retail|industrial|farmland|farmyard|quarry|residential|military)$\"]({south},{west},{north},{east});\n"
        + $"  nwr[\"office\"]({south},{west},{north},{east});\n"
        + $"  nwr[\"power\"]({south},{west},{north},{east});\n"
        + $"  nwr[\"natural\"~\"^(peak|mountain_range|hill|ridge|water|coastline)$\"]({south},{west},{north},{east});\n"
        + $"  nwr[\"ele\"]({south},{west},{north},{east});\n"
        + $"  nwr[\"military\"]({south},{west},{north},{east});\n"
        + $"  nwr[\"historic\"~\"^(castle|fort|citywalls)$\"]({south},{west},{north},{east});\n"
        + $"  nwr[\"waterway\"]({south},{west},{north},{east});\n"
        + $"  nwr[\"barrier\"~\"^(retaining_wall|city_wall)$\"]({south},{west},{north},{east});\n"
        + $"  nwr[\"highway\"]({south},{west},{north},{east});\n"
        + $"  nwr[\"railway\"]({south},{west},{north},{east});\n"
        + $"  nwr[\"bridge\"]({south},{west},{north},{east});\n"
        + $"  nwr[\"tunnel\"]({south},{west},{north},{east});\n"
        + $"  nwr[\"aeroway\"]({south},{west},{north},{east});\n"
        + $"  nwr[\"harbour\"=\"yes\"]({south},{west},{north},{east});\n"
        + $"  nwr[\"leisure\"=\"marina\"]({south},{west},{north},{east});\n"
        + ");\nout tags;";
}

static double CalculateAreaSquareKm(Sector sector)
{
    var latKm = (sector.North - sector.South) * 111.0;
    var lngKm = (sector.East - sector.West) * 111.0 * Math.Cos(((sector.South + sector.North) / 2.0) * Math.PI / 180.0);
    return Math.Round(latKm * lngKm, 2, MidpointRounding.AwayFromZero);
}

static bool Has(JsonElement tags, string key)
    => tags.TryGetProperty(key, out _);

static bool HasValue(JsonElement tags, string key, params string[] values)
    => tags.TryGetProperty(key, out var value)
        && values.Contains(value.GetString(), StringComparer.OrdinalIgnoreCase);

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

sealed record Sector(string Postcode, double South, double West, double North, double East);

sealed class Metrics
{
    public int Airports { get; set; }
    public double AreaSquareKm { get; set; }
    public int BridgesOrTunnels { get; set; }
    public int CastlesOrForts { get; set; }
    public int Chokepoints { get; set; }
    public int CommercialAreas { get; set; }
    public int Factories { get; set; }
    public int FarmlandOrResources { get; set; }
    public int GovernmentSites { get; set; }
    public int Hills { get; set; }
    public int IndustrialSites { get; set; }
    public int MilitarySites { get; set; }
    public int Mountains { get; set; }
    public int Offices { get; set; }
    public int PopulationSupport { get; set; }
    public int Ports { get; set; }
    public int Railways { get; set; }
    public int Roads { get; set; }
    public int Shops { get; set; }
    public int SpecialFeatures { get; set; }
    public int UrbanDensity { get; set; }

    public JsonObject ToJson(int connections) => new()
    {
        ["Airports"] = Airports,
        ["AreaSquareKm"] = AreaSquareKm,
        ["BridgesOrTunnels"] = BridgesOrTunnels,
        ["CastlesOrForts"] = CastlesOrForts,
        ["Chokepoints"] = Chokepoints,
        ["CommercialAreas"] = CommercialAreas,
        ["Connections"] = connections,
        ["Factories"] = Factories,
        ["FarmlandOrResources"] = FarmlandOrResources,
        ["GovernmentSites"] = GovernmentSites,
        ["Hills"] = Hills,
        ["IndustrialSites"] = IndustrialSites,
        ["MilitarySites"] = MilitarySites,
        ["Mountains"] = Mountains,
        ["Offices"] = Offices,
        ["PopulationSupport"] = PopulationSupport,
        ["Ports"] = Ports,
        ["Railways"] = Railways,
        ["Roads"] = Roads,
        ["Shops"] = Shops,
        ["SpecialFeatures"] = SpecialFeatures,
        ["UrbanDensity"] = UrbanDensity
    };
}