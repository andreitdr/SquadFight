# Step 09 — Campaign & Story Progression

**Depends on:** 08. **Size:** M

## Goal
Chapter-by-chapter, fight-by-fight story progression with locking, replaying, star ratings and story beats.

## Rules
- Chapters are ordered; fights inside a chapter are ordered.
- A fight unlocks when the previous fight is cleared; the first fight of a chapter unlocks when the previous chapter's last fight is cleared (config can override via `UnlockCondition`).
- Cleared fights are replayable any number of times (A7: no stamina) with normal drop rolls.
- **First-clear reward** (guaranteed, configurable) granted once, tracked in `progress`.
- **Stars (optional, config on):** up to 3 per fight (e.g., win; no hero dead; under N turns). Best stars stored. Chapter star bonuses optional.
- **Chapter-clear reward:** claimed once.
- Story beats: before/after fight dialogue entries (`StoryBeatDefinition`: speaker key, text key, portrait ref). Skippable, shown once unless replayed from a gallery.
- Defeat: no penalty beyond no rewards (config option `defeatConsolation`).

## Data additions
- `StoryBeatDefinition`, `FightDefinition.preBeats/postBeats`, `UnlockCondition` (ClearFight id, MinPartyPower, HeroOwned id).

## Services
`ProgressService`:
```
bool IsFightUnlocked(fightId)
bool IsChapterUnlocked(chapterId)
FightProgress GetFightProgress(fightId)   // cleared, stars, clears count
Result CompleteFight(fightId, BattleResult) // computes stars, marks cleared, triggers RewardService (step 10)
FightRef GetNextRecommendedFight()
```
`BattleFlowController` (Services): orchestrates start fight → simulate/play → result → rewards → progress; used by UI.

## Tasks
1. Core `ProgressionRules` (unlock evaluation, star evaluation) + tests.
2. `ProgressService` + save integration + events (`FightCompleted`, `ChapterCompleted`).
3. `BattleFlowController` with an interface so UI/presentation can plug in.
4. Story beats data + simple provider.
5. Sample content: 3 chapters × 8 fights, mixed waves, one boss per chapter, increasing difficulty.
6. Tests: unlock chain, first-clear only once, star conditions, replay, last fight→next chapter.

## Acceptance criteria
- Playing via debug console from fight 1-1 to the last fight unlocks everything in order.
- Progress persists after restart.
- Content can be reordered by editing a Chapter asset only.

## Prompt for Claude
> Read `00-overview.md` and `09-campaign-progression.md`. Implement `ProgressionRules` (Core), `ProgressService`, `BattleFlowController`, story beat data/provider, plus sample chapter content. Hook CompleteFight to a `RewardService` interface stub (implemented in step 10). Provide unit tests as listed.

## Decisions made
- Stars (config on): win = 1, +1 no hero died, +1 within the fight's round limit (`starTurnLimit`, default `defaultStarTurnLimit`); stars off → a win is 1
- Fight completion (progress + rewards) runs in one service transaction and is saved immediately (SaveNow) before the result screen
- Retreat = defeat (consolation table if configured); defeats still count in `progress.totalFights`
- Battle seeds are drawn from the persisted game RNG; replays pass a fixed seed; "skip" = `RunToEnd` (same outcome)
- Story beats: unseen pre/post beats shown once, gallery shows seen beats; `replay` flag shows all
- Content: 3 chapters × 8 fights (boss at fight 8 with a turn limit), 14 enemies (3 bosses); recommended power left 0 for the step-15 tool
