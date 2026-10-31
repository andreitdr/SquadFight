# Offline Hero Collector — Master Plan

Read this file first. Every step file (`01-…` to `18-…`) assumes you have read it.

## 1. Vision

An **offline** mobile game (Android + iOS, Unity / C#). No server, no accounts, no internet required at any point.

Core loop:
1. Pick a party of **5 heroes**.
2. Fight through the story, **chapter by chapter, fight by fight**.
3. Each fight gives **random rewards** (shards, equipment, currency) from drop tables that are **configurable before build**.
4. Collect **shards** to **unlock new heroes** (number of shards depends on hero rarity).
5. Extra shards of an already-unlocked hero **upgrade its base stats**.
6. Equip heroes with **weapon, armor pieces, rings**; equipment has **rarity** that scales stats.
7. Stronger party → next fight.

## 2. Pillars

- **Fully offline & local save** — robust against crashes, app kills, and updates.
- **Data-driven** — heroes, items, chapters, fights, drops, curves are all config assets. Designers (you) change content without touching code.
- **Deterministic core** — game logic (combat, drops, stats) is pure C#, testable without Unity, optionally seedable.
- **Mobile-first** — portrait UI, touch, safe areas, low memory, small build, battery friendly.

## 3. Assumptions (change here if wrong — plans reference this table)

| # | Topic | Assumption | Impact if changed |
|---|-------|-----------|-------------------|
| A1 | Combat style | **Turn-based auto-battle**, speed-ordered turns; each hero has 1 basic attack + 1 active skill (player can tap to trigger skill, or Auto) **+ 1 passive trait (owner request 2026-10-31: unique active + passive per hero; 14 new effect types, passive triggers)**. | Steps 08, 13 |
| A2 | Enemy count | 1–5 enemies per fight, optional boss. Enemies have a **rank** (Minion < Soldier < Elite < Champion < Boss): higher ranks have more HP and more skills; bosses also have special attacks charged at HP thresholds (owner request 2026-10-31). | 08, 09 |
| A3 | Equipment slots | Weapon, Helmet, Chest, Gloves, Boots, Ring ×2 (7 slots, enum-driven so slot list is configurable). | 06, 07 |
| A4 | Rarities | Common, Uncommon, Rare, Epic, Legendary (one shared enum for heroes and items). | 02, 05, 06 |
| A5 | Equipment ownership | Items are unique instances; each equipped on one hero at a time; can be moved freely. | 06 |
| A6 | Hero upgrade | "Shard star-ups": extra shards raise a **star level** (1→N), each star multiplies base stats via a configurable curve. No hero XP/levels in v1. A **player level** (account XP from defeated enemies by rank, bosses the most; level-up rewards) was added on owner request 2026-10-31; it does not change hero stats. | 05, 07 |
| A7 | Stamina | **None** (offline single-player; fights are free to replay). | 09 |
| A8 | Monetization | **None in v1** (no ads/IAP). Optional later. | 18 |
| A9 | Engine | The **owner's existing empty Unity project** (version as found in `ProjectVersion.txt`; do not upgrade), uGUI + TextMeshPro, new Input System. Render pipeline = whatever the template provides; game is 2D. | 01, 11 |
| A10 | Art | Placeholder art first; final art is swappable via config references. | 13, 15 |
| A11 | Orientation | Portrait. | 11 |
| A12 | Languages | English first, localization-ready from day 1. **Owner decision (M2): lightweight CSV localizer (`TableLocalizer`) instead of the Unity Localization package.** | 14 |
| A13 | Campaign & roster (owner request 2026-10-31) | **Dark-epic story "The Hundred": 8 acts × 5 chapters = 40 chapters, 9–11 main fights each (seeded random), 1–3 side quests per chapter. 100 heroes, 20 per class, rarity random per hero but strictly fewer at each higher rarity.** Shards spread over many fights and limited to the current acts' heroes; ~25% of main fights drop items only. | 02, 09, 10, 15 |
| A14 | Side quests | Optional zones inside a chapter for lore and better items (unique relics); they open through an unlock condition and never count toward chapter completion, stars or the unlock chain. | 09, 10, 12 |

## 4. Architecture

Layered, with Unity assembly definitions enforcing the boundaries:

```
Game.Core        pure C# (noEngineReferences = true)
                 enums, stat math, combat sim, drop roller, shard/unlock rules, RNG interface
Game.Data        ScriptableObject definitions + validators + config database (references Core)
Game.Save        save models, serializer, migration, storage (references Core)
Game.Services    PlayerState, Inventory, Roster, Party, Progress, Reward services (Core, Data, Save)
Game.UI          screens, view models, navigation (Services, Data)
Game.BattleView  combat presentation, animation, VFX (Core, Data)
Game.Editor      editor tools: validators, CSV import, balance simulator, build hooks
Game.Tests       EditMode + PlayMode tests
```

Rules:
- `Game.Core` must never reference `UnityEngine`.
- UI never mutates state directly; it calls service methods and listens to events.
- All identifiers are stable **string IDs** (`hero_knight_01`), never asset names or indices.
- No static singletons for game state; use a small `ServiceLocator`/composition root created in the Boot scene.
- Randomness always goes through `IRandom` (so tests and replays can seed it).

## 5. Folder structure

Project root = the existing Unity project folder (contains `Assets/`, `Packages/`, `ProjectSettings/`). Planning/automation files (`AUTOPILOT.md`, `CLAUDE.md`, `PROGRESS.md`, `DECISIONS.md`, `plan/`) live at the project root, **outside `Assets/`** so Unity ignores them.

```
Assets/
  _Game/
    Core/            Data/            Save/           Services/
    UI/              BattleView/      Editor/         Tests/
    Content/
      Heroes/  Enemies/  Equipment/  DropTables/  Chapters/  Config/
    Art/  Audio/  Prefabs/  Scenes/ (Boot, Main, Battle)
```

## 6. Step index

| Step | File | Depends on | Size |
|------|------|-----------|------|
| 01 | `01-project-setup.md` | — | S |
| 02 | `02-data-model-and-config.md` | 01 | M |
| 03 | `03-save-system.md` | 02 | M |
| 04 | `04-player-state-and-services.md` | 02, 03 | M |
| 05 | `05-heroes-shards-and-unlocking.md` | 04 | M |
| 06 | `06-equipment-system.md` | 04 | M |
| 07 | `07-stats-and-formulas.md` | 05, 06 | S |
| 08 | `08-combat-core.md` | 07 | L |
| 09 | `09-campaign-progression.md` | 08 | M |
| 10 | `10-rewards-and-drop-tables.md` | 05, 06, 09 | M |
| 11 | `11-ui-foundation.md` | 04 | M |
| 12 | `12-ui-screens.md` | 05–10, 11 | L |
| 13 | `13-battle-presentation.md` | 08, 11 | L |
| 14 | `14-audio-settings-localization.md` | 11 | S |
| 15 | `15-balance-and-content-tools.md` | 07–10 | M |
| 16 | `16-testing-and-qa.md` | ongoing | M |
| 17 | `17-mobile-optimization-and-builds.md` | 12, 13 | M |
| 18 | `18-polish-tutorial-release.md` | all | M |

Suggested order: 01 → 02 → 03 → 04 → 05 → 06 → 07 → 08 → 09 → 10 (**playable logic, no UI**) → 11 → 12 → 13 (**playable game**) → 14 → 15 → 16/17 → 18.
Steps 11 can start in parallel with 05–10 if desired.

**Milestones**
- **M1 "Logic complete"** after step 10: everything works via tests and a debug console.
- **M2 "Playable"** after step 13: full loop on device with placeholder art.
- **M3 "Release candidate"** after step 18.

## 7. How the work is run

The build is **autonomous**: Claude follows `AUTOPILOT.md` (one level above this folder), works through steps 01-18 in order, tracks state in `PROGRESS.md`, logs choices in `DECISIONS.md`, commits `step-NN: <title>` after each step, and asks the owner only when genuinely unsure or blocked. The "Prompt for Claude" blocks in the step files are task specs for Claude itself, not prompts for the owner to paste. To change a decision, update the Assumptions table above and tell Claude; it will rework affected steps.

**Definition of done (every step):** compiles with zero warnings in touched code, tests pass, acceptance criteria met, no `UnityEngine` in Core, no hard-coded content values (all in config), step file's "Decisions made" section filled in.

## 8. Main risks

| Risk | Mitigation |
|------|-----------|
| Save corruption / lost progress | Atomic writes, backup file, versioned migrations (step 03) |
| Balance hell | Simulation tool + CSV workflow (step 15) |
| Scope creep (hero levels, crafting, PvP) | v1 scope fixed by this file; extras go to "Future ideas" below |
| Unity-only code blocking tests | Pure-C# Core layer |
| Android device fragmentation | Test matrix + optimization pass (step 17) |

## 9. Future ideas (explicitly out of v1)

Hero XP/levels, equipment upgrading/crafting, hero skills trees, PvP/arena (needs a server), daily rewards, achievements, ads/IAP, cloud save, endless/tower mode.
