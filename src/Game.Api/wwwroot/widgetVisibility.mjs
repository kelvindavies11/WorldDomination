export function isWidgetHidden(hiddenWidgets, widget) {
  return hiddenWidgets.has(widget);
}

export function widgetHiddenClass(hiddenWidgets, widget) {
  return isWidgetHidden(hiddenWidgets, widget) ? "is-hidden-by-menu" : "";
}

export function widgetToggleLabel(hiddenWidgets, widget, label) {
  return `${isWidgetHidden(hiddenWidgets, widget) ? "Show" : "Hide"} ${label}`;
}
