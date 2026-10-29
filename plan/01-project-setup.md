# Step 01 — Project Setup & Architecture Skeleton

**Depends on:** nothing. **Size:** S

## Goal
**The owner already created an empty Unity project** (the folder Claude was opened in). Do NOT create a new project or re-run any "create project" flow. Audit the existing project and set it up with the layered architecture, assembly definitions, scene flow, and tooling, so it builds to a device and every later step just adds code in the right place. with the layered architecture, assembly definitions, scene flow, and tooling in place, so every later step just adds code in the right place.

## Deliverables
- The existing Unity project, configured: portrait, Android + iOS build targets/modules noted, IL2CPP/ARM64. Keep the Unity version already in `ProjectSettings/ProjectVersion.txt` (never upgrade it without asking).
- Folder structure from the overview.
- Assembly definitions: `Game.Core` (no engine refs), `Game.Data`, `Game.Save`, `Game.Services`, `Game.UI`, `Game.BattleView`, `Game.Editor` (Editor only), `Game.Tests.EditMode`, `Game.Tests.PlayMode`.
- Scenes: `Boot` (composition root), `Main` (menus), `Battle`.
- `.gitignore` (Unity), `.gitattributes` (Git LFS for art/audio), `README.md`.
- `Directory.Build.props`/`.editorconfig` with C# style rules; nullable enabled where supported.
- Packages: TextMeshPro, Input System, Test Framework, Localization (step 14), Addressables **not** needed (offline, bundled).

## Tasks
0. **Audit first (read-only):** locate the project root (folder with `Assets/`, `Packages/`, `ProjectSettings/`), read `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`, and list `Assets/`. Detect the template (URP 2D / URP 3D / Built-in) and what is already installed (TextMeshPro, Input System, Test Framework). Record findings in `PROGRESS.md` > Environment. Do not delete or overwrite anything the template created (e.g. `Assets/Scenes/SampleScene`, `Settings/`, URP assets); reuse them, and move/rename the sample scene into `Boot` only if it is untouched.
   - If the template is not 2D: keep it. The game is 2D sprites + uGUI; add a 2D-friendly camera setup. Only ask the owner if the template makes a plan assumption impossible.
   - Add missing packages by editing `Packages/manifest.json` (Input System, TextMeshPro/uGUI, Test Framework, Localization later). Use versions compatible with the installed Unity version.
1. Set Player Settings (via an Editor script or by editing `ProjectSettings/ProjectSettings.asset` carefully): company/package name `com.<you>.<game>`, portrait only, minimum Android API per current Unity requirement, IL2CPP + ARM64, target frame rate 60.
2. Create folders and `.asmdef` files; set reference graph exactly as in overview (Core references nothing).
3. Boot scene: `GameBootstrap` MonoBehaviour that builds a `ServiceLocator` (simple dictionary of interface→instance), loads config, then loads `Main`.
4. `SceneLoader` helper (async, with fade hook for later).
5. Define interfaces only (empty implementations): `IRandom`, `IClock`, `ISaveStorage`, `ILogger`.
6. `SystemRandom : IRandom` (seedable) and `SystemClock : IClock` in Core.
7. Debug overlay (FPS, memory) toggled by 3-finger tap, stripped from release builds via `#if`.
8. Set up CI-ready scripts: `build.ps1`/`build.sh` calling Unity batch mode for Android (can be stubs).
9. Write first EditMode test proving Core compiles with no engine refs.

## Technical notes
- Enable "Auto Referenced" **off** on all asmdefs; reference explicitly.
- Keep `Game.Core` `noEngineReferences: true`.
- Use `UnityEngine.Debug` only behind `ILogger`.

## Acceptance criteria
- Project opens with zero errors/warnings; Boot → Main loads on an Android device/emulator.
- Adding `using UnityEngine;` in Core fails compilation (verified).
- Test runner shows the sample EditMode test passing.

## Prompt for Claude
> Read `00-overview.md` and `01-project-setup.md`. The Unity project already exists in the current folder: audit it first, then set up the structure described: folders, assembly definition files (as `.asmdef` JSON), the Boot/Main/Battle scene bootstrap scripts, `ServiceLocator`, `SceneLoader`, `IRandom`/`IClock`/`ISaveStorage`/`ILogger` interfaces and default implementations, `.gitignore`, `.editorconfig`, and one EditMode test. Do not add game content yet. Automate scene creation and Player Settings with Editor scripts run in batch mode; only list a manual step if it truly cannot be scripted.

## Decisions made (fill after completion)
- Unity version: ___
- DI approach: ServiceLocator
