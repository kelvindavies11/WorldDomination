export const defaultGameOptions = {
  enableAnimations: true,
  enableSounds: true
};

const optionStorageKeys = {
  enableAnimations: "dynamic-osm-enable-animations",
  enableSounds: "dynamic-osm-enable-sounds"
};

export function loadGameOptions(storage = window.localStorage) {
  return {
    enableAnimations: storage.getItem(optionStorageKeys.enableAnimations) !== "false",
    enableSounds: storage.getItem(optionStorageKeys.enableSounds) !== "false"
  };
}

export function setGameOption(storage = window.localStorage, key, enabled) {
  const storageKey = optionStorageKeys[key];
  if (!storageKey) {
    throw new Error(`Unknown game option: ${key}`);
  }

  storage.setItem(storageKey, enabled ? "true" : "false");
  return loadGameOptions(storage);
}

export function gameOptionsMarkup(options) {
  return `
    <div class="game-options" aria-label="Game options">
      ${gameOptionToggleMarkup("enableAnimations", "Enable animations", options.enableAnimations)}
      ${gameOptionToggleMarkup("enableSounds", "Enable sounds", options.enableSounds)}
    </div>
  `;
}

function gameOptionToggleMarkup(key, label, enabled) {
  return `
    <button
      type="button"
      class="game-option-toggle${enabled ? " is-enabled" : ""}"
      role="switch"
      aria-checked="${enabled ? "true" : "false"}"
      data-action="toggle-game-option"
      data-option-key="${key}">
      <span>${label}</span>
      <span class="game-option-switch" aria-hidden="true"></span>
    </button>
  `;
}
