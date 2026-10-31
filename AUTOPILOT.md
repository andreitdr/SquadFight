# AUTOPILOT — Build the whole game without hand-holding

You (Claude) are the sole developer of this project. The owner gave you ONE start prompt and expects you to build the entire game by yourself, step by step, until it is done. The owner does not copy prompts, does not run commands for you, and does not want a running commentary.

## Project location
The owner has **already created an empty Unity project** and opened Claude in that folder. That folder is the project root (it contains `Assets/`, `Packages/`, `ProjectSettings/`). Never create a new Unity project. Keep all autopilot files (`AUTOPILOT.md`, `CLAUDE.md`, `PROGRESS.md`, `DECISIONS.md`, `plan/`) at the root, outside `Assets/`. On the very first run, audit the project as described in step 01 task 0 before changing anything, and make the first commit a snapshot of the untouched project (after adding a proper Unity `.gitignore`).

## Source of truth
- `plan/00-overview.md` — vision, architecture, assumptions (A1–A12), step order.
- `plan/01-…` to `plan/18-…` — one spec per step.
- `PROGRESS.md` — your own state file (create it on first run from the template at the bottom). **Read it first in every session; update it after every step.**
- `DECISIONS.md` — append every decision you make (what, why, which step).

## Important: how to read the step files
Each step file ends with a "Prompt for Claude" block and some phrases like "give me a checklist of manual steps" or "provide the prefab checklist for me". Those were written for a human-driven workflow. **Ignore that framing.** Treat the step file's Goal, Rules, Tasks, Acceptance criteria and the prompt block as your *task spec*, and do the work yourself.

## The loop (repeat until step 18 is done)
1. Read `PROGRESS.md`; pick the first step not marked DONE whose dependencies are DONE (overview §6). Steps 11 may run any time after 04.
   Create a task branch for it from the current branch: `git switch -c task/step-NN-<slug>` (see "Branch workflow" in `CLAUDE.md`).
2. Read `plan/00-overview.md` and that step's file.
3. Implement everything in the step: code, assembly definitions, assets, tests.
4. Verify (see "Verification").
5. Fill the step file's "Decisions made" section and append to `DECISIONS.md`.
6. Commit on the task branch: `step-NN: <title>` (git init on first run; Git LFS rules from step 01). Then merge it back into the branch it came from with `git merge --no-ff`; resolve conflicts (re-run affected tests), or `git merge --abort` and ask the owner if they cannot be resolved safely. Delete the merged branch with `git branch -d`.
7. Update `PROGRESS.md` (status, test results, open questions, anything deferred). Then start the next step immediately. **Do not stop to report between steps and do not ask permission to continue.**

## Do it yourself — automate everything automatable
- **Unity scenes, prefabs, ScriptableObject assets, Player Settings, packages (`manifest.json`)**: create them through code. Write Editor scripts (`Tools/Game/...`) and run them with Unity batch mode (`Unity -batchmode -nographics -quit -projectPath . -executeMethod ...`), or write the YAML/JSON directly when that is reliable. Never hand the owner a "click here, then there" checklist for something a script can do.
- **Tests**: run EditMode/PlayMode tests in batch mode (`-runTests -testPlatform EditMode -testResults ...`) when a Unity install is available. Fix failures yourself.
- Find the Editor matching `ProjectVersion.txt` (Unity Hub default paths on Windows `C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe`, macOS `/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity`, Linux `~/Unity/Hub/Editor/<version>/Editor/Unity`). Note: batch mode fails if the project is already open in the Editor; if so, ask the owner once to close it, or use a copy of the project for test runs.
- **If Unity is NOT installed in your environment**: first look for it (`which unity-editor`, `/opt/unity`, `~/Unity/Hub`, GameCI docker image `unityci/editor`). If you cannot get it, still write everything (Core is pure C#: compile and test it with the `dotnet` SDK in a parallel `.csproj` test harness that links the same source files). Record in `PROGRESS.md` exactly which verifications are still pending real Unity, and batch them into one question for the owner at the end of the milestone, not one per step.
- **Content** (heroes, items, chapters, drop tables, story text): generate sensible placeholder content yourself, in config, following the schemas. Use the step-15 tools and simulators to tune balance until the acceptance numbers pass (e.g. no difficulty walls, first hero unlock reachable in a sensible number of fights).
- **Art/audio**: generate placeholders (colored shapes, procedural/CC0 sounds, listed in credits). Mark them `placeholder` so step 18 can audit them.
- **Git, CI files, build scripts, docs**: all yours.

## When to ask the owner (and ONLY then)
Ask when one of these is true:
1. **Ambiguous design decision with real consequences** that the plan/assumptions don't settle and that is expensive to reverse (e.g. combat style change, monetization, hero roster/theme/art style, game name, target store accounts).
2. **Something only the owner can provide or do**: Unity license/installation, signing keystore, store accounts, real device testing, final art, legal text approval.
3. **You are blocked** after genuinely trying two or three different approaches.

How to ask:
- Use the AskUserQuestion tool with 2–4 concrete options and your recommended one first. Never open-ended "what should I do?".
- **Batch** questions: collect everything you will need for the next few steps and ask once.
- **Never block on a question you can safely default.** If it is cheap to change later, pick the default, log it in `DECISIONS.md` marked `DEFAULTED – owner may override`, and keep going. If the owner is not present, take the most reasonable option, log it, continue.
- The defaults already written in the step files (D1…D9) are approved. Use them.

## Owner suggestions
If the owner sends a change request at any time: create a task branch for it from the current branch (`git switch -c task/<slug>`), update `plan/00-overview.md` (assumptions table) and the affected step files first, note it in `DECISIONS.md`, redo any already-DONE step it affects (mark it REWORK in `PROGRESS.md`), merge the task branch back as in loop step 6, then continue the loop.

## Verification (a step is DONE only when all are true)
- Everything compiles with zero errors and no new warnings.
- All tests listed in the step exist and pass (or are logged as pending-Unity per above).
- Each acceptance criterion in the step is checked off in `PROGRESS.md` with how it was verified.
- Core has no `UnityEngine` references; no balance numbers hard-coded in code.
- At the end of steps 10, 13 and 18 (milestones M1, M2, M3), also run: config validator, a headless simulated playthrough, and the progression simulator, and record the results.
- For high-stakes steps (03 save system, 10 rewards/atomicity, 17 builds), do a second self-review pass looking only for data-loss, duplication and crash bugs before marking DONE. Use a subagent for the review if one is available.

## Working style
- Small, frequent commits inside a step are fine; one final `step-NN` commit marks completion.
- Keep the owner's attention for the end: at each milestone (M1, M2, M3) write a short `MILESTONE-Mx.md` (what works, how to try it, known gaps, any pending questions) and send a one-paragraph message. Otherwise stay quiet.
- If context is getting long, rely on `PROGRESS.md` + `DECISIONS.md` and continue; they are designed so a fresh session can resume exactly where you stopped.
- Never delete the owner's files; never commit secrets (keystores via env vars only).
- Be honest in `PROGRESS.md`: if something is stubbed, partial or unverified, say so.

## PROGRESS.md template (create on first run)
```
# Progress
Last updated: <date>
Current step: 01

| Step | Title | Status (TODO/DOING/DONE/REWORK) | Tests | Notes / pending |
|------|-------|-------------------------------|-------|-----------------|
| 01 | Project setup | TODO | | |
| 02 | Data model & config | TODO | | |
| … | … | | | |
| 18 | Polish, tutorial, release | TODO | | |

## Environment
- Unity project root: ?
- Unity version (from ProjectVersion.txt): ?
- Template / render pipeline: ?
- Unity Editor executable available to Claude: ? (path)
- dotnet available: ?
- Git initialised: ?

## Pending owner questions (batched)
- none

## Pending verifications (need real Unity / device)
- none
```
