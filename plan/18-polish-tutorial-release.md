# Step 18 — Polish, Tutorial & Release

**Depends on:** all previous steps. **Size:** M

## Goal
Turn a working game into a shippable one: onboarding, juice, content completion, legal/store requirements, and launch.

## Onboarding & tutorial
- Scripted first session: Story beat → first fight (auto-win-friendly) → reward → **guided unlock of a hero** (granted enough shards on purpose) → party edit → equip first item → second fight.
- `TutorialService` (steps stored in save so it resumes): highlights `TutorialAnchor` objects with a mask + pointer, blocks other input, skippable after first completion.
- Contextual tips (first time viewing shards, first duplicate shard → star-up explanation).

## Polish
- Juice: button feedback, reward reveal animations, rarity glow, unlock celebration, number count-up, haptics.
- Screen transitions, loading states, empty states, consistent iconography.
- Final art/audio integration; replace placeholders (checklist generated from validator: assets flagged `placeholder`).
- Accessibility: font scaling, reduce-motion, colorblind rarity icons.
- Content pass: finalize hero roster, equipment sets, chapters, story text, and run progression simulator (step 15) for pacing sign-off.

## Meta features (optional, still offline)
- Daily login bonus using device clock (with simple clock-tamper tolerance, since there's no server; decide whether to care — D8).
- Achievements/stats screen; hero/item codex.
- Offline idle rewards (optional).
- Share/rate prompts (store review API).

## Release checklist
- Legal: privacy policy page (state: no data collected, fully offline), EULA if desired, third-party license list (`THIRD_PARTY_NOTICES.md`).
- Store assets: icon (adaptive icon on Android), screenshots (phone + tablet), feature graphic, trailer/GIF, short + long descriptions, keywords, age rating questionnaire, Data Safety form (no data collected), content rating.
- Google Play: developer account, internal → closed → open testing track, pre-launch report; App Store: TestFlight, App Review notes.
- Crash reporting: none needed offline, or optional opt-in analytics later (would require INTERNET permission — decide D9; default none).
- Versioning: 1.0.0 tag, release notes, keep `save` migration tests current for post-launch updates.
- Post-launch plan: hotfix process, balance patch via config update, content updates (new chapters = new assets + save-compatible migrations).

## Tasks
1. Tutorial service + scripted steps + tests for step persistence.
2. Polish pass list (track in issues), placeholder audit.
3. Content completion & pacing sign-off with simulators.
4. Store listing materials and legal pages.
5. Release candidate build, full QA checklist (step 16), staged rollout.

## Acceptance criteria
- A new player completes the tutorial without guidance and understands shards, unlocking, equipment, and party.
- No placeholder assets remain in release build.
- Store checklists complete; RC passes QA matrix; 1.0.0 submitted.

## Prompt for Claude
> Read `00-overview.md` and `18-polish-tutorial-release.md`. Implement `TutorialService` with saved progress, anchors and overlay masking, contextual first-time tips, the placeholder-asset audit tool, and the optional daily bonus (behind a config flag). Draft the privacy policy text (offline, no data collected), `THIRD_PARTY_NOTICES.md` generator, and a store-listing checklist file I can fill in.

## Decisions made
- TutorialService (13 scripted steps, saved after each step, skippable, replayable from Settings); overlay = 4 blockers around the target anchor (only the target is tappable), pulsing ring, hint, Skip
- Guided first session: fight 1-1 first clear grants Sister Maren's 20 shards + a Wooden Sword → unlock → add to party → equip → second fight
- Existing players (save v3 with fights played) skip the tutorial via migration 3→4
- Contextual tips once each after the tutorial (shards, star-up, equipment, party power)
- D8 daily bonus implemented behind `dailyBonusEnabled` (default off): local date, claims only on a strictly later date (clock rewinds never pay), gaps reset the streak
- D9 no analytics/crash reporting (offline, no INTERNET permission)
- Placeholder audit tool + release-build warning (BLOCK_PLACEHOLDERS=1 makes it fatal for the final 1.0 build); 35 placeholder assets remain (no final art yet)
- Legal/store: PRIVACY_POLICY.md, THIRD_PARTY_NOTICES.md (generated), STORE_LISTING.md checklist
- Polish: gold count-up on results, unlock celebration, rarity glow, button press scale/sound/haptics, reduce-motion setting
