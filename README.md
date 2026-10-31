# SquadFight — Offline Hero Collector

Offline mobile hero collector (Unity 6000.6.7f1, C#, portrait, Android + iOS). Pick a party of 5, fight through the campaign,
collect shards to unlock and star-up heroes, equip gear. No server, no accounts, no network permission.

Content: the dark-epic campaign "The Hundred" (8 acts, 40 chapters, 401 main fights, 79 optional side quests) and
100 heroes (20 per class), each with a unique active skill and passive. Most content rows are generated from the design
tables in `tools/content/` (see CONTENT_SCHEMA.md, "Generated content").

## Layout
| Path | What |
|------|------|
| `Assets/_Game/Core` | Pure C# rules: stats, combat simulator, drop roller, progression (no UnityEngine) |
| `Assets/_Game/Data` | ScriptableObject content definitions + `ConfigDatabase` (SO → Core models) |
| `Assets/_Game/Save` | Save models, envelope + checksum, atomic file storage, migrations (no UnityEngine) |
| `Assets/_Game/Services` | Player state services: roster, party, inventory, progress, rewards (no UnityEngine) |
| `Assets/_Game/App` | Unity glue: Boot composition root, scene loading, lifecycle, debug tools |
| `Assets/_Game/UI`, `BattleView` | Code-built uGUI screens and battle presentation |
| `Assets/_Game/Editor` | Editor tools: setup, validators, importers, simulators, build script |
| `Assets/_Game/Tests` | EditMode + PlayMode tests |
| `Assets/_Game/Content` | Game content assets (heroes, items, chapters, drop tables, config) |
| `DotnetTests/` | Fast pure-C# test harness (`dotnet test`) linking Core/Save/Services sources |
| `plan/` | Specs per step; `PROGRESS.md` / `DECISIONS.md` track state |

## Common commands
```bash
./build.sh setup            # apply Player Settings + regenerate scenes
./build.sh tests            # EditMode + PlayMode tests in batch mode
./build.sh validate         # config validator
./build.sh android-dev      # development APK
./build.sh android-release  # release AAB (needs signing env vars, see BUILD.md)
dotnet test DotnetTests/GameTests   # fast pure tests
```
Close the Unity Editor before running batch commands on the same project.

## Documents
`MILESTONE-M1/M2/M3.md` (status), `PROGRESS.md`, `DECISIONS.md`, `CONTENT_SCHEMA.md` (content + balance workflow), `BUILD.md`,
`QA_CHECKLIST.md`, `PERF_REPORT.md`, `STORE_LISTING.md`, `PRIVACY_POLICY.md`, `THIRD_PARTY_NOTICES.md`, `PLACEHOLDER_AUDIT.md`, `AUDIO_CREDITS.md`.

## Dev controls
- Debug overlay (FPS/memory): three-finger tap on device, F3 in the Editor (dev builds only).
- Debug console: four-finger tap / backquote — `help`, `battle <fight> [seed]`, `soak 500`, `give …`, `clear all`, `reset`.
- Balance in seconds: `dotnet run -c Release --project DotnetTests/BalanceCli -- progression`.
