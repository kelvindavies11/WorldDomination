import assert from "node:assert/strict";
import test from "node:test";

import { playerStatsMarkup, playerStatsTotals } from "../../src/Game.Api/wwwroot/playerStats.mjs";

const snapshot = {
  territories: [
    {
      id: "t1",
      ownerFactionId: "human-1",
      stats: {
        strategicValue: 30,
        revenuePerTick: 120,
        armyGrowthPerTick: 4
      }
    },
    {
      id: "t2",
      ownerFactionId: "human-1",
      stats: {
        strategicValue: 20,
        revenuePerTick: 80,
        armyGrowthPerTick: 2
      }
    },
    {
      id: "t3",
      ownerFactionId: "npc-1",
      stats: {
        strategicValue: 99,
        revenuePerTick: 999,
        armyGrowthPerTick: 9
      }
    }
  ],
  armies: [
    { factionId: "human-1", strength: 60 },
    { factionId: "human-1", strength: 25 },
    { factionId: "npc-1", strength: 100 }
  ]
};

test("player stats totals sum owned territory production and player armies", () => {
  const totals = playerStatsTotals(snapshot, "human-1");

  assert.deepEqual(totals, {
    revenuePerTick: 200,
    armyStrength: 85,
    armyGrowthPerTick: 6,
    territoryValue: 50,
    territoryCount: 2
  });
});

test("player stats markup renders revenue army and territory values", () => {
  const markup = playerStatsMarkup(snapshot, "human-1");

  assert.match(markup, /data-player-stats/);
  assert.match(markup, /data-player-revenue/);
  assert.match(markup, />200<\/strong>/);
  assert.match(markup, /data-player-army/);
  assert.match(markup, />85<\/strong>/);
  assert.match(markup, /data-player-army-growth/);
  assert.match(markup, />\+6<\/strong>/);
  assert.match(markup, /data-player-territory-value/);
  assert.match(markup, />50<\/strong>/);
  assert.match(markup, /2 territories/);
});
