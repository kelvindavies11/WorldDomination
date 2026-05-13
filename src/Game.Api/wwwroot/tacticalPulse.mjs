const minDurationMs = 520;
const maxDurationMs = 1800;

export function tacticalPulseTiming(actionType, strength) {
  const normalizedStrength = Math.max(1, Number.isFinite(Number(strength)) ? Number(strength) : 1);
  const strengthRatio = Math.min(1, Math.log2(normalizedStrength + 1) / Math.log2(41));
  const durationMs = Math.round(minDurationMs + (maxDurationMs - minDurationMs) * strengthRatio);
  const isReinforce = actionType === "reinforce";
  const intensityBase = actionType === "attack" ? 1 : actionType === "claim" ? 0.78 : 0.58;

  return {
    durationMs,
    minDurationMs,
    maxDurationMs,
    intensity: Number((intensityBase + strengthRatio * (isReinforce ? 0.22 : 0.42)).toFixed(2)),
    scale: normalizedStrength < 8 ? "small" : normalizedStrength < 24 ? "medium" : "large",
    soundCue: actionType === "reinforce" ? "reinforce" : actionType === "claim" ? "claim" : "attack"
  };
}

export function createTacticalPulseEventState(event, options) {
  const strength = Math.max(1, Number(event?.strength ?? 1));
  const actionType = event?.actionType ?? "attack";
  const timing = tacticalPulseTiming(actionType, strength);

  return {
    gameId: event?.gameId ?? null,
    sourceTerritoryId: event?.sourceTerritoryId ?? null,
    targetTerritoryId: event?.targetTerritoryId ?? null,
    actionType,
    strength,
    ownerFactionId: event?.ownerFactionId ?? null,
    occurredAtUtc: event?.occurredAtUtc ?? null,
    animationsEnabled: options?.enableAnimations !== false,
    soundsEnabled: options?.enableSounds !== false,
    ...timing
  };
}
