# Audio credits

All current sounds are **placeholders**, synthesized from code by `tools/generate_placeholder_audio.py`
(square/triangle/saw/noise oscillators with envelopes). They were created for this project and are released under **CC0 1.0**.
Every clip carries the `placeholder` asset label so the step-18 audit lists it.

| Id | Use |
|----|-----|
| ui_click | button press |
| hit, crit, heal, skill, death | battle |
| battle_start, boss | battle intro / boss wave |
| victory, defeat | battle end jingles |
| reward, unlock, starup, equip, sell | menus |
| music_menu, music_battle, music_boss | looping music |

To replace a sound: drop a file with the same name into `Assets/_Game/Audio/Final/` and run
**Tools/Game/Setup/Build Audio Library** (final files win over placeholders; the placeholder label is not applied).
