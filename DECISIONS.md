# Decisions log

Format: **[step] Decision** — why. `DEFAULTED – owner may override` marks choices made without asking that are cheap to change.

## Step 01
- **[01] Keep Unity 6000.6.7f1 and the URP 2D template as-is.** Plan A9: never upgrade; template is already 2D.
- **[01] Owner's existing `.gitignore`/`.gitattributes` kept** (GitHub Unity templates, already LFS-ready); appended `.DS_Store`, `BalanceReports/`, `TestResults/`, dotnet `bin/obj`. `.DS_Store` untracked.
- **[01] Extra assembly `Game.App`** (Unity glue + composition root: `GameBootstrap`, `SceneLoader`, save lifecycle hooks, debug overlay/console). `Game.Core`, `Game.Save` and `Game.Services` are all `noEngineReferences: true`. Why: services stay pure C# and testable outside Unity; overview said "services live in a Unity assembly only for glue" – the glue is now its own assembly.
- **[01] Config flows SO → Core POCOs.** `Game.Data` ScriptableObjects are converted by `ConfigDatabase` into a pure `GameDatabase` (Core) exposed via `IConfigProvider`. Services depend on Core config models, not on ScriptableObjects. Why: keeps Services engine-free and lets tests build configs in code.
- **[01] Logger interface named `IGameLogger`** (not `ILogger`) to avoid clashing with `UnityEngine.ILogger` in files that import both namespaces.
- **[01] RNG = own `SeededRandom` (xoshiro128\*\* seeded via SplitMix64)** instead of wrapping `System.Random`. Why: `System.Random` output differs between runtimes (Mono/IL2CPP/.NET), we need identical results across platforms, editor, tests and saved RNG state.
- **[01] Editor namespace is `Game.EditorTools`** (assembly still `Game.Editor`) to avoid shadowing `UnityEditor.Editor`.
- **[01] Nullable reference types not enabled** in Unity assemblies (would flood the console with warnings in a mixed codebase); enabled only in the dotnet harness projects is also skipped for consistency. DEFAULTED – owner may override.
- **[01] Dual test harness:** Unity Test Framework (source of truth, run in batch mode) + `DotnetTests/` (netstandard2.1 library linking Core/Save/Services sources + NUnit test project) for fast iteration and coverage numbers.
- **[01] Package id `com.andreitdr.squadfight`, company `andreitdr`, product `SquadFight`.** DEFAULTED – owner may override (game name not final).
- **[01] Android: min API 26 (template value, ≥ Unity 6 minimum), target API 36, IL2CPP, ARM64 only. iOS: IL2CPP, min iOS 15.0 (template).**
- **[01] UI is built in code** (uGUI hierarchy created by a `UIFactory` driven by a `Theme` asset) rather than hand-authored prefabs. Why: autonomous development, reviewable diffs, no fragile prefab YAML; art stays swappable through Theme/config sprite refs.
