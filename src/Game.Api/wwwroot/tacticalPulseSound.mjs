let audioContext = null;

export function playTacticalPulseSound(eventState) {
  if (!eventState?.soundsEnabled || typeof window === "undefined") {
    return;
  }

  const AudioContextType = window.AudioContext ?? window.webkitAudioContext;
  if (!AudioContextType) {
    return;
  }

  audioContext ??= new AudioContextType();
  const now = audioContext.currentTime;
  const gain = audioContext.createGain();
  const oscillator = audioContext.createOscillator();
  const cue = eventState.soundCue ?? "attack";
  const baseFrequency = cue === "reinforce" ? 330 : cue === "claim" ? 420 : 160;
  const volume = Math.min(0.18, 0.055 + eventState.intensity * 0.04);
  const durationSeconds = Math.min(0.42, Math.max(0.14, eventState.durationMs / 5000));

  oscillator.type = cue === "attack" ? "sawtooth" : "triangle";
  oscillator.frequency.setValueAtTime(baseFrequency, now);
  oscillator.frequency.exponentialRampToValueAtTime(baseFrequency * (cue === "attack" ? 0.65 : 1.45), now + durationSeconds);

  gain.gain.setValueAtTime(0.0001, now);
  gain.gain.exponentialRampToValueAtTime(volume, now + 0.025);
  gain.gain.exponentialRampToValueAtTime(0.0001, now + durationSeconds);

  oscillator.connect(gain);
  gain.connect(audioContext.destination);
  oscillator.start(now);
  oscillator.stop(now + durationSeconds + 0.02);
}
