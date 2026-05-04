const SELECTED_TERRITORY_WIDGET = "selected-territory";

const actions = [
  { action: "territory-menu-info", label: "Show territory info", icon: infoIcon(), className: "info", disabled: false },
  { action: "territory-menu-expand", label: "Expand into area", icon: expandIcon(), className: "expand", disabled: true },
  { action: "territory-menu-attack", label: "Attack", icon: attackIcon(), className: "attack", disabled: true },
  { action: "territory-menu-build", label: "Build", icon: buildIcon(), className: "build", disabled: true }
];

export function territoryActionMenuMarkup(menu) {
  if (!menu?.territoryId) {
    return "";
  }

  const x = Number.isFinite(menu.x) ? menu.x : 0;
  const y = Number.isFinite(menu.y) ? menu.y : 0;

  return `
    <div class="territory-action-menu is-offset-right" data-territory-action-menu data-territory-id="${escapeHtml(menu.territoryId)}" style="--menu-x: ${x}px; --menu-y: ${y}px;" role="menu" aria-label="Territory actions">
      ${menu.showInfo ? territoryInfoRingMarkup(menu.info, menu.infoHiding) : ""}
      <div class="territory-action-wheel">
        ${actions.map(actionSliceMarkup).join("")}
        <div class="territory-action-menu-center" aria-hidden="true">
          ${menu.showInfo ? centerArmyMarkup(menu.info?.armyStrength) : ""}
        </div>
      </div>
    </div>
  `;
}

export function applyTerritoryInfoAction(state) {
  if (state.territoryActionMenu?.territoryId) {
    state.selectedTerritoryId = state.territoryActionMenu.territoryId;
    if (state.territoryActionMenu.showInfo && !state.territoryActionMenu.infoHiding) {
      state.territoryActionMenu.infoHiding = true;
      return;
    }

    state.territoryActionMenu.showInfo = true;
    state.territoryActionMenu.infoHiding = false;
  }
}

export function hideTerritoryActionMenu(state) {
  state.territoryActionMenu = null;
}

function actionSliceMarkup(action) {
  return `
    <button type="button" class="territory-action-slice territory-action-slice-${action.className}" data-action="${action.action}" role="menuitem" aria-label="${action.label}" title="${action.label}" ${action.disabled ? "disabled aria-disabled=\"true\"" : ""}>
      ${action.icon}
    </button>
  `;
}

function territoryInfoRingMarkup(info, isHiding = false) {
  if (!info) {
    return "";
  }

  return `
    <aside class="territory-action-info-ring${isHiding ? " is-hiding" : ""}" aria-label="Selected territory info">
      ${infoSliceMarkup("economy", info.economy)}
      ${infoSliceMarkup("defense", info.defense)}
      ${infoSliceMarkup("mobility", info.mobility)}
      ${infoSliceMarkup("value", info.strategicValue)}
    </aside>
  `;
}

function infoSliceMarkup(kind, value) {
  return `
    <div class="territory-info-slice territory-info-slice-${kind}">
      ${infoMetricIcon(kind)}
      <b>${Number.isFinite(value) ? value : "-"}</b>
    </div>
  `;
}

function centerArmyMarkup(value) {
  return `
    <span class="territory-info-army">
      ${infoMetricIcon("army")}
      <b>${Number.isFinite(value) ? value : 0}</b>
    </span>
  `;
}

function infoMetricIcon(kind) {
  const paths = {
    economy: `<path d="M7 12h10"></path><path d="M8 8h8"></path><path d="M9 16h6"></path>`,
    defense: `<path d="M12 4l7 3v5c0 4-3 7-7 8-4-1-7-4-7-8V7l7-3z"></path>`,
    mobility: `<path d="M5 12h12"></path><path d="M13 7l5 5-5 5"></path>`,
    value: `<path d="M12 5l2 5 5 .5-4 3.5 1.2 5-4.2-2.7L7.8 19 9 14l-4-3.5 5-.5 2-5z"></path>`,
    army: `<path d="M8 19V9l4-4 4 4v10"></path><path d="M6 19h12"></path>`
  };

  return iconSvg(paths[kind] ?? paths.value, "territory-info-icon", kind);
}

function infoIcon() {
  return iconSvg(`
    <circle cx="12" cy="12" r="9"></circle>
    <line x1="12" y1="11" x2="12" y2="16"></line>
    <circle cx="12" cy="8" r="0.7"></circle>
  `);
}

function expandIcon() {
  return iconSvg(`
    <path d="M12 5v14"></path>
    <path d="M5 12h14"></path>
    <path d="M16 8l3-3"></path>
    <path d="M8 16l-3 3"></path>
  `);
}

function attackIcon() {
  return iconSvg(`
    <path d="M5 19l14-14"></path>
    <path d="M14 5h5v5"></path>
    <path d="M7 7l10 10"></path>
  `);
}

function buildIcon() {
  return iconSvg(`
    <path d="M6 19h12"></path>
    <path d="M8 19V9l4-4 4 4v10"></path>
    <path d="M10 19v-5h4v5"></path>
  `);
}

function iconSvg(paths, className = "territory-action-icon", iconKind = "") {
  return `
    <svg class="${className}" ${iconKind ? `data-info-icon="${iconKind}"` : ""} viewBox="0 0 24 24" aria-hidden="true" focusable="false">
      ${paths}
    </svg>
  `;
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}
