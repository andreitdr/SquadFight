# Step 11 — UI Foundation

**Depends on:** 04 (can run in parallel with 05–10). **Size:** M

## Goal
A reusable mobile UI framework: navigation, theming, safe areas, view-model binding, popups, and common components. Screens in step 12 are then quick to build.

## Decisions (A9, A11)
- uGUI + TextMeshPro, portrait, reference resolution 1080×1920, Canvas Scaler "Scale With Screen Size" (match 0.5).
- MVVM-lite: each screen has a `ViewModel` (plain C#, in Game.UI, testable) and a `View` MonoBehaviour. Views subscribe to `EventBus`, never mutate services directly except via ViewModel commands.

## Components
- `UIManager`: screen stack (push/pop/replace), popup layer, toast layer, loading overlay, back-button handling (Android back).
- `ScreenBase` / `PopupBase` with lifecycle (`OnShow`, `OnHide`, animations in/out).
- `SafeAreaFitter` (notches), `AspectHelper`.
- `Theme` ScriptableObject: rarity colors, fonts, button styles, spacing.
- Reusable widgets: `PrimaryButton`, `IconButton`, `CurrencyBar`, `RarityFrame`, `HeroCard`, `ItemCard`, `ShardProgressBar`, `StatRow`, `Tabs`, `ScrollList` (pooled), `ConfirmPopup`, `RewardPopup`, `Toast`.
- Input: Input System, tap/long-press detection, haptics wrapper (`IHaptics`), disable multi-touch where undesired.
- Transitions: screen fade, button press scale; all via a tiny tween helper (or DOTween if allowed — decision D4, default: own minimal tween to avoid dependencies).
- Localization hooks: all text via `ILocalizer.Get(key)`; widgets accept keys (full setup in step 14).

## Tasks
1. UI scene scaffolding in `Main` scene with canvases (Screens, Popups, Overlay).
2. Implement `UIManager`, base classes, Android back handling.
3. Implement `Theme` + widgets as prefabs with documented usage.
4. `ViewModel` base with `INotifyChanged`-style events; example view model unit test.
5. A "UI Gallery" dev scene showing every widget for visual QA at multiple aspect ratios (16:9, 19.5:9, 4:3 tablets).
6. Pooled scroll list with 1,000 items stays at 60 fps (verify in profiler).

## Acceptance criteria
- Gallery scene renders correctly on notched/tall/tablet simulator sizes.
- Back button/gesture pops screens/popups correctly; no leaks (instances destroyed/pooled).
- No service mutation inside Views (code review checklist).

## Prompt for Claude
> Read `00-overview.md` and `11-ui-foundation.md`. Implement the UI framework in Game.UI: UIManager, ScreenBase/PopupBase, Theme, SafeAreaFitter, widget scripts, pooled scroll list, minimal tween helper, and a UI Gallery scene-builder editor script. Provide the prefab/hierarchy setup as a checklist for me where code can't create it. Include ViewModel unit tests.
