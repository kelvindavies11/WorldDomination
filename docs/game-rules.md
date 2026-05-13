# Dynamic OSM World Domination Game Rules

This document is the living rules reference for the game. Future planning and implementation should treat these rules as the default unless the user explicitly changes them.

## Core Concept

The game is a real-time multiplayer world-domination strategy game using OpenStreetMap as the visual playing field.

Generated territories of random size are overlaid on top of the OpenStreetMap view. Real map features influence movement, economy, defense, strategic value, and conflict difficulty.

The game includes both human players and NPC factions.

## Victory

The win condition is total domination.

A faction wins by controlling 100% of the generated map territory (or the configured `winningControlPercentage` threshold for custom games).

The leaderboard tracks each faction's percentage of total map area controlled. Because territories are random sizes, map control is calculated by controlled area, not territory count.

### All Human Players Eliminated

If every human faction loses all of its territories, the game ends immediately. The NPC faction with the highest map-control percentage at that moment is declared the winner. This applies even if no NPC has reached the configured winning-control threshold.

### Stalemate

If no territory changes ownership for **5 consecutive minutes**, the game ends automatically as a stalemate with no winner. This prevents games from stalling indefinitely when all fronts are locked.

The stalemate timer resets any time a territory is captured — by a human player or an NPC. If the timer expires the server broadcasts a `GameEnded` event with no winner faction and the game is marked `Ended`.

## Starting Positions

All factions start small.

### Custom Game Lobby

Custom games follow a pregame lobby phase before the match starts:

1. The host creates a game, which allocates NPC faction slots and assigns NPC starting positions randomly at creation time. NPC positions are fixed for the lifetime of the game and are deterministic per game ID.
2. The host and other players join the game. Each player provides a display name and is assigned a human faction slot in join order (`human-1`, `human-2`, and so on).
3. Each joined player selects their own starting HQ territory from any unclaimed neutral territory. Human starting positions are not filtered for balance; players may choose any neutral territory not already taken by an NPC or another player.
4. Once every joined player has selected a starting territory, the host can start the game.
5. Starting the game locks all position selections and transitions the match to the active `Started` state. No further players can join after the game has started.
6. If a game is ended from the lobby or match UI, it remains visible in the game list in an `Ended` state for reference and cannot be joined again.

### Default Match

The built-in Cardiff match (`cardiff-match`) starts in `Started` state immediately. All human and NPC positions are randomly assigned at match creation. There is no pregame lobby for the default match.

NPC factions are placed randomly but evenly across the map using spacing rules so they do not cluster unfairly.

## Elimination

If a human or NPC faction loses all territories, that faction is permanently eliminated from the match.

NPC factions can eliminate human players.

The faction that captures another faction's final territory receives elimination credit. The leaderboard should record elimination count and eliminated/out status.

## Territory Control

Factions expand by moving armies into neutral or enemy territories.

Capturing territories increases the faction's map-control percentage and may increase economy, mobility, defense, or strategic reach depending on the territory's features.

### Attacking Enemy Territories

Human players can attack an adjacent enemy territory using the **Attack** button in the territory action menu. To initiate an attack:

1. Click on one of your own territories on the map — the action menu appears.
2. The **Attack** button is enabled when at least one adjacent territory is owned by another faction. The **Expand** button is enabled when at least one adjacent neutral territory exists.
3. Click **Attack** (or **Expand**) to enter the move-order panel.
4. Select the target territory and choose how many troops to send.
5. Click **Send** to dispatch the army.

Combat resolves when the army arrives using the base combat model (see **Combat Rules** below). The territory is captured only if the attacker wins.

## Core MVP Loop

The first playable version should implement this loop:

1. Create a match for one chosen map area.
2. Load OpenStreetMap visuals for that area.
3. Generate random territory overlays on top of the map.
4. Calculate Economy, Defense, Mobility, and Strategic Value for each territory from OSM features.
5. Assign NPC starting HQ territories randomly at game creation.
6. Allow players to join the game and select their own starting HQ territory.
7. Once all joined players have selected a start, the host starts the game.
8. Give every faction one HQ territory and one starting army.
9. Let players and NPCs send armies into neutral or enemy territories.
10. Resolve all movement with visible ETAs.
11. Capture neutral territories when armies arrive.
12. Start combat when armies arrive in enemy territories.
13. Allow connected nearby armies to reinforce active battles if they arrive before the battle ends.
14. Permanently eliminate factions that lose all territories.
15. Credit the final captor with the elimination.
16. Continuously update the leaderboard with map-control percentage, rank, elimination count, and eliminated status.
17. End the match when one faction controls 100% of the map.

Pregame lobby updates, territory ownership changes, and leaderboard changes should be delivered from the API layer so all connected human players see the same state at the same time.

The MVP should exclude:

- diplomacy
- alliances
- fog of war
- multiple unit types
- player-built buildings
- manual economy management
- respawning
- naval combat beyond port-to-port travel
- air combat beyond airport-to-airport movement

## Match Defaults

The first match should use:

- map area: Cardiff
- territory count target: 80-120 territories, with 100 as the initial target
- human players: 2 for first testing, expandable to 4
- NPC factions: 6
- starting army strength: 100
- neutral territories: all territories not chosen or assigned as starts
- target match duration: 20-45 minutes for early testing

## NPC Behaviour

NPC factions are controlled by the server and act automatically on a fixed tick interval (every 5 seconds).

### NPC Nature

Every NPC faction is assigned one of three natures at game creation. The nature determines how often the NPC expands and how aggressively it attacks:

| Nature | Tick frequency | Attack behaviour |
|---|---|---|
| **Active** | Every tick | Aggressively attacks adjacent enemy territories — 65% preference for enemies over neutral expansion |
| **Conservative** | Every other tick (1, 3, 5 …) | Expands into neutral freely; attacks an enemy only when its attacking garrison is strictly larger than the defending garrison |
| **Passive** | Every third tick (1, 4, 7 …) | Never attacks enemy territories; only expands into unowned neutral territory |

Natures are assigned cyclically: NPC 1 is Active, NPC 2 is Conservative, NPC 3 is Passive, NPC 4 is Active, and so on.

The NPC's nature is visible in its faction name (for example, "NPC 1 (Active)") on the leaderboard and territory panel.

An Active NPC is the most aggressive — it attacks neighbouring enemy territories relentlessly and expands into neutral territory when no enemies are adjacent. A Passive NPC expands slowly and never initiates attacks against other factions. A Conservative NPC falls in between.

All NPC factions still receive army reinforcements every tick regardless of their nature.

## Army Growth

Every started game produces a reinforcement tick every 5 seconds. Each territory that is owned and currently has a stationed army generates new troops for its owning faction:

  Reinforcement per territory per tick = max(1, Economy / 10)

Territories with higher Economy stats — such as city centres with many shops, offices, and commercial areas — produce more troops. This means that capturing economically productive territory provides a compounding advantage over time.

The leaderboard **Revenue** figure shows a faction's total Economy score across all owned territories, and **Growth** shows the combined growth rate. Both update live via the match feed.



The first playable movement model is territory-to-territory army movement.

Players and NPCs select armies and send them from one territory to another connected territory. Movement happens in real time and always uses an ETA.

Armies do not move instantly. Land, rail, air, and sea travel all have visible travel times.

Incoming army movement should be visible to affected players before arrival so defenders have time to react.

Players should be able to see at least:

- source territory
- destination territory
- owning faction
- army size, unless fog-of-war rules are added later
- ETA
- route type, such as land, road, rail, air, or sea

## Troop Redistribution

A player may move troops from one of their own territories to any directly connected territory they also own.

Rules:

- Source and target must both be owned by the acting faction.
- The source and target must share an allowed route.
- At least one troop must remain in the source territory after the transfer (garrison rule).
- Troops merge into the existing army at the target territory — no combat occurs.
- The same ETA rules apply as for any other movement.

## Physical Movement Rules

OpenStreetMap features affect movement:

- Roads increase land travel speed.
- Railways and train routes increase travel speed.
- Bridges and tunnels allow valid land movement across barriers.
- Mountains decrease travel speed.
- Water and sea cannot be crossed directly.
- Airports allow air travel from an airport territory to the closest valid airport territory.
- Ports allow sea travel from a port territory to another reachable port territory.

For the first version, "closest valid airport" means the nearest airport territory by direct map distance that is controlled by the moving faction or is otherwise allowed by the game mode.

For the first version, "reachable port" means another port territory connected by sea or navigable water without requiring an invalid land crossing. Sea travel still uses an ETA and is not instant.

Road routes may cross water, rivers, or other barriers only when OpenStreetMap shows a valid bridge or tunnel connection.

## Defense Rules

Territory defense is affected by natural and man-made features.

Defense increases from:

- mountains
- hills
- castles
- military sites
- government buildings
- natural chokepoints such as rivers, coastlines, and limited crossings

The defender receives the benefit of the territory's defensive features when combat starts.

## Combat Rules

Combat is local to the territory being attacked.

When an army arrives in an enemy territory, combat begins in that target territory. The first version should use a power-based combat model.

The base combat model is:

```text
attacker_power = attacking_army_strength

defender_power =
  defending_army_strength *
  (1 + territory_defense / 200)
```

This means a territory with `Defense = 100` gives the defender a 50% defensive strength bonus.

The full combat model should use the Attack Position and Defense Position formulas later in this document. The base formula above explains the core defender terrain advantage.

Combat may include a small random battle factor, such as plus or minus 10%, so outcomes are not perfectly predictable.

Casualties should scale with how close the battle was:

- a crushing win leaves the winner with more surviving strength
- a narrow win leaves the winner heavily weakened
- for version one, the losing army is destroyed

## Multi-Territory Battle Support

Battles happen inside one target territory, but nearby territories can influence the battle by sending reinforcements.

If a defending faction owns neighboring or connected territories, armies in those territories may move into the battle as defensive reinforcements.

If an attacking faction owns neighboring or connected territories, armies in those territories may move into the battle as attacking reinforcements.

Reinforcements always use normal movement rules and ETAs. They do not join instantly.

If reinforcements arrive before the battle ends, they join the active battle on their faction's side.

Support depends on connection quality:

- same territory: full strength already present
- road-connected neighbor: fast reinforcement
- rail-connected neighbor: very fast reinforcement if rail access exists
- mountain-separated neighbor: slower reinforcement
- bridge or tunnel connection: valid reinforcement route
- water-separated territory without bridge, tunnel, port, or air route: blocked
- airport support: allowed only through valid airport-to-airport movement with ETA
- port support: allowed only through valid port-to-port sea movement with ETA

The surrounding map matters because a territory is easier to defend or attack when the faction controls connected nearby territories with armies and good transport routes.

## Faction Size Benefits

Large factions benefit from their size through economy, production, reinforcement options, and mobility rather than an automatic combat bonus.

A larger controlled area can provide:

- more total Economy
- more army production or recruitment
- more territories that can send reinforcements
- more road, rail, airport, and port access
- more defensive depth
- higher map-control percentage toward the 100% victory condition

Territory size should not automatically make a local army stronger. Size matters through controlled area, contained OSM features, available armies, connected infrastructure, and support capacity.

Default faction growth formula:

```text
faction_power_growth =
  total_controlled_economy +
  total_controlled_strategic_value +
  connected_mobility_bonus
```

Current MVP comparison detail:

- faction revenue is the sum of `Economy` across all currently controlled territories
- connected mobility bonus is the sum of `Mobility` from owned territories that have at least one allowed route to another owned territory
- faction army growth is exposed as `Revenue + total controlled Strategic Value + connected mobility bonus`
- the authoritative match snapshot and leaderboard rows should include `territoryCount`, `revenue`, `armyStrength`, and `armyGrowth` so clients compare players from API data instead of recalculating locally

Default support formula:

```text
support_strength =
  reinforcing_army_strength *
  support_efficiency
```

Default support efficiency:

- same territory: `1.00`
- road-connected neighbor: `0.85`
- rail-connected neighbor: `0.95`
- basic adjacent land territory: `0.70`
- hill or mountain route: `0.55`
- airport route: `0.75`
- port route: `0.70`
- invalid barrier crossing: `0.00`

## Attack And Defense Position Formulas

Attack and defense should account for more than army size. A faction's position around the target territory affects how strong its attack or defense is.

### Attack Position

Attack Position measures how well the attacker can project force into the target territory.

Default formula:

```text
Attack Position =
  0.35 * staging_strength_score +
  0.25 * route_quality_score +
  0.15 * mobility_advantage_score +
  0.15 * surrounding_pressure_score +
  0.10 * strategic_value_score
```

Default input scores:

- `staging_strength_score`: attacking army strength available in the source and nearby supporting territories
- `route_quality_score`: quality of the route into the target, including roads, rail, bridges, tunnels, airports, or ports
- `mobility_advantage_score`: attacker's mobility compared with the defender's reinforcement mobility
- `surrounding_pressure_score`: number and strength of attacker-controlled territories bordering or connected to the target
- `strategic_value_score`: how valuable the target is to the attacker's expansion or elimination goals

Default route quality scores:

- rail route: `100`
- airport route: `90`
- major road route: `85`
- port route: `75`
- minor road route: `70`
- basic adjacent land route: `55`
- hill or mountain route: `40`
- invalid water or barrier crossing: `0`

### Defense Position

Defense Position measures how well the defender can hold and reinforce the target territory.

Default formula:

```text
Defense Position =
  0.30 * territory_defense_score +
  0.25 * garrison_strength_score +
  0.20 * reinforcement_access_score +
  0.15 * chokepoint_score +
  0.10 * surrounding_control_score
```

Default input scores:

- `territory_defense_score`: the territory's Defense stat
- `garrison_strength_score`: defending army strength already inside the target territory
- `reinforcement_access_score`: strength and ETA quality of friendly reinforcements from connected territories
- `chokepoint_score`: rivers, coastlines, mountains, limited crossings, or few valid approach routes
- `surrounding_control_score`: percentage of neighboring or connected territories controlled by the defender

### Battle Power With Position

Attack Position and Defense Position should modify battle power without overwhelming army size.

Default formula:

```text
position_modifier(position_score) =
  1 + (position_score / 400)

effective_attacker_strength =
  attacking_army_strength *
  position_modifier(Attack Position)

effective_defender_strength =
  defending_army_strength *
  (1 + territory_defense / 200) *
  position_modifier(Defense Position)
```

This means:

- `Attack Position = 0` gives no attack bonus.
- `Attack Position = 100` gives a 25% attack bonus.
- `Defense Position = 0` gives no extra position bonus.
- `Defense Position = 100` gives a 25% defense position bonus, in addition to the territory defense bonus.

The defender still benefits from the territory's own Defense stat. The attacker benefits from staging, route quality, mobility, and surrounding pressure.

### Reinforcement Access Score

Reinforcement Access should consider both strength and arrival time.

Default formula:

```text
reinforcement_access_score =
  capped_score(total_reinforcement_strength, reinforcement_strength_cap) *
  eta_quality

eta_quality =
  max(0, 1 - average_reinforcement_eta_seconds / battle_window_seconds)
```

If reinforcements cannot arrive before the battle is expected to end, their `eta_quality` approaches `0`.

### Surrounding Control Score

Surrounding Control measures local map control around the battle.

Default formula:

```text
surrounding_control_score =
  defender_controlled_connected_neighbors /
  total_connected_neighbors *
  100
```

For Attack Position, use the same formula with attacker-controlled connected neighbors.

## Territory Stats

Each territory should expose clear stats.

### Economy

Economy increases from:

- factories
- shops
- commercial areas
- offices
- industrial zones
- farmland or resource sites

Economy controls income, production speed, or army growth.

### Defense

Defense represents how hard a territory is to capture.

Defense is increased by terrain, fortifications, military features, government buildings, and chokepoints.

### Mobility

Mobility represents how well connected a territory is.

Mobility increases from:

- roads
- railways
- train stations
- bridges
- tunnels
- airports
- ports

Mobility improves travel speed, reinforcement speed, and strategic reach.

### Strategic Value

Strategic Value is a combined score used for map readability and NPC decision-making.

It may include economy, defense, mobility, territory size, schools, hospitals, government buildings, resources, ports, airports, and other strategically useful OSM features.

## Default Stat Formulas

All territory stats should be calculated on a 0-100 scale.

Raw OSM feature counts should be capped before scoring so that dense city territories do not become impossibly overpowered. The default helper is:

```text
capped_score(value, cap) = min(value, cap) / cap * 100
```

Area-based values should use feature density rather than raw counts where possible:

```text
density = feature_count / territory_area_km2
```

### Economy Formula

Economy measures income, production potential, and army-growth potential.

Default formula:

```text
Economy =
  0.25 * factory_score +
  0.20 * shop_score +
  0.15 * commercial_score +
  0.15 * office_score +
  0.10 * industrial_land_score +
  0.10 * farmland_or_resource_score +
  0.05 * population_support_score
```

Default input scores:

- `factory_score`: factories, manufacturing, industrial buildings
- `shop_score`: shops, retail, supermarkets, malls
- `commercial_score`: commercial land use and business areas
- `office_score`: office buildings and workplace density
- `industrial_land_score`: industrial zones, warehouses, depots
- `farmland_or_resource_score`: farmland, mines, quarries, power/resource infrastructure
- `population_support_score`: residential density, schools, hospitals, and services that imply local population

### Defense Formula

Defense measures how hard a territory is to capture.

Default formula:

```text
Defense =
  0.25 * mountain_score +
  0.15 * hill_score +
  0.15 * military_score +
  0.15 * castle_or_fort_score +
  0.10 * government_score +
  0.10 * chokepoint_score +
  0.10 * urban_defense_score
```

Default input scores:

- `mountain_score`: mountainous or steep terrain
- `hill_score`: hills and elevated terrain
- `military_score`: military bases, barracks, military land use
- `castle_or_fort_score`: castles, forts, historic defensive structures
- `government_score`: government buildings and civic authority sites
- `chokepoint_score`: rivers, coastlines, limited crossings, narrow territory connections
- `urban_defense_score`: dense buildings and urban cover

### Mobility Formula

Mobility measures how well a territory can move, reinforce, and project force.

Default formula:

```text
Mobility =
  0.25 * road_score +
  0.20 * rail_score +
  0.15 * bridge_tunnel_score +
  0.15 * airport_score +
  0.15 * port_score +
  0.10 * connection_score
```

Default input scores:

- `road_score`: road density and road quality
- `rail_score`: rail lines and train stations
- `bridge_tunnel_score`: bridges and tunnels that cross barriers
- `airport_score`: airports, airfields, heliports
- `port_score`: ports, harbours, ferry terminals, marinas
- `connection_score`: number and quality of valid neighboring territory connections

### Strategic Value Formula

Strategic Value is the combined score used for map readability, NPC target selection, and AI planning.

Default formula:

```text
Strategic Value =
  0.35 * Economy +
  0.25 * Defense +
  0.25 * Mobility +
  0.10 * territory_area_score +
  0.05 * special_feature_score
```

Default input scores:

- `territory_area_score`: useful size without making huge territories automatically dominant
- `special_feature_score`: rare or high-impact features such as airports, ports, major government sites, hospitals, universities, castles, major resource sites, or critical crossings

### Movement ETA Formula

Every army movement uses an ETA.

Default formula:

```text
ETA seconds =
  base_distance_seconds *
  terrain_multiplier *
  barrier_multiplier *
  transport_multiplier
```

Default multipliers:

- basic land movement: `transport_multiplier = 1.00`
- road route: `transport_multiplier = 0.70`
- rail route: `transport_multiplier = 0.50`
- air route between airport territories: `transport_multiplier = 0.35`
- sea route between port territories: `transport_multiplier = 0.60`
- hills: `terrain_multiplier = 1.15`
- mountains: `terrain_multiplier = 1.40`
- valid bridge or tunnel crossing: `barrier_multiplier = 1.00`
- invalid water or sea crossing: movement is blocked

## NPC Behavior

NPC factions use the same rules as human players.

NPCs should:

- expand into nearby neutral territories first
- prefer territories with high Economy, Mobility, or Strategic Value
- avoid attacks where the defender is much stronger
- reinforce threatened territories
- use roads, rail, ports, and airports when they reduce ETA
- attack weak nearby factions once neutral expansion slows
- pursue eliminations when an enemy is close to being wiped out

NPCs should not cheat. They should act through the same movement, visibility, and combat systems available to human players.

## Leaderboard

The leaderboard should show:

- map-control percentage
- rank
- elimination count
- eliminated/out status
- optional match history showing which faction eliminated which player

Map-control percentage is the primary score.
