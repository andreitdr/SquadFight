# Step 17 — Mobile Optimization & Builds

**Depends on:** 12, 13. **Size:** M

## Goal
Small, fast, battery-friendly builds that run well on low-end phones and pass store requirements.

## Performance targets
- 60 fps in battle, 30 fps floor on low-end devices (3 GB RAM, ~2019 hardware).
- Cold start < 5 s; scene loads < 2 s.
- RAM < 600 MB peak; no growth after 50 fights.
- APK/AAB size target: < 150 MB (decide D6); install size low via compression.
- Battery: cap frame rate sensibly (menus 30 fps option), no busy loops.

## Optimization checklist
- **Graphics:** URP 2D with minimal renderer features; sprite atlases (Sprite Atlas V2); texture compression ASTC (Android/iOS); max texture sizes; avoid transparent overdraw; batching verification (Frame Debugger).
- **UI:** split canvases (static vs. dynamic), disable raycast on non-interactive graphics, avoid layout groups in lists (pooled lists), TMP font atlas size control.
- **Memory/GC:** object pooling, no per-frame allocations (profile with Deep Profile & Memory Profiler), avoid LINQ in hot paths, cache lookups; incremental GC enabled.
- **Audio:** compress music (Vorbis), streaming for long tracks, short SFX decompress-on-load.
- **Code:** IL2CPP + managed stripping (link.xml for reflection/JSON models), ARM64 only (+ ARMv7 only if market needs).
- **Assets:** check unused assets with Build Report; no Addressables needed (offline) unless size requires on-demand packs (Play Asset Delivery).
- **Device features:** adaptive performance package (optional), thermal throttling response (lower VFX quality).
- **Quality tiers:** Low/Med/High settings auto-selected by device RAM/GPU; VFX density, shake, shadows.

## Build pipeline
- Build script (`Game.Editor/BuildScript.cs`): sets version/bundle code from a version file, runs config validation, builds AAB (release) and APK (dev), iOS Xcode project.
- Android: keystore handling via env vars (never commit), target latest required API level, 64-bit, app bundle, Play Asset Delivery optional, enable R8/minify rules, backup rules (**disable auto-backup of save OR allow it deliberately — decide D7**; allowing Android Auto Backup can restore progress after reinstall, a nice offline-game feature).
- iOS: signing profiles, `Info.plist` privacy keys (none needed if no tracking), exclude from iCloud backup vs. allow (same decision as D7), App Transport Security not applicable.
- Development vs. release defines: `DEV_BUILD` enables debug console and overlay; release strips them.
- Reproducible build number: `versionCode = yyMMddNN` or incremental from file.

## Tasks
1. Profile on the lowest-end device; record baseline in `PERF_REPORT.md`.
2. Apply optimizations iteratively (largest wins first); re-measure.
3. Implement quality tiers + auto detection.
4. Write build script and CI release workflow; document signing setup.
5. Size report via Build Report; remove unused assets/packages.
6. Test the offline guarantee: build must work with **no INTERNET permission** declared (remove it from manifest; verify no packages require it).

## Acceptance criteria
- Targets met on low-end device; `PERF_REPORT.md` shows before/after.
- Release AAB builds from command line with a single command.
- Manifest has no network permission; game runs in airplane mode.

## Prompt for Claude
> Read `00-overview.md` and `17-mobile-optimization-and-builds.md`. Implement the build script (Android AAB/APK, iOS project), version management, DEV_BUILD stripping, quality tier auto-selection, link.xml/stripping config, manifest cleanup (no INTERNET permission), and document the optimization checklist results needing device profiling as a task list for me. Provide code for pooling/allocation fixes found by reviewing hot paths.
