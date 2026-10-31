"""Art specs for the 90 generated heroes and the new enemies, derived from the hand-made specs in roster.py.

Each character copies a template of its class/role, rotates the palette (seeded per id), and varies hair, skin, headgear
and weapon within the options the template already supports. Enemies that reuse another enemy's art (`art` in
campaign_design*.py) get no spec: the game tints the base sprites instead.
"""
import colorsys
import copy
import os
import random
import re
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', 'content'))
from roster_design import HEROES as HERO_DESIGN  # noqa: E402
from campaign_design import ACTS  # noqa: E402
from campaign_design_late import ACTS_LATE  # noqa: E402

HEX = re.compile(r'^#[0-9a-fA-F]{6}$')
KEEP = {'skin', 'eye', 'bone', 'gem'}
SKINS = ['#f2c9a0', '#f6d2b0', '#e8c0a0', '#d8a882', '#c08a64', '#a8714e', '#8a5a3c', '#f6dcc4', '#e2b48e']
HAIR = ['short', 'ponytail', 'messy', 'long', 'swept', 'long_wavy']
HAIR_COLORS = ['#7a4a28', '#c0602a', '#e8c070', '#3a2a1e', '#1e1a1a', '#dfe8f5', '#a03a2a', '#5a3a6a', '#d8a050', '#6a6a72']
EYES = ['#7a4a2a', '#3f8a4a', '#3a6ab0', '#4a8a6a', '#6aa86a', '#5a8a3a', '#8a5aa0', '#c07a2a']

HERO_TEMPLATES = {
    'Tank': ['hero_knight', 'hero_paladin'],
    'Warrior': ['hero_rogue', 'hero_dragonknight', 'hero_hunter'],
    'Ranger': ['hero_archer', 'hero_hunter'],
    'Mage': ['hero_apprentice', 'hero_stormcaller'],
    'Support': ['hero_cleric', 'hero_bard'],
}
WEAPONS = {
    'Tank': [('sword', 'shield_round'), ('mace', 'shield_kite'), ('sword', 'shield_kite'), ('mace', 'shield_round')],
    'Warrior': [('dagger', 'dagger'), ('greatsword', None), ('axe', None), ('sword', None), ('club', None)],
    'Ranger': [('bow', None)],
    'Mage': [('staff_orb', None), ('staff_storm', None), ('staff_lantern', None), ('staff_skull', None), ('staff_dragon', None)],
    'Support': [('staff_sun', None), ('lute', None), ('mace', None), ('staff_orb', None)],
}
HEADGEAR = {
    'Tank': ['helm_knight', 'winged_helm', 'horned_helm', None],
    'Warrior': ['hood', 'horned_helm', None, None],
    'Ranger': ['hood', 'feather_cap', None, None],
    'Mage': ['wizard_hat', 'circlet', 'hood_shadow', None],
    'Support': ['veil', 'circlet', 'feather_cap', None],
}

# Enemy template by keyword (first match), else by role.
ENEMY_KEYWORDS = [
    (('wolf', 'hound', 'beast', 'boar', 'stag', 'wendigo', 'crawler', 'maw', 'bat', 'eel', 'scorpion'), 'enemy_wolf'),
    (('dragon', 'wyvern', 'djinn', 'leviathan', 'tide_mother', 'hunger', 'titan'), 'enemy_wyvern'),
    (('skeleton', 'bone', 'husk', 'construct', 'colossus', 'golem', 'turret', 'unit_seven', 'sentinel'), 'enemy_skeleton'),
    (('lich', 'king', 'saint', 'priest', 'sallow', 'hag', 'matron', 'malrec', 'aurem', 'hoarfrost', 'ilvane'), 'enemy_lich'),
    (('giant', 'brute', 'crab', 'jarl', 'thane', 'ogre', 'juggernaut', 'elemental', 'magma', 'tyrant'), 'enemy_orc_brute'),
    (('shaman', 'dryad', 'acolyte', 'doctor', 'mage'), 'enemy_orc_shaman'),
    (('cultist', 'zealot', 'assassin', 'wraith', 'siren', 'echo', 'stalker', 'reaver', 'null'), 'enemy_cultist'),
    (('goblin', 'imp', 'thornling', 'spawn', 'hollowling'), 'enemy_goblin'),
    (('archer', 'gunner'), 'enemy_bone_archer'),
]
ROLE_TEMPLATE = {'grunt': 'enemy_ghoul', 'brute': 'enemy_orc_brute', 'striker': 'enemy_ghoul', 'archer': 'enemy_bone_archer',
                 'caster': 'enemy_cultist', 'healer': 'enemy_orc_shaman', 'swarm': 'enemy_goblin', 'boss': 'enemy_lich'}


def _hsv(c):
    r, g, b = (int(c[i:i + 2], 16) / 255 for i in (1, 3, 5))
    return colorsys.rgb_to_hsv(r, g, b)


def _hex(h, s, v):
    r, g, b = colorsys.hsv_to_rgb(h % 1.0, max(0, min(1, s)), max(0, min(1, v)))
    return '#%02x%02x%02x' % (round(r * 255), round(g * 255), round(b * 255))


def recolor(node, shift, sat, key=None):
    """Rotates every palette colour by `shift` (hue turns) and scales saturation, except skin/eyes/bone/gems."""
    if isinstance(node, dict):
        return {k: recolor(v, shift, sat, k) for k, v in node.items()}
    if isinstance(node, list):
        return [recolor(v, shift, sat, key) for v in node]
    if isinstance(node, str) and HEX.match(node) and key not in KEEP:
        h, s, v = _hsv(node)
        return _hex(h + shift, s * sat, v)
    return node


def toward(color_hex, target_hex):
    """Hue shift that moves color_hex's hue onto target_hex's."""
    return _hsv(target_hex)[0] - _hsv(color_hex)[0]


def hero_specs(templates):
    out = []
    for h in HERO_DESIGN:
        hid = 'hero_' + h['key']
        rng = random.Random(hid)
        t = copy.deepcopy(templates[rng.choice(HERO_TEMPLATES[h['cls']])])
        main = (t.get('torso') or {}).get('color', '#888888')
        S = recolor(t, rng.uniform(0, 1), rng.uniform(0.7, 1.15))
        S['id'] = hid
        S['skin'] = rng.choice(SKINS)
        S['eye'] = rng.choice(EYES)
        hg = rng.choice(HEADGEAR[h['cls']])
        if hg is None:
            S.pop('headgear', None)
            S['hair'] = rng.choice(HAIR)
        else:
            S['headgear'] = hg
            S.setdefault('headgear_color', _hex(rng.random(), 0.3, 0.75))
            S['hair'] = rng.choice(HAIR)
        S['hair_color'] = rng.choice(HAIR_COLORS)
        weapon, off = rng.choice(WEAPONS[h['cls']])
        S['weapon'] = weapon
        if off:
            S['offhand'] = off
        else:
            S.pop('offhand', None)
        if weapon != 'lute':
            S.pop('archetype', None)
        if weapon == 'bow':
            S.setdefault('quiver', _hex(rng.random(), 0.4, 0.45))
            S['rest'] = dict(aF=18, wF=-6)
        elif weapon in ('dagger',) and off == 'dagger':
            S['rest'] = dict(aF=20, wF=40, aB=-20, wB=-40)
        else:
            S.pop('rest', None)
        S.pop('wings', None)  # wings stay unique to Drakon
        S['body'] = rng.choice(['human', 'slim']) if S.get('body') in ('human', 'slim') else S.get('body', 'human')
        for k in ('lashes', 'freckles', 'scar', 'thick_brows', 'ears'):
            S.pop(k, None)
        if rng.random() < 0.2:
            S['freckles'] = True
        if rng.random() < 0.15:
            S['scar'] = True
        if rng.random() < 0.15:
            S['ears'] = 'elf'
        if rng.random() < 0.4:
            S['lashes'] = True
        S['aura'] = _hex(rng.random(), 0.5, 1.0)
        out.append(S)
    return out


def enemy_specs(templates):
    out, bosses = [], set()
    for act in ACTS + ACTS_LATE:
        for e in act['enemies']:
            if e['art']:
                continue
            eid = 'enemy_' + e['key']
            rng = random.Random(eid)
            base = next((t for words, t in ENEMY_KEYWORDS if any(w in e['key'] for w in words)), ROLE_TEMPLATE[e['role']])
            t = copy.deepcopy(templates[base])
            ref = t.get('skin') or (t.get('torso') or {}).get('color') or '#888888'
            S = recolor(t, toward(ref if HEX.match(ref) else '#888888', e['tint']), rng.uniform(0.8, 1.2))
            if 'skin' in S and HEX.match(S['skin']) and base not in ('enemy_skeleton',):
                h, s, v = _hsv(e['tint'])
                S['skin'] = _hex(h, min(0.7, s), max(0.45, v))
            S['id'] = eid
            S['aura'] = e['tint']
            if e['role'] == 'boss':
                S['scale'] = S.get('scale', 1.0) * 1.15
                bosses.add(eid)
            elif e['role'] == 'swarm':
                S['scale'] = S.get('scale', 1.0) * 0.85
            out.append(S)
    return out, bosses


def variants():
    """enemy id -> base enemy id whose sprites it reuses (tinted at runtime)."""
    return {'enemy_' + e['key']: 'enemy_' + e['art'] for act in ACTS + ACTS_LATE for e in act['enemies'] if e['art']}
