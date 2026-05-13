namespace Game.Application;

public sealed class GameMapService
{
    private readonly List<MatchMapDto> maps =
    [
        new(
            Id: "cardiff",
            Name: "Cardiff & Newport",
            Center: new MapCoordinateDto(-3.045, 51.565),
            CameraBounds:
            [
                new MapCoordinateDto(-3.4500, 51.3400),
                new MapCoordinateDto(-2.8000, 51.7100)
            ],
            BoundaryCoordinates:
            [
                new MapCoordinateDto(-3.4050, 51.4000), // SW near Barry
                new MapCoordinateDto(-3.3450, 51.3720), // Rhoose / St Athan coast
                new MapCoordinateDto(-3.2400, 51.3680), // Llantwit Major coast
                new MapCoordinateDto(-3.1550, 51.3760), // Fontygary coast
                new MapCoordinateDto(-3.0650, 51.3870), // Lavernock / Wentloog approach
                new MapCoordinateDto(-2.9600, 51.4100), // Wentloog Level coast
                new MapCoordinateDto(-2.8950, 51.4400), // Goldcliff / Nash
                new MapCoordinateDto(-2.8550, 51.4880), // Severn estuary south of Newport
                new MapCoordinateDto(-2.8550, 51.5550), // Newport east
                new MapCoordinateDto(-2.8950, 51.6240), // Caldicot / Portskewett
                new MapCoordinateDto(-2.9600, 51.6600), // East valleys north
                new MapCoordinateDto(-3.0550, 51.6650), // Caerphilly / Blackwood north
                new MapCoordinateDto(-3.1600, 51.6620), // Rhymney valley north
                new MapCoordinateDto(-3.2800, 51.6480), // Rhondda / Merthyr approach
                new MapCoordinateDto(-3.3600, 51.6140), // Llantrisant / Rhondda west
                new MapCoordinateDto(-3.4050, 51.5750), // NW near Pontypridd
                new MapCoordinateDto(-3.4050, 51.4700), // West edge mid
                new MapCoordinateDto(-3.4050, 51.4000)  // Close (= start)
            ]),
        new(
            Id: "wales-west",
            Name: "West & South West Wales",
            Center: new MapCoordinateDto(-4.60, 51.90),
            CameraBounds:
            [
                new MapCoordinateDto(-5.8000, 51.4500),
                new MapCoordinateDto(-3.4000, 52.3500)
            ],
            BoundaryCoordinates:
            [
                new MapCoordinateDto(-3.5600, 52.2650), // NE Aberystwyth area
                new MapCoordinateDto(-3.5600, 51.6300), // SE Neath / Port Talbot east
                new MapCoordinateDto(-3.6800, 51.5650), // Swansea Bay / Briton Ferry
                new MapCoordinateDto(-3.8400, 51.5450), // Swansea west
                new MapCoordinateDto(-4.0880, 51.5350), // Gower south / Rhossili
                new MapCoordinateDto(-4.3200, 51.5380), // Burry Port / Kidwelly
                new MapCoordinateDto(-4.6650, 51.5350), // Pendine Sands
                new MapCoordinateDto(-4.9820, 51.5680), // Pembrokeshire south approach
                new MapCoordinateDto(-5.1950, 51.6250), // Pembroke / Milford Haven
                new MapCoordinateDto(-5.4300, 51.6800), // Dale / St Brides Bay
                new MapCoordinateDto(-5.6650, 51.7750), // St Brides Bay north
                new MapCoordinateDto(-5.6650, 51.9950), // North Pembrokeshire
                new MapCoordinateDto(-5.5100, 52.1680), // Fishguard area
                new MapCoordinateDto(-5.0950, 52.2650), // Cardigan / Newport Pembs
                new MapCoordinateDto(-4.6780, 52.2650), // Cardigan Bay south
                new MapCoordinateDto(-4.2650, 52.2650), // Aberaeron direction
                new MapCoordinateDto(-3.8600, 52.2650), // New Quay area
                new MapCoordinateDto(-3.5600, 52.2650)  // Close (= start)
            ]),
        new(
            Id: "north-wales",
            Name: "North Wales",
            Center: new MapCoordinateDto(-3.80, 52.99),
            CameraBounds:
            [
                new MapCoordinateDto(-5.0000, 52.4000),
                new MapCoordinateDto(-2.6000, 53.6000)
            ],
            BoundaryCoordinates:
            [
                new MapCoordinateDto(-2.8500, 52.5500), // SE near Chirk / Llangollen
                new MapCoordinateDto(-2.8500, 53.1500), // E English border mid
                new MapCoordinateDto(-3.0800, 53.3500), // NE near Prestatyn coast
                new MapCoordinateDto(-3.3500, 53.4300), // N coast Rhyl
                new MapCoordinateDto(-3.7500, 53.4200), // N coast Colwyn Bay / Llandudno
                new MapCoordinateDto(-4.2500, 53.4300), // N coast Bangor / Menai
                new MapCoordinateDto(-4.7200, 53.4300), // NW Holyhead / Holy Island
                new MapCoordinateDto(-4.7200, 52.9500), // W Llŷn Peninsula NW
                new MapCoordinateDto(-4.5500, 52.6500), // W coast Pwllheli / Aberdaron
                new MapCoordinateDto(-4.1000, 52.5600), // SW Barmouth / Aberdyfi
                new MapCoordinateDto(-3.5500, 52.5500), // S Bala area
                new MapCoordinateDto(-2.8500, 52.5500)  // Close (= start)
            ]),
        new(
            Id: "mid-wales",
            Name: "Mid Wales",
            Center: new MapCoordinateDto(-3.56, 52.32),
            CameraBounds:
            [
                new MapCoordinateDto(-4.4000, 51.7000),
                new MapCoordinateDto(-2.7000, 53.0000)
            ],
            BoundaryCoordinates:
            [
                new MapCoordinateDto(-2.9100, 51.8400), // SE English border south near Hay-on-Wye
                new MapCoordinateDto(-2.9100, 52.5000), // E English border mid
                new MapCoordinateDto(-3.0000, 52.8100), // NE near Welshpool
                new MapCoordinateDto(-3.5500, 52.7500), // N Machynlleth area
                new MapCoordinateDto(-4.0000, 52.6100), // NW Aberystwyth north
                new MapCoordinateDto(-4.2200, 52.3500), // W Aberystwyth
                new MapCoordinateDto(-4.1500, 52.0000), // SW Lampeter area
                new MapCoordinateDto(-3.8000, 51.8400), // S Brecon / Llanwrtyd
                new MapCoordinateDto(-2.9100, 51.8400)  // Close (= start)
            ]),
        new(
            Id: "south-wales",
            Name: "South Wales",
            Center: new MapCoordinateDto(-3.96, 51.82),
            CameraBounds:
            [
                new MapCoordinateDto(-5.6000, 51.2000),
                new MapCoordinateDto(-2.4000, 52.4000)
            ],
            BoundaryCoordinates:
            [
                new MapCoordinateDto(-2.6000, 51.5500), // SE Chepstow / Severn estuary
                new MapCoordinateDto(-2.6000, 51.9000), // NE Monmouthshire / Abergavenny
                new MapCoordinateDto(-3.0000, 52.1700), // N Brecon Beacons NE
                new MapCoordinateDto(-3.4000, 52.2600), // N Brecon Beacons mid
                new MapCoordinateDto(-4.0000, 52.1800), // N Carmarthen north
                new MapCoordinateDto(-4.2500, 52.2400), // NW Ceredigion south
                new MapCoordinateDto(-4.7500, 52.0500), // W Cardigan Bay south
                new MapCoordinateDto(-5.1500, 51.9200), // W Pembrokeshire NW
                new MapCoordinateDto(-5.3300, 51.8000), // W St Davids Head
                new MapCoordinateDto(-5.3300, 51.6000), // SW Pembrokeshire south
                new MapCoordinateDto(-4.9500, 51.3800), // S Pembroke / coast
                new MapCoordinateDto(-4.2000, 51.3800), // S Gower / Burry Port
                new MapCoordinateDto(-3.7000, 51.3900), // S Swansea Bay
                new MapCoordinateDto(-3.2000, 51.3800), // S Port Talbot
                new MapCoordinateDto(-3.0000, 51.3800), // S Bridgend / Vale coast
                new MapCoordinateDto(-2.6000, 51.5500)  // Close (= start)
            ])
    ];

    public IReadOnlyList<MatchMapDto> ListMaps() => maps;

    public MatchMapDto GetMap(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return maps.Single(map => string.Equals(map.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
