# Store listing checklist

Fill in / tick before submitting. Items marked (owner) need accounts, legal approval or final art.

## Identity
- [ ] Final game name (current working title: **SquadFight**) (owner)
- [ ] Package / bundle id: `com.andreitdr.squadfight` (change in `ProjectSetup.cs` before the first upload — cannot change later) (owner)
- [ ] Developer name, website, support email (owner)
- [ ] Final app icon 512×512 + Android adaptive icon (foreground/background) + iOS 1024×1024 (owner/art)

## Texts (draft)
- **Short description (80):** Collect heroes, gear them up and battle through an offline fantasy campaign.
- **Long description:** Assemble a squad of five heroes and fight chapter by chapter through the Greenwood, a haunted crypt and the dragon's peak. Every fight drops gold, equipment and hero shards — collect shards to unlock new heroes and raise their stars, equip weapons, armor and rings, and watch your squad grow stronger. Turn-based auto battles with skills you can trigger yourself, three-star challenges, and no internet, ads or accounts required.
- [ ] Keywords / tags: hero collector, RPG, offline, auto battler, fantasy, turn-based
- [ ] Translations of the listing (en ✔ draft, ro – to do)

## Graphics (owner/art)
- [ ] Phone screenshots 1080×2340 (≥ 4): Home, Battle, Heroes, Result. Placeholder captures: `TestResults/screens/tour_*.png`, `battle_mid.png`
- [ ] Tablet screenshots (7" and 10")
- [ ] Google Play feature graphic 1024×500
- [ ] Optional trailer / gameplay GIF

## Compliance
- [ ] Privacy policy URL (host `PRIVACY_POLICY.md`) (owner)
- [ ] Google Play **Data safety**: "No data collected", "No data shared"
- [ ] Apple **App Privacy**: "Data Not Collected"
- [ ] Content rating questionnaire (IARC): fantasy violence, no blood, no purchases, no user interaction → expected PEGI 7 / Everyone 10+
- [ ] Target audience & ads declaration: no ads
- [ ] `THIRD_PARTY_NOTICES.md` included / linked
- [ ] Export compliance (iOS): uses only standard encryption exempt (none)

## Release process
- [ ] `VERSION.txt` = 1.0.0, tag `v1.0.0`
- [ ] `./build.sh android-release` with upload keystore env vars → AAB; enable Play App Signing
- [ ] Google Play: internal testing → closed testing → open/production (staged rollout 10% → 50% → 100%), check the pre-launch report
- [ ] iOS: `./build.sh ios` → Xcode archive → TestFlight → App Review (notes: offline game, no login needed)
- [ ] QA_CHECKLIST.md passed on the device matrix; placeholder audit shows 0 (set `BLOCK_PLACEHOLDERS=1` for the final build)
- [ ] Release notes written; save migration test fixture added for the shipped save version

## Post-launch
- Hotfix: patch version bump (1.0.x), same save version unless the format changes (then add a migration + fixture).
- Balance patches: edit CSVs → import → BalanceCli progression → ship.
- New chapters: new rows in chapters/fights/droptables CSVs; saves stay compatible (reconciler adds new heroes/fights).
