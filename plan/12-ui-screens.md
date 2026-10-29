# Step 12 — UI Screens

**Depends on:** 05–10 and 11. **Size:** L

## Goal
All player-facing screens, wired to services through view models.

## Navigation map
```
Boot → Main Menu ─┬─ Campaign Map → Fight Preview (party) → Battle → Result/Rewards
                  ├─ Heroes → Hero Detail ─┬─ Equipment picker
                  │                        └─ Unlock / Star-up
                  ├─ Party Builder
                  ├─ Inventory
                  └─ Settings
```
Persistent top bar: currencies, (optional) player name. Bottom tab bar: Campaign, Heroes, Inventory, Settings.

## Screens & requirements
1. **Main Menu / Home** — Continue button, chapter progress summary, red-dot notifications (hero unlockable, star-up available, new better item).
2. **Campaign Map** — chapters list/carousel, fights as nodes in a path, locked/cleared/stars state, recommended power vs. party power, boss marker, story replay.
3. **Fight Preview** — enemy list with weaknesses/info, drop preview (shows possible reward categories, not exact odds unless config flag), party quick-edit, Start button, Auto toggle.
4. **Party Builder** — 5 slots, drag or tap to assign from owned heroes, party power, filters (rarity/class), duplicate prevention, save automatically.
5. **Heroes Collection** — grid of all heroes (owned + silhouettes of locked), shard progress bar `have/need`, rarity frame, sort/filter; tapping opens detail.
6. **Hero Detail** — final stats (with breakdown base/star/equipment), skills, star level, 7 equipment slots with tap-to-change, Unlock button (if enough shards) or Star-up button, lore.
7. **Equipment Picker** — filtered list for the slot, compare vs. equipped (stat deltas green/red), equip/unequip, "equipped by X" badge, optional sell.
8. **Inventory** — all items, filter by slot/rarity, sort, item detail popup, sell/bulk-sell low rarity (optional).
9. **Battle HUD** — in step 13 (HP bars, skill buttons, speed, auto, pause/retreat).
10. **Result / Rewards screen** — victory/defeat, stars earned, animated reward reveal (shards with progress change, items with rarity glow), "Hero unlocked!" celebration popup, Next fight / Retry / Back.
11. **Settings** — volume sliders, language, vibration, battle speed default, reset progress (double confirm), credits, privacy policy link, version.

## Cross-cutting
- Empty states, loading states, error popups for `Result` failures with friendly messages.
- Red-dot notification system driven by events.
- Tutorial anchors (`TutorialAnchor` component) on key buttons for step 18.

## Tasks
1. ViewModels with unit tests (hero unlock info, party power, item comparison).
2. Build screens in order: Heroes/Hero Detail → Inventory/Equipment → Party → Campaign/Preview → Result → Settings → Home.
3. Wire everything through `UIManager`; test each flow end-to-end using debug console to grant resources.
4. Accessibility basics: min touch target 48dp, readable fonts, colorblind-safe rarity (icon + color).

## Acceptance criteria
- The full loop is playable on device: pick party → fight (using step 13) → get rewards → unlock hero → equip → progress.
- No screen shows raw keys or placeholder text; no dead-end navigation.
- All screens usable on 16:9, 19.5:9 and tablet aspect ratios.

## Prompt for Claude
> Read `00-overview.md`, `11-ui-foundation.md` and `12-ui-screens.md`. Implement the listed screens in Game.UI with view models bound to existing services, one screen at a time in the order given. For each screen give me the prefab/hierarchy checklist and a short manual test script. Include view-model unit tests.
