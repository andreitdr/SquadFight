# Generated art pipeline

Every sprite in `Assets/_Game/Art/Generated/` is drawn by these scripts as vector art (SVG) and rasterised with
resvg. Nothing is hand-painted, so everything can be regenerated, tweaked or swapped.

## Regenerate

```bash
python3 -m venv .venv-art && .venv-art/bin/pip install -r tools/art/requirements.txt
cd tools/art
../../.venv-art/bin/python gen_characters.py          # heroes + enemies: frames, portraits, icons, card busts (~1 min)
../../.venv-art/bin/python gen_ui.py                  # 9-slices, card frames/backs, gems, stars, logo, icon set
../../.venv-art/bin/python gen_items.py               # one icon per equipment template
../../.venv-art/bin/python gen_backgrounds.py         # home + chapter scenes and banners
../../.venv-art/bin/python gen_cards.py               # baked hero cards + collection sheet (showcase/store)
```

`gen_characters.py hero_knight enemy_lich` renders only those ids; `--portraits` skips the animation frames.
`preview.py out.png hero_knight --px=300 --clips=idle,attack --first` writes a quick contact sheet.

Then in Unity: **Tools/Game/Art/Apply Generated Art** (batch: `-executeMethod Game.EditorTools.Content.GeneratedArt.ApplyBatch`).
It configures the importers, builds one `SpriteAnimSet` per character and fills the art slots on heroes, enemies,
equipment, chapters and the UI `Theme`.

## What is where

| Folder | Content | Used by |
|---|---|---|
| `Characters/<id>/anim/*.png` + `anim.json` | idle(8) attack(6-7) skill(8) hit(3) death(6) victory(6) frames | `HeroDefinition/EnemyDefinition.animations` → battle units |
| `Characters/<id>/portrait.png` | transparent bust, 512 px | hero detail, enemy previews |
| `Characters/<id>/icon.png` | round icon, 256 px | party slots, turn order, dialogue, skill bar |
| `Characters/<id>/card_bust.png` | 3:4 bust, 600x800 | hero cards (`HeroDefinition.cardArt`) |
| `UI/` | panel/button/bar/bubble 9-slices (grayscale, tinted by Theme colors), card frames + backs per rarity, rarity gems, stars, logo, ribbon | `Resources/Theme.asset` |
| `Icons/` | tabs, currency, shard, power, equipment slots, classes, status badges, battle controls | `Theme` |
| `Items/<eq_id>.png` | equipment icons | `EquipmentTemplate.icon` |
| `Backgrounds/` | `home`, `chapter_1..3` (1080x1920) + `_banner` crops | `Theme.appBackground`, `ChapterDefinition.background/banner` |
| `Cards/` | baked hero cards and a collection sheet | showcase, store screenshots (not loaded at runtime) |

## Swapping art

* **Same file name, new picture**: overwrite the PNG/JPG. Unity reimports it; nothing else to do.
* **Different asset**: assign your sprite on the hero/enemy/item/chapter asset or in `Resources/Theme.asset`.
  The Apply tool only overwrites slots that are empty or still hold generated/placeholder art, so your choice sticks.
* **Animations**: replace frames in `Characters/<id>/anim/` (any count; update `anim.json`), or create your own
  `SpriteAnimSet` (Create > Game > Art > Sprite Anim Set) and assign it. Clip names: idle, attack, skill, hit, death, victory.
  `displayScale` = frame size / on-screen body size, `feetY` = feet height inside the frame (0 = bottom).
* **Back to placeholders**: clear the slot; every widget falls back to the old tinted shapes.

## Dialogue

* Battle barks and hero lines: `Assets/_Game/Resources/Localization/strings_dialogue.csv`, keys
  `line_<heroOrEnemyId>_<trigger>_<n>`. Triggers: unlock, select, starup, battle_start, skill, kill, low_hp, ally_down,
  victory, defeat. Add or remove numbered variants freely; add a language column (e.g. `ro`) to translate.
* Story scenes: `Content/Source/story.csv` + `preBeats/postBeats` in `fights.csv`, then **Import CSV Content**.
