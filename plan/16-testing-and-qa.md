# Step 16 — Testing & QA (runs continuously, finalized before release)

**Depends on:** ongoing from step 01. **Size:** M

## Goal
Catch regressions automatically and verify the game on real devices.

## Automated tests
| Layer | Type | What |
|-------|------|------|
| Core | EditMode (NUnit) | Formulas, combat determinism, drop roller statistics, progression rules |
| Services | EditMode | Invariants, transactional behavior, event emission |
| Save | EditMode | Round-trip, corruption, migrations with fixtures |
| Config | EditMode | Validator on real `GameConfig` (build gate) |
| UI | PlayMode | Navigation flows, key screens instantiate without errors |
| Integration | PlayMode | Headless full loop: new game → fights → rewards → unlock → equip → save → reload → state identical |
| Perf | PlayMode | Battle 5v5 frame time, memory after 50 fights (leak check) |

- Coverage target: ≥ 90% for Game.Core, ≥ 80% for Services (Unity Code Coverage package).
- CI (GitHub Actions + GameCI or local script): run EditMode+PlayMode tests and config validation on every push; build Android on tags.

## Manual QA
- **Test matrix:** 1 low-end Android (3GB RAM), 1 mid Android, 1 recent Android, 1 iPhone, 1 tablet.
- **Checklists** (`QA_CHECKLIST.md`): fresh install, upgrade install (save migration), airplane mode (must work fully), kill app during fight/reward screen, low storage, background/resume 30 min, rotation lock, notch/safe area, interruptions (call, notification), language switching, audio focus.
- **Soak test:** auto-play script running 500 fights; check memory/FPS/log errors.
- **Cheat/exploit review:** rapid tapping reward claim (double-claim prevention), kill app right after reward to duplicate rewards (rewards must be applied + saved atomically before showing popup).
- Bug template and severity rubric in `QA_CHECKLIST.md`.

## Tasks
1. Set up test assemblies and shared test helpers (builders for configs, seeded RNG, in-memory save).
2. Add integration "headless playthrough" test.
3. Add CI configuration files.
4. Write `QA_CHECKLIST.md` and soak-test auto-player.
5. Crash/log capture: file logger of last N lines included in a "copy debug info" button in Settings (offline-friendly bug reports).

## Acceptance criteria
- CI green; coverage targets met.
- Headless playthrough test passes deterministically.
- All checklist items pass on the device matrix; zero known crash bugs.

## Prompt for Claude
> Read `00-overview.md` and `16-testing-and-qa.md`. Review existing tests for gaps against the table, add missing ones (including the headless full-loop integration test and perf/leak tests), add CI workflow files, `QA_CHECKLIST.md`, the soak auto-player, and the on-device debug-info/log export. Report the coverage numbers and any untested risky areas.
