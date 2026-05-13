export function playerStatsTotals(snapshot, factionId) {
  const ownedTerritories = (snapshot?.territories ?? [])
    .filter(territory => territory.ownerFactionId === factionId);
  const playerArmies = (snapshot?.armies ?? [])
    .filter(army => army.factionId === factionId);

  return {
    revenuePerTick: sum(ownedTerritories, territory => territory.stats?.revenuePerTick),
    armyStrength: sum(playerArmies, army => army.strength),
    armyGrowthPerTick: sum(ownedTerritories, territory => territory.stats?.armyGrowthPerTick),
    territoryValue: sum(ownedTerritories, territory => territory.stats?.strategicValue),
    territoryCount: ownedTerritories.length
  };
}

export function playerStatsMarkup(snapshot, factionId) {
  const totals = playerStatsTotals(snapshot, factionId);

  return `
    <div class="player-stats-grid" data-player-stats>
      <div class="player-stat" data-player-revenue>
        <span>Revenue</span>
        <strong>${totals.revenuePerTick}</strong>
      </div>
      <div class="player-stat" data-player-army>
        <span>Army</span>
        <strong>${totals.armyStrength}</strong>
      </div>
      <div class="player-stat" data-player-army-growth>
        <span>Growth</span>
        <strong>+${totals.armyGrowthPerTick}</strong>
      </div>
      <div class="player-stat" data-player-territory-value>
        <span>Territory value</span>
        <strong>${totals.territoryValue}</strong>
      </div>
      <p class="muted">${totals.territoryCount} ${totals.territoryCount === 1 ? "territory" : "territories"}</p>
    </div>
  `;
}

function sum(items, selector) {
  return items.reduce((total, item) => total + (Number(selector(item)) || 0), 0);
}
