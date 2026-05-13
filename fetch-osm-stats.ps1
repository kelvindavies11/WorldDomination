$ProgressPreference = 'SilentlyContinue'
Set-Location "c:\Users\kelvi\OneDrive\Documents\New project"

$sectors = @(
  @{postcode="NP10 8";S=51.490;W=-3.090;N=51.540;E=-3.010},
  @{postcode="NP18 2";S=51.490;W=-3.010;N=51.540;E=-2.900},
  @{postcode="NP10 4";S=51.540;W=-3.090;N=51.580;E=-3.040},
  @{postcode="NP20 4";S=51.540;W=-3.040;N=51.580;E=-2.990},
  @{postcode="NP19 8";S=51.540;W=-2.990;N=51.580;E=-2.940},
  @{postcode="NP19 4";S=51.540;W=-2.940;N=51.580;E=-2.900},
  @{postcode="NP10 3";S=51.580;W=-3.090;N=51.620;E=-3.050},
  @{postcode="NP20 1";S=51.580;W=-3.050;N=51.620;E=-3.010},
  @{postcode="NP20 0";S=51.580;W=-3.010;N=51.620;E=-2.970},
  @{postcode="NP19 0";S=51.580;W=-2.970;N=51.620;E=-2.930},
  @{postcode="NP19 9";S=51.580;W=-2.930;N=51.620;E=-2.900},
  @{postcode="NP10 0";S=51.620;W=-3.090;N=51.655;E=-3.050},
  @{postcode="NP20 2";S=51.620;W=-3.050;N=51.655;E=-3.010},
  @{postcode="NP20 3";S=51.620;W=-3.010;N=51.655;E=-2.970},
  @{postcode="NP19 7";S=51.620;W=-2.970;N=51.655;E=-2.930},
  @{postcode="NP18 1";S=51.620;W=-2.930;N=51.655;E=-2.900}
)

function Query-Overpass($query) {
  $tmpQ = [System.IO.Path]::GetTempFileName() + ".ql"
  [System.IO.File]::WriteAllText($tmpQ, $query, [System.Text.Encoding]::UTF8)
  $tmpR = [System.IO.Path]::GetTempFileName() + ".json"
  $proc = Start-Process -FilePath "curl.exe" `
    -ArgumentList @("-s", "-X", "POST",
      "https://overpass-api.de/api/interpreter",
      "--data-urlencode", "data@$tmpQ",
      "-o", $tmpR,
      "--max-time", "55") `
    -Wait -PassThru
  $result = ""
  if ($proc.ExitCode -eq 0 -and (Test-Path $tmpR)) {
    $result = [System.IO.File]::ReadAllText($tmpR)
  }
  Remove-Item $tmpQ, $tmpR -ErrorAction SilentlyContinue
  return $result
}

function Count-Features($elements) {
  $f = @{
    Shops=0; Factories=0; CommercialAreas=0; Offices=0; IndustrialSites=0;
    FarmlandOrResources=0; PopulationSupport=0; Mountains=0; Hills=0;
    MilitarySites=0; CastlesOrForts=0; GovernmentSites=0; Chokepoints=0;
    UrbanDensity=0; Roads=0; Railways=0; BridgesOrTunnels=0;
    Airports=0; Ports=0; SpecialFeatures=0
  }
  foreach ($el in $elements) {
    $t = @{}
    if ($null -ne $el.PSObject.Properties["tags"]) {
      $el.tags.PSObject.Properties | ForEach-Object { $t[$_.Name] = $_.Value }
    }
    if ($t.ContainsKey("shop") -or $t["amenity"] -eq "marketplace") { $f.Shops++ }
    if ($t["building"] -eq "industrial" -or $t["man_made"] -in @("works","factory")) { $f.Factories++ }
    if ($t["landuse"] -in @("commercial","retail")) { $f.CommercialAreas++ }
    if ($t.ContainsKey("office")) { $f.Offices++ }
    if ($t["landuse"] -eq "industrial" -or $t["building"] -in @("warehouse","industrial")) { $f.IndustrialSites++ }
    if ($t["landuse"] -in @("farmland","farmyard","quarry") -or $t["man_made"] -in @("petroleum_well","power_plant","water_works") -or $t.ContainsKey("power")) { $f.FarmlandOrResources++ }
    if ($t["landuse"] -eq "residential" -or $t["amenity"] -in @("school","college","university","hospital","clinic","doctors")) { $f.PopulationSupport++ }
    if ($t["natural"] -in @("peak","mountain_range")) { $f.Mountains++ }
    if ($t["natural"] -in @("hill","ridge") -or $t.ContainsKey("ele")) { $f.Hills++ }
    if ($t.ContainsKey("military") -or $t["landuse"] -eq "military") { $f.MilitarySites++; $f.SpecialFeatures++ }
    if ($t["historic"] -in @("castle","fort","citywalls") -or $t["building"] -eq "castle") { $f.CastlesOrForts++; $f.SpecialFeatures++ }
    if ($t["office"] -eq "government" -or $t["amenity"] -in @("townhall","courthouse","police","fire_station")) { $f.GovernmentSites++ }
    if ($t.ContainsKey("waterway") -or $t["natural"] -in @("water","coastline") -or $t["barrier"] -in @("retaining_wall","city_wall")) { $f.Chokepoints++ }
    if ($t.ContainsKey("building") -or $t["landuse"] -in @("residential","commercial","retail")) { $f.UrbanDensity++ }
    if ($t.ContainsKey("highway")) { $f.Roads++ }
    if ($t.ContainsKey("railway")) { $f.Railways++ }
    if ($t.ContainsKey("bridge") -or $t.ContainsKey("tunnel")) { $f.BridgesOrTunnels++ }
    if ($t.ContainsKey("aeroway")) { $f.Airports++; $f.SpecialFeatures++ }
    if ($t["amenity"] -eq "ferry_terminal" -or $t["harbour"] -eq "yes" -or $t["leisure"] -eq "marina") { $f.Ports++; $f.SpecialFeatures++ }
    if ($t["amenity"] -in @("hospital","university") -or $t["power"] -in @("plant","substation")) { $f.SpecialFeatures++ }
  }
  return $f
}

function Bbox-Area($S, $N, $W, $E) {
  $latKm = ($N - $S) * 111.0
  $lngKm = ($E - $W) * 111.0 * [Math]::Cos(($S + $N) / 2 * [Math]::PI / 180)
  return [Math]::Round($latKm * $lngKm, 2)
}

$results = @{}

foreach ($s in $sectors) {
  $code = $s.postcode
  $S = $s.S; $W = $s.W; $N = $s.N; $E = $s.E
  Write-Host "Querying $code ..."

  $q = "[out:json][timeout:45];`n(`n  node($S,$W,$N,$E);`n  way($S,$W,$N,$E);`n);`nout tags;"
  $raw = Query-Overpass $q

  if ($raw.Length -lt 10 -or $raw[0] -ne '{') {
    Write-Host "  WARNING: bad response for $code (len=$($raw.Length), first=[$($raw.Substring(0,[Math]::Min(50,$raw.Length)))])"
    continue
  }

  try {
    $parsed = $raw | ConvertFrom-Json
    $cnt = Count-Features $parsed.elements
    $cnt["AreaSquareKm"] = Bbox-Area $S $N $W $E
    $cnt["Connections"] = 5
    $results[$code] = $cnt
    Write-Host "  $code DONE: $($parsed.elements.Count) elements, area=$($cnt.AreaSquareKm) km2, shops=$($cnt.Shops) roads=$($cnt.Roads) urban=$($cnt.UrbanDensity)"
  } catch {
    Write-Host "  PARSE ERROR for $code`: $_"
  }
  Start-Sleep -Milliseconds 1500
}

Write-Host ""
Write-Host "FETCHED: $($results.Count) / $($sectors.Count)"
if ($results.Count -eq 0) {
  Write-Host "No results - aborting JSON update"
  exit 1
}

# Read and update the features JSON
$featuresPath = "src\Game.Application\Data\cardiff-territory-features.json"
$existing = Get-Content $featuresPath -Raw | ConvertFrom-Json

# Build ordered hashtable preserving existing entries
$out = [System.Collections.Specialized.OrderedDictionary]::new()
$existing.PSObject.Properties | ForEach-Object { $out[$_.Name] = $_.Value }

# Overwrite NP entries with real data
foreach ($code in $results.Keys) {
  $c = $results[$code]
  $out[$code] = [PSCustomObject]@{
    Airports            = $c.Airports
    AreaSquareKm        = $c.AreaSquareKm
    BridgesOrTunnels    = $c.BridgesOrTunnels
    CastlesOrForts      = $c.CastlesOrForts
    Chokepoints         = $c.Chokepoints
    CommercialAreas     = $c.CommercialAreas
    Connections         = $c.Connections
    Factories           = $c.Factories
    FarmlandOrResources = $c.FarmlandOrResources
    GovernmentSites     = $c.GovernmentSites
    Hills               = $c.Hills
    IndustrialSites     = $c.IndustrialSites
    MilitarySites       = $c.MilitarySites
    Mountains           = $c.Mountains
    Offices             = $c.Offices
    PopulationSupport   = $c.PopulationSupport
    Ports               = $c.Ports
    Railways            = $c.Railways
    Roads               = $c.Roads
    Shops               = $c.Shops
    SpecialFeatures     = $c.SpecialFeatures
    UrbanDensity        = $c.UrbanDensity
  }
}

# Convert back to PSCustomObject for JSON serialization
$outObj = [PSCustomObject]$out
$json = $outObj | ConvertTo-Json -Depth 3
[System.IO.File]::WriteAllText((Resolve-Path $featuresPath).Path, $json)
Write-Host "Updated $featuresPath with real OSM data for $($results.Count) sectors."
