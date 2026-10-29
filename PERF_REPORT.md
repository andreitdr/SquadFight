# Performance report

Status: **device profiling pending** (owner deferred device tests at M2). Numbers below were measured on the development
Mac (Editor batch mode / dotnet) and from the Android build; the device task list is at the end.

## Measured
| Metric | Target | Measured | How |
|---|---|---|---|
| 5v5 battle simulation | < 5 ms | well below (avg reported by `Performance_5v5_Under5ms`) | dotnet + Unity EditMode |
| Managed memory after battles | no growth after 50 fights | +0.02 MB over 18 Main↔Battle round trips | PlayMode `TwentyBattleRoundTrips_NoLeaks` |
| Pooled list, 1,000 rows | 60 fps | < 30 live rows, < 40 ever created, < 2 ms layout per scroll step | PlayMode `RecycleList_1000Items_StaysBoundedAndFast` |
| Battle FX allocations | pooled | < 60 floating texts/sparks created for a full boss fight | PlayMode presentation test |
| 500-fight soak | 0 errors | 0 errors, save stays consistent | `Soak_500Fights_NoErrors_SaveStaysHealthy` |
| Android dev APK (IL2CPP, ARM64) | < 150 MB (D6) | 47 MB (development build, with symbols) | `./build.sh android-dev` |
| Network permission | none | none (`aapt dump permissions`) | Android build |

Largest assets: `Builds/size-report.md` (Unity splash logo, TMP font atlases, URP shaders, placeholder sprites).

## Optimization checklist
| Item | State |
|---|---|
| Rendering | 2D only; UI on uGUI; no lights/shadows/post-processing used; camera clears to a solid color |
| Canvases | split by layer (background, screens, popups, toasts, overlay); text/decor `raycastTarget` off; lists pooled without layout groups |
| GC | incremental GC on; pooled FX and turn-order dots; no LINQ in per-frame paths of the battle HUD; simulator LINQ only per turn |
| Audio | music Vorbis streaming, SFX ADPCM decompress-on-load, mono |
| Code | IL2CPP, ARM64 only, managed stripping Medium (release) with `link.xml` for save types |
| Frame rate | quality tiers: Low (≤ 3 GB RAM or ≤ 4 cores) menus 30 fps / battles 60 fps; others 60 fps; vSync off |
| Effects | Low tier disables decorative sparks/projectiles (gameplay feedback stays) |
| Builds | config validation gate, INTERNET permission stripped, size report each build |
| Not used | Addressables, Play Asset Delivery (build is small) |

## Device task list (for the owner / QA)
1. Install `Builds/Android/*-dev.apk` on a low-end (3 GB) and a mid-range device; enable the FPS overlay (three-finger tap).
2. Cold start: time from icon tap to Home (< 5 s). Scene loads Main↔Battle (< 2 s).
3. Play fight 1-8/2-8/3-8 (5v5 boss): FPS ≥ 60 mid, ≥ 30 low; note drops during crit/boss banners.
4. Scroll Heroes and Bag (after `soak 500` to fill the bag) — 60 fps.
5. Memory: overlay "Total MB" before/after `soak 500` and after 50 presented battles (< 600 MB peak, no growth).
6. Battery/thermal: 20 minutes of battles at ×1; check the device does not throttle heavily.
7. Record numbers in this file (before/after table) and attach Unity Profiler captures if a target is missed.
