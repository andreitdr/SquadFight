# QA checklist

Automated coverage (run before every release candidate):
```bash
./build.sh tests                       # Unity EditMode + PlayMode (incl. full loop, 20-battle leak check, screen tour)
dotnet test DotnetTests/GameTests      # pure tests (+ --collect:"XPlat Code Coverage")
./build.sh validate                    # config validator (also runs as a build gate)
dotnet run -c Release --project DotnetTests/BalanceCli -- progression 1,2,3
```
Soak test on a device (dev build): open the console (four-finger tap) → `soak 500` → expect `0 errors`; check the
FPS/memory overlay (three-finger tap) before/after; Settings → Copy debug info and look for errors in the log.

## Device matrix
| Device class | Example | RAM | Status |
|---|---|---|---|
| Low-end Android | Galaxy A10 / Redmi 8 (2019) | 2–3 GB | pending |
| Mid Android | Pixel 6a / Galaxy A54 | 6 GB | pending |
| Recent Android | Pixel 9 / Galaxy S24 | 8+ GB | pending |
| iPhone | iPhone 12 or newer | 4+ GB | pending |
| Tablet | iPad 9th gen / Galaxy Tab A8 (4:3 / 16:10) | 3+ GB | pending |

## Checklists (tick per device)
**Install & save**
- [ ] Fresh install: Boot → Home in < 5 s, tutorial starts (after step 18), first fight playable.
- [ ] Upgrade install over the previous build: progress kept (save migration), no reset.
- [ ] Airplane mode from first launch: everything works (no network permission in the manifest).
- [ ] Kill the app during a battle: on relaunch, the battle is gone (no reward), progress before it is intact.
- [ ] Kill the app on the result screen: rewards are present after relaunch exactly once.
- [ ] Rapidly tap Fight / Next / reward buttons: no double battles, no duplicated rewards.
- [ ] Low storage (< 50 MB free): playing on does not crash; a failed save is retried (no data loss).
- [ ] Settings → Reset progress (double confirm) gives a clean new game; settings kept.

**Lifecycle & system**
- [ ] Background 30 min during a battle and on the home screen → resume correctly; music resumes.
- [ ] Incoming call / notification shade / alarm during a battle → audio pauses and resumes.
- [ ] Headphones plugged/unplugged → audio continues on the right output.
- [ ] Rotation locked to portrait (device auto-rotate on).
- [ ] Notch / punch-hole / rounded corners: nothing important under cut-outs (top bar, battle HUD, skill bar).
- [ ] Android back: closes popups, then screens, then asks to quit; in battle opens pause.
- [ ] Language: switch EN ↔ RO in Settings; all visible text changes; restart keeps the language.
- [ ] Vibration toggle respected; sound/music sliders persist after restart.

**Gameplay flows** (manual test script)
1. Home → Continue → Preview shows enemies, rewards, power → Fight! → battle plays → Victory → result shows gold, shards (bar animates), item.
2. Heroes → Sister Maren (red dot) → Unlock → celebration → Add to party.
3. Hero detail → equipment slot → picker shows green/red deltas → Equip → stats update; equip an item worn by another hero → confirm move.
4. Party → tap heroes to add/remove, tap two slots to swap, Auto-fill → power updates in the top bar.
5. Battle: Auto off → tap a hero's ready skill (ring) → used on its next turn; speed ×1/×2/×3; no Skip button; Pause → Retreat → defeat, no rewards.
6. Bag → filter by slot → item popup → Sell → gold increases; "Sell spare commons" keeps upgrades.
7. Campaign → chapters ◀ ▶, locked fights toast, Story replay.

**Performance** (dev build, overlay on)
- [ ] 5v5 boss fight (fight 1-8, 2-8, 3-8) ≥ 60 fps mid device, ≥ 30 fps low-end.
- [ ] Heroes / Bag lists scroll at 60 fps (Bag with 200+ items: `give item eq_iron_sword` repeatedly or `soak 500`).
- [ ] Memory stable after 50 battles (overlay Total MB within +10%).

## Exploit review (done in code, re-check on device)
- Rewards are applied and saved atomically before the result screen is shown (kill = all-or-nothing).
- A battle can be started only once at a time; the outcome is recorded once (Finish/Retreat idempotent).
- RNG state is saved: restarting cannot re-roll a drop.
- Device clock changes do not affect anything (no time-based rewards yet).

## Bug report template
```
Title: <short, specific>
Build / version: (Settings → bottom)        Device + OS:
Severity: S1 / S2 / S3 / S4                Frequency: always / often / rare / once
Steps to reproduce:
1.
Expected:                                   Actual:
Debug info: (Settings → Copy debug info, paste here)
Screenshot / video:
```

## Severity rubric
| Severity | Meaning | Release |
|---|---|---|
| S1 Blocker | crash, progress/save loss, cannot continue, reward duplication | must fix |
| S2 Major | feature broken with no workaround, wrong rewards, stuck screen | must fix |
| S3 Minor | visual glitch, wrong text, workaround exists | fix if cheap |
| S4 Trivial | polish, cosmetic | backlog |
