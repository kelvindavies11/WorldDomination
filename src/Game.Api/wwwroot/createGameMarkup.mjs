export const defaultTerritoryCount = 100;

export function createGameFormMarkup({ creating }) {
  return `
    <form class="form" data-action="create-game">
      <div class="field">
        <label for="name">Game name</label>
        <input id="name" name="name" value="Cardiff Skirmish" required>
      </div>
      <div class="field">
        <label for="mapArea">Map area</label>
        <select id="mapArea" name="mapArea">
          <option>Cardiff</option>
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
      <div class="actions">
        <button type="submit">${creating ? "Creating..." : "Create Game"}</button>
        <a class="button secondary" href="/games" data-link>Cancel</a>
      </div>
    </form>
  `;
}
