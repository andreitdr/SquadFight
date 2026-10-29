# Step 03 — Save System (offline persistence)

**Depends on:** 02. **Size:** M

## Goal
Reliable local saving with no data loss on crash, kill, or app update. This is the most important safety net for an offline game, since there's no server backup.

## Save model (Game.Save)
`SaveData` (plain C# classes, serializable):
- `version` (int), `createdUtc`, `lastSavedUtc`
- `currencies` : Dictionary<string,long> (e.g., gold)
- `heroes` : list of `HeroSave { heroId, unlocked, stars, shards }` (shards tracked for locked heroes too)
- `party` : string[5] of hero IDs (empty slots allowed)
- `equipment` : list of `EquipmentSave { instanceId (GUID), templateId, rarity, rolledStats?, equippedByHeroId? }`
- `progress` : chapters/fights cleared, stars/best results, first-clear rewards claimed
- `settings` : volume, language, vibration, battle speed, auto-skill toggle
- `rngState` (optional, if seeded runs are wanted)

## Storage rules
- Serialize with `System.Text.Json` or `Newtonsoft.Json` (Unity package `com.unity.nuget.newtonsoft-json`). Choose one and record it.
- Location: `Application.persistentDataPath/save.json`.
- **Atomic write:** write `save.tmp` → flush → replace `save.json`; keep `save.bak` (previous good copy).
- On load: try `save.json` → on parse/checksum failure try `save.bak` → else new game (and keep the broken file as `save.corrupt.<timestamp>`).
- Checksum (SHA-256 of payload) stored in a wrapper envelope to detect corruption; **not** security (offline game, cheating is the player's choice), optionally light XOR/AES obfuscation later.
- Auto-save triggers: after any state-changing service call (debounced ~1s), `OnApplicationPause(true)`, `OnApplicationFocus(false)`, `OnApplicationQuit`.

## Migrations
- `ISaveMigration { int From; int To; void Apply(JObject) }` chain; run on load until current version.
- Test fixtures: keep sample saves for each historical version in `Tests/Fixtures`.

## Tasks
1. Implement `SaveData` classes and `SaveEnvelope`.
2. Implement `FileSaveStorage : ISaveStorage` (atomic write, backup, corrupt handling) and `InMemorySaveStorage` for tests.
3. Implement `SaveManager` (Load, Save, MarkDirty, debounce, lifecycle hooks via a tiny MonoBehaviour).
4. Implement migration runner + one sample migration (v1→v2) as a pattern.
5. Debug menu items: "Open save folder", "Delete save", "Corrupt save (test)".
6. Tests: round-trip, corrupt main file falls back to backup, both corrupt → new game, migration chain, atomic write interrupted (simulate partial tmp file).

## Acceptance criteria
- Killing the app mid-play loses at most ~1 second of progress.
- Corrupt `save.json` never crashes; backup restores.
- Old fixture saves load through migrations in tests.

## Prompt for Claude
> Read `00-overview.md` and `03-save-system.md`. Implement the save system in Game.Save exactly as specified: models, envelope with checksum, atomic file storage with backup/corruption handling, migration framework with a sample migration, `SaveManager` with debounced auto-save and lifecycle hooks. Provide EditMode tests for every failure scenario listed, using `InMemorySaveStorage`/a temp directory.

## Decisions made
- JSON library: Newtonsoft (`com.unity.nuget.newtonsoft-json` 3.2.2), camelCase, enums as strings, `DateParseHandling.None`
- Obfuscation: none (SHA-256 over counter+payload for corruption detection only)
- Envelope has a monotonic save counter; load picks the newest intact of main/tmp, then bak
- Intact-but-unreadable / future-version / IO-error saves are never quarantined or overwritten: session goes read-only
- Second review pass done (subagent): 7 findings fixed with regression tests
