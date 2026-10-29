# Step 04 — Player State & Core Services

**Depends on:** 02, 03. **Size:** M

## Goal
A single authoritative in-memory player state, mutated only through service methods that validate rules, raise events, and mark the save dirty. This is the "backend" of an offline game.

## Services (Game.Services)
| Service | Responsibility |
|---------|---------------|
| `PlayerState` | Holds loaded `SaveData` (domain view); no logic |
| `CurrencyService` | Add/spend/get currencies; never negative; events |
| `RosterService` | Query heroes (owned/locked), shard counts, star levels (logic arrives in step 05) |
| `PartyService` | 5-slot party, add/remove/swap, no duplicates, only unlocked heroes (step 05 finishes) |
| `InventoryService` | Equipment instances: add, remove, query by slot/rarity, equipped status (logic in step 06) |
| `ProgressService` | Cleared fights/chapters, stars, unlock checks (step 09) |
| `RewardService` | Applies a `RewardBundle` atomically (step 10) |
| `EventBus` | Typed pub/sub (`HeroUnlocked`, `ShardsChanged`, `PartyChanged`, `ItemEquipped`, `FightCompleted`, …) |

## Design rules
- Every mutating method returns a `Result` (success/failure + reason code), never throws for rule violations.
- Mutations are **transactional**: validate first, then apply, then raise events, then `SaveManager.MarkDirty()`.
- Services expose read-only views (`IReadOnlyList`, immutable snapshots) to UI.
- No Unity types in service interfaces (keep UI/Unity out); services live in a Unity assembly only for glue.

## Tasks
1. `EventBus` + event definitions.
2. `Result`/`ErrorCode` types.
3. Implement `CurrencyService`, `RosterService` (read parts), `PartyService`, `InventoryService` skeletons with full unit tests of invariants.
4. `GameSession` composition: builds all services from `SaveData` + `ConfigDatabase`; registered in `ServiceLocator` in Boot.
5. New-game initializer: starting heroes (config: `GameConfig.startingHeroes`), starting currencies, empty party auto-filled.
6. Debug console (dev builds): commands `give gold 500`, `give shards hero_x 10`, `unlock hero_x`, `clear fight x`, `reset`.

## Acceptance criteria
- State can be built from a `SaveData`, mutated, and exported back to an identical-format `SaveData`.
- Invariants hold under tests: no negative currency, party has no duplicates, party size ≤ 5.
- Debug console works in Editor and dev builds, absent from release.

## Prompt for Claude
> Read `00-overview.md` and `04-player-state-and-services.md`. Implement EventBus, Result types, CurrencyService, PartyService, InventoryService and read-side of RosterService in Game.Services, plus `GameSession` composition and a new-game initializer driven by `GameConfig`. Add a dev-only debug console. Write unit tests for all invariants. Do not implement shard/unlock logic yet (step 05) or equipment stat logic (step 06).

## Decisions made
- Services are pure C# (`Game.Services`, noEngineReferences); Unity glue in `Game.App`
- `ServiceContext` transactions: snapshot + deferred events + single dirty mark; rollback restores the snapshot
- Party assign onto an occupied slot / existing member swaps instead of failing; party stored as fixed-length list with nulls
- `SaveReconciler` repairs saves against current content on every load (never deletes items or shards)
- Debug console = IMGUI overlay (backquote / 4-finger tap), compiled only in Editor/development builds
