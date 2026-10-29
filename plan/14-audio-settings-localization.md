# Step 14 — Audio, Settings & Localization

**Depends on:** 11. **Size:** S

## Goal
Sound, user settings persistence, and localization-ready text so adding languages later needs no code changes.

## Audio
- `IAudioService`: `PlayMusic(trackId, fade)`, `PlaySfx(sfxId)`, `SetMusicVolume`, `SetSfxVolume`, mute.
- AudioMixer groups (Master/Music/SFX) with exposed volume params; SFX pool of AudioSources.
- `AudioLibrary` ScriptableObject mapping IDs → clips (config, not hard-coded).
- Handle app pause/resume and phone call/headphone interruptions.
- Hooks: UI click, reward reveal, unlock fanfare, battle hit/crit/skill/death, victory/defeat jingle, music per scene/chapter.

## Settings
- `SettingsService` backed by `SaveData.settings`: music/sfx volume, language, vibration on/off, screen shake, battle speed default, auto-skill default, reduce-motion.
- `IHaptics` implementation (Android/iOS) honoring vibration setting.

## Localization
- Use Unity Localization package, **or** a lightweight own `ILocalizer` with CSV/JSON tables (D5, default: Unity Localization for plurals + fallback).
- All strings keyed (`hero_knight_name`); no literals in UI code.
- Language detection on first run (system language → supported or English), changeable in Settings.
- Font fallback assets for CJK/Cyrillic if those languages are planned (TMP font atlas considerations for size).
- Validator: every key used in config exists in the default language; missing translation report.
- Dynamic text formatting with smart strings / `string.Format` and pluralization for shard counts.

## Tasks
1. Audio service + mixer + library + pooling; tests for volume mapping (dB conversion).
2. `SettingsService` + UI binding (step 12 Settings screen) + save integration.
3. Localization setup, table generator from config assets (auto-create key stubs for new heroes/items).
4. Editor tool: "Find missing localization keys".
5. Placeholder CC0 audio set listed in `AUDIO_CREDITS.md`.

## Acceptance criteria
- Changing language at runtime updates all visible text.
- Volume/vibration settings persist across restarts.
- Audio stops/resumes cleanly on app background.

## Prompt for Claude
> Read `00-overview.md` and `14-audio-settings-localization.md`. Implement `IAudioService` with mixer and library, `SettingsService` with save integration and haptics, and localization using Unity Localization (English default, Romanian placeholder table). Add the editor tool that reports missing keys and generates key stubs from config assets. Include tests for the pure parts.
