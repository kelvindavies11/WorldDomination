export const defaultTerritoryCount = 100;

export function createGameFormMarkup({ creating, maps = [] }) {
  const mapOptions = maps.length > 0
    ? maps.map(m => `<option value="${m.id}">${m.name}</option>`).join("")
    : `<option value="">Loading maps...</option>`;
  return `
    <form class="form" data-action="create-game">
      <div class="field">
        <label for="name">Game name</label>
        <input id="name" name="name" value="New Skirmish" required>
      </div>
      <div class="field">
        <label for="mapArea">Map area</label>
        <select id="mapArea" name="mapArea" ${maps.length === 0 ? "disabled" : ""}>
          ${mapOptions}
        </select>
      </div>
      <div class="field">
        <label for="maxHumanPlayers">Max human players</label>
        <input id="maxHumanPlayers" name="maxHumanPlayers" value="2" inputmode="numeric" required>
      </div>
      <div class="field">
        <label for="npcFactions">NPC factions</label>
        <input id="npcFactions" name="npcFactions" value="6" inputmode="numeric" required>
      </div>
      <div class="field">
        <label for="winningControlPercentage">Winning control percentage — <strong id="win-pct-display">80%</strong></label>
        <p class="hint">Victory target for the percentage of map territory a faction must control to win.</p>
        <input id="winningControlPercentage" name="winningControlPercentage" type="range" min="10" max="100" step="5" value="80"
          oninput="document.getElementById('win-pct-display').textContent = this.value + '%'">
      </div>
      <div class="actions">
        <button type="submit">${creating ? "Creating..." : "Create Game"}</button>
        <a class="button secondary" href="/games" data-link>Cancel</a>
      </div>
    </form>
  `;
}
