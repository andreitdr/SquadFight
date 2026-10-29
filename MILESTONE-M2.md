# Milestone M2 — "Playable" (steps 11–13)

Date: 2026-10-29 · Commit: `step-13` (f377eae)

## What works
The full game loop runs in the Editor with placeholder art:
**Home → Campaign → Fight Preview (enemies, possible rewards, party power, auto toggle) → Battle → Result (stars, animated rewards, shard progress, hero-unlockable highlight) → Heroes (unlock, star-up) → Equipment picker (stat deltas) → Party builder → Bag → Settings.**

- **UI framework**: code-built uGUI (no prefabs), theme asset, screen/popup stacks, Android back, safe areas, toasts, pooled lists, red-dot notifications, tutorial anchors (for step 18).
- **Battle presentation**: turn-by-turn playback of the deterministic simulator — HP/shield bars, floating damage/heal/crit numbers, status icons, wave and boss banners, ×1/×2/×3 speed, Auto/manual skills (tap a hero to queue its skill), Skip, Pause → Retreat. Presentation never changes outcomes (tested: presented battle == pure simulation, event for event).
- **Localization**: every visible string goes through a localizer; English + Romanian UI tables, language switch in Settings rebuilds all text.
- **Screenshots** of every screen are rendered by the PlayMode tests into `TestResults/screens/` (`tour_*.png`, `gallery_*.png`, `battle_mid.png`).

## Numbers
- Tests: **222/222 EditMode**, **12/12 PlayMode** (incl. a full boot→battle→result loop on a temporary save), **217/217** dotnet.
- Config validator: 0 errors / 0 warnings. Progression simulator: campaign completed in all 9 runs.

## How to try it
1. Open the project in Unity 6000.6.7f1, open `Assets/_Game/Scenes/Boot.unity`, press Play (portrait Game view, e.g. 1080×2340).
2. Continue → Fight! → watch the battle (Skip/speed in the top bar) → Result → Heroes → unlock Sister Maren (her shards come from the first fight).
3. Dev tools: **`** opens the console (`help`, `battle fight_1_8 42` replays a fight from a seed, `clear all`, `give gold 5000`…); F3 shows FPS/memory; `Assets/_Game/Scenes/Dev/UIGallery.unity` shows every widget.

## Known gaps
- **Not yet run on a real phone.** Device checks (60 fps battles/lists, touch, notch, IL2CPP save round trip) are batched for the build step (17) — I'll produce an Android build there.
- **Balance**: campaign still too easy for an optimal player (step 15).
- Audio is not wired yet (step 14 adds placeholder sounds and music); vibration is a no-op until step 14.
- Long texts are truncated rather than ellipsized (font quirk, step 18 polish). Content names/story are English in the Romanian table.

## Question for you
D5 (localization backend) — see the question in chat; I recommend keeping the current lightweight CSV localizer.
