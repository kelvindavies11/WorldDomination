const SELECTED_TERRITORY_WIDGET = "selected-territory";

const actions = [
  { action: "territory-menu-info", label: "Scout territory", icon: infoIcon(), className: "info", disabled: false },
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
        ${actions.map(action => actionSliceMarkup(action, menu)).join("")}
        <div class="territory-action-menu-center" aria-hidden="true"></div>
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

function actionSliceMarkup(action, menu) {
  const disabled = action.action === "territory-menu-expand"
    ? !menu.canExpand
    : action.action === "territory-menu-attack"
      ? !menu.canAttack
      : action.disabled;

  return `
    <button type="button" class="territory-action-slice territory-action-slice-${action.className}" data-action="${action.action}" role="menuitem" aria-label="${action.label}" title="${action.label}" ${disabled ? "disabled aria-disabled=\"true\"" : ""}>
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
      ${infoSliceMarkup("army", info.armyStrength)}
    </aside>
  `;
}

function infoSliceMarkup(kind, value) {
  return `
    <div class="territory-info-slice territory-info-slice-${kind}">
      <span class="territory-info-content">
        ${infoMetricIcon(kind)}
        <b>${Number.isFinite(value) ? value : "-"}</b>
      </span>
    </div>
  `;
}

function infoMetricIcon(kind) {
  const paths = {
    // Coin with $ stem — economy/income
    economy: `<circle cx="12" cy="12" r="8.5"/><path d="M12 7.5v9M9.5 10c0-1.4 1.1-2.5 2.5-2.5s2.5 1.1 2.5 2.5-1.1 2.5-2.5 2.5-2.5 1.1-2.5 2.5 1.1 2.5 2.5 2.5 2.5-1.1 2.5-2.5"/>`,
    // Shield with tick — defense
    defense: `<path d="M12 2L3 6.5v5.7c0 5.1 3.8 8.9 9 10.8 5.2-1.9 9-5.7 9-10.8V6.5z"/><path d="M9 12l2 2 4-4"/>`,
    // Lightning bolt — speed/mobility
    mobility: `<path d="M13 2H6L3 12h7l-2 10 13-14h-8z"/>`,
    // 5-point star — strategic value
    value: `<polygon points="12,2 15.5,9 23,9.5 17,15 19,22 12,18 5,22 7,15 1,9.5 8.5,9"/>`,
    // Upright sword (blade + crossguard + pommel) — army strength
    army: `<path d="M12 2v16M8 10h8"/><circle cx="12" cy="20.5" r="1.8" fill="currentColor" stroke="none"/>`,
  };

  return iconSvg(paths[kind] ?? paths.value, "territory-info-icon", kind);
}

function infoIcon() {
  return iconSvg(`
    <circle cx="11" cy="11" r="7"/>
    <line x1="16.5" y1="16.5" x2="21" y2="21"/>
    <line x1="11" y1="9" x2="11" y2="14"/>
    <circle cx="11" cy="7" r="0.8" fill="currentColor" stroke="none"/>
  `);
}

function expandIcon() {
  return iconSvg(`
    <polyline points="15 3 21 3 21 9"/>
    <polyline points="9 21 3 21 3 15"/>
    <line x1="21" y1="3" x2="14" y2="10"/>
    <line x1="3" y1="21" x2="10" y2="14"/>
  `);
}

function attackIcon() {
  return iconSvg(`
    <line x1="6" y1="18" x2="18" y2="6"/>
    <polyline points="8 6 18 6 18 16"/>
    <line x1="8" y1="12" x2="14" y2="12"/>
  `);
}

function buildIcon() {
  return iconSvg(`
    <path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3-3a1 1 0 0 0 0-1.4l-1.6-1.6a1 1 0 0 0-1.4 0z"/>
    <path d="M5 19l7-7"/>
    <path d="M15 5l-11 11 3 3L18 8z"/>
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
