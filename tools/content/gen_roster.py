"""Writes the 90 designed heroes (roster_design.py) into heroes.csv, skills.csv and passives.csv.

    python3 tools/content/gen_roster.py            # regenerate (idempotent)
    python3 tools/content/gen_roster.py --report   # also print the rarity table

Rows above the GENERATED sections of each CSV are hand-written (the 10 original heroes) and are kept as they are.
Rarities are rolled per hero with a fixed seed (owner request: random, but meaningful), under the rule that every
class has strictly fewer heroes of each higher rarity (Common > Uncommon > Rare > Epic > Legendary >= 1).
The 10 original heroes keep their rarities and count toward their class.
"""
import colorsys
import os
import random
import sys
from collections import Counter

sys.path.insert(0, os.path.dirname(__file__))
import csvsections  # noqa: E402
from roster_design import HEROES  # noqa: E402

SEED = 100

RARITIES = ['Common', 'Uncommon', 'Rare', 'Epic', 'Legendary']
WEIGHTS = [32, 27, 21, 13, 7]
CLASSES = ['Tank', 'Warrior', 'Ranger', 'Mage', 'Support']

# Common-rarity stat template per class (from the original heroes); rarity and lean scale it.
BASE = {
    'Tank':    dict(maxHp=600, attack=40, defense=30, speed=90, critChance=0.05, critDamage=1.5),
    'Warrior': dict(maxHp=430, attack=62, defense=15, speed=115, critChance=0.15, critDamage=1.7),
    'Ranger':  dict(maxHp=410, attack=56, defense=12, speed=110, critChance=0.15, critDamage=1.5),
    'Mage':    dict(maxHp=380, attack=60, defense=10, speed=100, critChance=0.08, critDamage=1.5),
    'Support': dict(maxHp=440, attack=44, defense=17, speed=105, critChance=0.05, critDamage=1.5),
}
RARITY_MULT = {'Common': 1.0, 'Uncommon': 1.12, 'Rare': 1.33, 'Epic': 1.6, 'Legendary': 1.95}
LEANS = {
    'balanced': {},
    'bulky':    dict(maxHp=1.15, attack=0.92),
    'fierce':   dict(attack=1.12, maxHp=0.9),
    'swift':    dict(speed=1.08, maxHp=0.95),
    'keen':     dict(critChance=0.05, critDamage=0.1, attack=0.95),
    'stalwart': dict(defense=1.18, speed=0.95),
}
BASICS = {
    'Tank': ['skill_slash', 'skill_bash'],
    'Warrior': ['skill_stab', 'skill_cleave', 'skill_rend'],
    'Ranger': ['skill_shot', 'skill_twin_shot'],
    'Mage': ['skill_bolt', 'skill_spark'],
    'Support': ['skill_smite', 'skill_chord', 'skill_mend_strike'],
}
# New basic attacks used by the generated heroes (appended to skills.csv).
NEW_BASICS = [
    ('skill_bash', 'Shield Rap', 'A blow with the shield rim.', 'SingleEnemy', 1, 'Damage:0.9'),
    ('skill_rend', 'Rend', 'A tearing strike.', 'SingleEnemy', 1, 'Damage:1.05'),
    ('skill_twin_shot', 'Twin Shot', 'Two quick arrows at random enemies.', 'RandomEnemies', 2, 'Damage:0.55'),
    ('skill_spark', 'Spark', 'A crackle of magic at the weakest enemy.', 'LowestHpEnemy', 1, 'Damage:0.95'),
    ('skill_mend_strike', 'Mending Strike', 'A light blow that also heals the caster a little.', 'SingleEnemy', 1, 'Damage:0.7|self>Heal:0.2'),
]


def slug(s):
    out = ''.join(c.lower() if c.isalnum() else '_' for c in s)
    while '__' in out:
        out = out.replace('__', '_')
    return out.strip('_')


def hand_rows(name):
    head, _ = csvsections.read(name)
    rows = csvsections.rows(head)
    return rows[0], rows[1:]


def strictly_decreasing(counts):
    c = [counts.get(r, 0) for r in RARITIES]
    return all(c[i] > c[i + 1] for i in range(4)) and c[4] >= 1


def roll_rarities(rng, existing):
    """existing: class -> Counter of the hand-written heroes' rarities. Returns key -> rarity."""
    result = {}
    for cls in CLASSES:
        keys = [h['key'] for h in HEROES if h['cls'] == cls]
        for attempt in range(10000):
            draw = rng.choices(RARITIES, WEIGHTS, k=len(keys))
            if strictly_decreasing(existing[cls] + Counter(draw)):
                break
        else:
            raise SystemExit(f'could not roll a meaningful rarity spread for {cls}')
        for k, r in zip(keys, draw):
            result[k] = r
    return result


def stats(cls, rarity, lean):
    b = dict(BASE[cls])
    m = RARITY_MULT[rarity]
    tier = RARITIES.index(rarity)
    l = LEANS[lean]
    hp = round(b['maxHp'] * m * l.get('maxHp', 1) / 10) * 10
    atk = round(b['attack'] * m * l.get('attack', 1))
    df = round(b['defense'] * m * l.get('defense', 1))
    spd = round(b['speed'] * (1 + 0.015 * tier) * l.get('speed', 1))
    cc = round(b['critChance'] + 0.01 * tier + l.get('critChance', 0), 3)
    cd = round(b['critDamage'] + 0.05 * max(0, tier - 1) + l.get('critDamage', 0), 3)
    return [str(hp), str(atk), str(df), str(spd), f'{cc:g}', f'{cd:g}']


def tint(rng, cls):
    hue = {'Tank': 0.6, 'Warrior': 0.0, 'Ranger': 0.33, 'Mage': 0.75, 'Support': 0.13}[cls]
    h = (hue + rng.uniform(-0.12, 0.12)) % 1.0
    r, g, b = colorsys.hsv_to_rgb(h, rng.uniform(0.35, 0.75), rng.uniform(0.6, 0.95))
    return '#%02X%02X%02X' % (int(r * 255), int(g * 255), int(b * 255))


def main():
    rng = random.Random(SEED)
    header, hand = hand_rows('heroes.csv')
    col = {c: i for i, c in enumerate(header)}
    existing = {c: Counter() for c in CLASSES}
    for r in hand:
        existing[r[col['class']]][r[col['rarity']]] += 1

    keys = [h['key'] for h in HEROES]
    assert len(keys) == len(set(keys)), 'duplicate hero key'
    rarity = roll_rarities(rng, existing)

    hero_rows, skill_rows, passive_rows = [], [], []
    used = set()
    for h in HEROES:
        hid = 'hero_' + h['key']
        a, p = h['active'], h['passive']
        sid = 'skill_' + slug(a['name'])
        pid = 'passive_' + slug(p['name'])
        for x in (hid, sid, pid):
            assert x not in used, f'duplicate id {x}'
            used.add(x)
        lean = rng.choice(sorted(LEANS))
        basic = rng.choice(BASICS[h['cls']])
        row = {'id': hid, 'name': h['name'], 'rarity': rarity[h['key']], 'class': h['cls'], 'origin': f"act_{h['act']}",
               'basicSkill': basic, 'activeSkill': sid, 'passive': pid, 'tint': tint(rng, h['cls']), 'lore': h['lore']}
        for c, v in zip(['maxHp', 'attack', 'defense', 'speed', 'critChance', 'critDamage'], stats(h['cls'], rarity[h['key']], lean)):
            row[c] = v
        hero_rows.append([row[c] for c in header])
        skill_rows.append([sid, a['name'], a['desc'], a['targeting'], str(a['count']), str(a['cooldown']), str(a['initial']), a['effects']])
        passive_rows.append([pid, p['name'], p['desc'], p['trigger'], f"{p['threshold']:g}" if p['threshold'] else '', f"{p['chance']:g}" if p['chance'] else '',
                             str(p['cooldown']), 'true' if p['once'] else 'false', p['targeting'], str(p['count']), p['effects']])

    csvsections.update('heroes.csv', 'gen_roster', hero_rows)
    csvsections.update('skills.csv', 'gen_roster', [[i, n, d, t, str(c), '0', '0', e] for i, n, d, t, c, e in NEW_BASICS] + skill_rows)
    csvsections.update('passives.csv', 'gen_roster', passive_rows)

    total = {c: existing[c] + Counter(rarity[h['key']] for h in HEROES if h['cls'] == c) for c in CLASSES}
    lines = ['| Class | ' + ' | '.join(RARITIES) + ' | Total |', '|---|' + '---|' * 6]
    for c in CLASSES:
        lines.append(f'| {c} | ' + ' | '.join(str(total[c][r]) for r in RARITIES) + f' | {sum(total[c].values())} |')
    all_ = sum(total.values(), Counter())
    lines.append('| **All** | ' + ' | '.join(str(all_[r]) for r in RARITIES) + f' | {sum(all_.values())} |')
    print(f'{len(HEROES)} generated heroes (seed {SEED}); rarity per class (incl. the 10 original heroes):')
    print('\n'.join(lines))
    if '--report' in sys.argv:
        for h in HEROES:
            print(f"  {h['cls']:8} {rarity[h['key']]:10} act_{h['act']} {h['name']}")


if __name__ == '__main__':
    main()
