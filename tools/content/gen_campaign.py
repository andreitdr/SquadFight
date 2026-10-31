"""Builds the 40-chapter campaign from campaign_design*.py and roster_design.py.

    python3 tools/content/gen_campaign.py          # regenerate (idempotent)

Writes its GENERATED section of enemies/skills/passives/equipment/droptables/chapters/fights/story.csv, and adjusts the
hand-written head rows of chapters 1-3 in a fixed, repeatable way:
  * fights.csv: adds the `kind` column, renumbers the boss of chapters 1-3 so the new fights sit before it;
  * droptables.csv: limits the act-1 random shard drops to the act_1 pool and the act-1 random gear to tier 1;
  * equipment.csv: adds the `tier` column (the original 24 items are tier 1);
  * enemies.csv: adds the `passive` column;
  * story.csv: rewrites the chapter 1-3 beats in the darker tone (same ids).
  * enemies.csv: adds the `passive` column; `rank` replaces `isBoss` (bosses stay Boss, the others get the rank their
    skill count gives) and `extraSkills`/`specials` hold the boss kits of bestiary_design.py;
  * skills.csv: adds the `threshold` column (boss specials).
Also writes levels.csv (player levels) and the level-up drop tables.
Waves are rank-aware: Elites from a chapter's middle on, Champions in its last third and in side quests, and a higher
rank always has more HP than a lower rank in the same wave (the validator checks this too).
Enemy multipliers are first guesses: `BalanceCli tune` refits them, `BalanceCli recommend` fills recommendedPower.
"""
import os
import random
import sys

sys.path.insert(0, os.path.dirname(__file__))
import csvsections as cs  # noqa: E402
from campaign_design import ACTS, FIGHTS_PER_CHAPTER, SPEAKERS  # noqa: E402
from campaign_design_late import ACTS_LATE  # noqa: E402
from roster_design import HEROES as NEW_HEROES  # noqa: E402
from bestiary_design import BOSS_KITS, NEW_ENEMIES, NEW_FOES  # noqa: E402

KEY = 'gen_campaign'
ALL_ACTS = ACTS + ACTS_LATE
RNG = random.Random(4040)

# Enemy base stats per role at the start of act II (~the end of chapter 3); acts scale them (defense scales slower).
ROLE = {
    'grunt':   (950, 105, 35, 95, 0.05, 1.5),
    'brute':   (1600, 135, 70, 86, 0.05, 1.5),
    'striker': (850, 125, 25, 122, 0.12, 1.6),
    'archer':  (800, 120, 25, 108, 0.10, 1.5),
    'caster':  (760, 110, 22, 100, 0.05, 1.5),
    'healer':  (820, 85, 28, 98, 0.05, 1.5),
    'swarm':   (560, 80, 14, 118, 0.05, 1.5),
    'boss':    (6000, 150, 60, 96, 0.10, 1.5),
}
ACT_SCALE = {1: 0.8, 2: 1.0, 3: 1.32, 4: 1.74, 5: 2.3, 6: 3.0, 7: 3.95, 8: 5.2}
TIER_GROWTH = 1.38
GEAR_WEIGHTS = {
    1: 'Common=70;Uncommon=26;Rare=4', 2: 'Common=45;Uncommon=40;Rare=13;Epic=2', 3: 'Common=30;Uncommon=45;Rare=20;Epic=5',
    4: 'Common=20;Uncommon=40;Rare=30;Epic=9;Legendary=1', 5: 'Common=10;Uncommon=35;Rare=38;Epic=15;Legendary=2',
    6: 'Common=5;Uncommon=25;Rare=45;Epic=21;Legendary=4', 7: 'Uncommon=20;Rare=45;Epic=29;Legendary=6', 8: 'Uncommon=10;Rare=40;Epic=40;Legendary=10',
}
SIDE_WEIGHTS = {a: GEAR_WEIGHTS[min(8, a + 2)] for a in range(1, 9)}
SHARD_WEIGHTS = [('Common', 40, 2, 3), ('Uncommon', 30, 2, 3), ('Rare', 17, 1, 3), ('Epic', 9, 1, 2), ('Legendary', 4, 1, 2)]
UNLOCK = {'Common': 10, 'Uncommon': 20, 'Rare': 40, 'Epic': 80, 'Legendary': 160}
ITEM_ONLY_SHARE = 0.3
RANKS = ['Minion', 'Soldier', 'Elite', 'Champion', 'Boss']
SKILL_RANK = {0: 'Minion', 1: 'Soldier', 2: 'Elite', 3: 'Champion'}
# Player levels: total XP to reach level L = LEVEL_XP_A x (L - 1) ^ LEVEL_XP_POW. A casual run earns ~650 XP per chapter
# (`BalanceCli levels`), so it ends near level 50; levels 51-60 are for players who keep fighting.
MAX_LEVEL = 60
LEVEL_XP_A, LEVEL_XP_POW = 164, 1.3
# Level-up rewards by the act a casual player is in at that level (first level reached inside each act).
LEVEL_ACT_FROM = {1: 1, 2: 11, 3: 19, 4: 25, 5: 31, 6: 37, 7: 42, 8: 47}
# Heroes met before their act (they appear in an earlier chapter's story): hero id -> chapter of recruitment.
RECRUIT_CHAPTER = {'hero_aldous': 28, 'hero_lisbet': 32, 'hero_sunniva': 34}

# Darker rewrite of the original chapter 1-3 beats (ids unchanged).
STORY_OVERRIDES = {
    'beat_ch1_intro': 'Goblins have been raiding the Greenwood road, and the villages have started burying their dead in the fields. Three young heroes set out to stop them.',
    'beat_ch1_f1_pre': 'Stay behind me. Whatever comes out of those trees, it does not get past me.',
    'beat_ch1_f4_pre': 'Wolves and goblins hunting together. Something taught them. Something that hates us.',
    'beat_ch1_boss_pre': 'Who dares enter MY forest? The pale man promised me this road. Crush them!',
    'beat_ch1_end': 'The Goblin King falls, still clutching a black candle. He was only taking orders. A trail of black candles leads north, to the old crypt.',
    'beat_ch2_intro': 'Beneath the hills lies the Crypt of Ash, where the Wardens buried their own. It was sealed for three hundred years. The seal was broken from the inside.',
    'beat_ch2_f1_pre': "I read about this place. Mostly the part that said 'never go in'. It was underlined three times.",
    'beat_ch2_f5_pre': 'These cultists are not raising the dead for fun. They are building an army, and they are not even hiding it.',
    'beat_ch2_end': "With its last breath the Lich laughs. The army was never his. 'Go to the Peak,' it whispers. 'Kill the dragon. He would so like you to.'",
    'beat_ch3_intro': 'Dragon\'s Peak burns on the horizon. Orc warbands march under a crimson banner, and every village between here and the mountain is ash.',
    'beat_ch3_f1_pre': 'Orcs, wyverns, and a dragon at the top. Whoever planned this wanted us exhausted when we get there.',
    'beat_ch3_f5_pre': 'The priests are feeding the dragon\'s fire with something black. Listen. She is screaming in there.',
    'beat_ch3_boss_pre': 'Little heroes. Please. I am not what you were told. I am holding the door. Stop. STOP.',
    'beat_ch3_end': 'The Red Dragon falls, and the mountain goes quiet. Then, far below, something that had been locked for three hundred years begins, very slowly, to breathe.',
}
GEAR_NOUNS = [('Weapon', 'Blade', 'a'), ('Weapon', 'Waraxe', 'b'), ('Helmet', 'Helm', ''), ('Chest', 'Hauberk', ''), ('Gloves', 'Gauntlets', ''),
              ('Boots', 'Greaves', ''), ('Ring', 'Band', 'a'), ('Ring', 'Signet', 'b')]


def slug(s):
    out = ''.join(c.lower() if c.isalnum() else '_' for c in s)
    while '__' in out:
        out = out.replace('__', '_')
    return out.strip('_')


def fmt(x):
    return f'{x:.2f}'


# ------------------------------------------------------------------------------------------------ inputs

def chapters():
    """Yields (n, act, position in act, chapter design)."""
    n = 0
    for act in ALL_ACTS:
        for p, ch in enumerate(act['chapters']):
            n += 1
            yield n, act, p, ch


def hero_table():
    head, sections = cs.read('heroes.csv')
    rows = cs.rows(head)
    header = rows[0]
    all_rows = rows[1:] + cs.rows(sections.get('gen_roster', ''))
    col = {c: i for i, c in enumerate(header)}
    return {r[col['id']]: dict(rarity=r[col['rarity']], origin=r[col['origin']], name=r[col['name']]) for r in all_rows}


def strings_of_head(name):
    head, sections = cs.read(name)
    lines = head.rstrip('\n').split('\n')
    comments = [l for l in lines if l.startswith('#')]
    rows = cs.rows(head)
    return comments, rows[0], rows[1:], sections


def head_text(comments, header, rows):
    return '\n'.join(comments + [cs.to_text([header] + rows)]) + '\n'


# ------------------------------------------------------------------------------------------------ enemies

def skill_row(sid, x, initial='1', threshold=''):
    return [sid, x['name'], x['desc'], x['targeting'], str(x['count']), str(x.get('cooldown', 0)), initial, x['effects'], threshold]


def passive_row(pid, x):
    return [pid, x['name'], x['desc'], x['trigger'], f"{x['threshold']:g}" if x['threshold'] else '', f"{x['chance']:g}" if x['chance'] else '',
            str(x['cooldown']), 'true' if x['once'] else 'false', x['targeting'], str(x['count']), x['effects']]


def boss_kit(key, skills, passives):
    """Adds a boss's extra active, specials (and passive, if the kit has one); returns (passive id, extra ids, special ids)."""
    kit = BOSS_KITS[key]
    extra = 'skill_x_' + key
    skills.append(skill_row(extra, kit['extra'], initial='2'))
    specials = []
    for n, sp in enumerate(kit['specials'], 1):
        sid = f'special_{key}_{n}'
        skills.append(skill_row(sid, dict(sp, cooldown=0), initial='0', threshold=f"{sp['threshold']:g}"))
        specials.append(sid)
    passive = ''
    if kit['passive']:
        passive = 'passive_e_' + key
        passives.append(passive_row(passive, kit['passive']))
    return passive, [extra], specials


def enemy_rows(existing_ids):
    """Rows for every designed enemy (campaign + new bestiary) and their skills; bosses get their kits."""
    rows, skills, passives = [], [], []
    for act in ALL_ACTS:
        a = act['num']
        for e in act['enemies'] + NEW_ENEMIES.get(a, []):
            eid = 'enemy_' + e['key']
            assert eid not in existing_ids, eid
            hp, atk, df, spd, cc, cd = ROLE[e['role']]
            s = ACT_SCALE[a] * e['scale']
            basic = 'skill_enemy_bite' if e['role'] in ('striker', 'swarm') else 'skill_enemy_hit'
            active = passive = ''
            extras, specials = [], []
            if e['active']:
                active = 'skill_e_' + e['key']
                skills.append(skill_row(active, e['active']))
            if e.get('extra'):
                extras.append('skill_x_' + e['key'])
                skills.append(skill_row(extras[0], e['extra'], initial='2'))
            if e['passive']:
                passive = 'passive_e_' + e['key']
                passives.append(passive_row(passive, e['passive']))
            if e['role'] == 'boss':
                rank = 'Boss'
                kit_passive, extras, specials = boss_kit(e['key'], skills, passives)
                passive = passive or kit_passive
            else:
                rank = e.get('rank') or SKILL_RANK[bool(active) + bool(passive) + len(extras)]
                assert rank == SKILL_RANK[bool(active) + bool(passive) + len(extras)], f'{eid}: {rank} with the wrong skill count'
            rows.append([eid, e['name'], rank, str(round(hp * s / 10) * 10), str(round(atk * s)), str(round(df * s ** 0.6)),
                         str(spd), f'{cc:g}', f'{cd:g}', basic, active, passive, ';'.join(extras), ';'.join(specials), e['tint'], 'enemy_' + e['art'] if e['art'] else ''])
    return rows, skills, passives


# ------------------------------------------------------------------------------------------------ equipment

def equipment_rows():
    rows = []
    for act in ALL_ACTS:
        t = act['num']
        if t == 1:
            continue
        f = TIER_GROWTH ** (t - 1)
        tn = act['tier_name']
        for slot, noun, v in GEAR_NOUNS:
            if slot == 'Weapon':
                stats = f'Attack={round(28 * f)};CritChance={0.02 + 0.004 * t:.3f}' if v == 'a' else f'Attack={round(24 * f)};CritDamage={0.08 + 0.015 * t:.3f}'
            elif slot == 'Helmet':
                stats = f'MaxHp={round(110 * f)};Defense={round(10 * f ** 0.8)}'
            elif slot == 'Chest':
                stats = f'MaxHp={round(170 * f)};Defense={round(16 * f ** 0.8)}'
            elif slot == 'Gloves':
                stats = f'Attack={round(12 * f)};CritChance={0.02 + 0.003 * t:.3f}'
            elif slot == 'Boots':
                stats = f'Speed={round(8 + 1.5 * t)};MaxHp={round(50 * f)}'
            else:
                stats = f'Attack={8 + t:.0f}%' if v == 'a' else f'MaxHp={6 + 0.8 * t:.1f}%;CritDamage={0.05 + 0.012 * t:.3f}'
            rows.append([f'eq_t{t}_{slug(noun)}', f'{tn} {noun}', slot, 'Common', str(t), stats, ''])
    return rows


def relic_row(name, slot, act):
    """A unique side-quest item: about 30% above its act's gear, with one extra stat."""
    t = act
    f = TIER_GROWTH ** (t - 1) * 1.3
    extra = {'Weapon': f'Attack={round(30 * f)};CritChance=0.05;CritDamage=0.15', 'Helmet': f'MaxHp={round(120 * f)};Defense={round(11 * f ** 0.8)};Attack=4%',
             'Chest': f'MaxHp={round(185 * f)};Defense={round(17 * f ** 0.8)};MaxHp=5%', 'Gloves': f'Attack={round(13 * f)};CritChance=0.04;Speed=3',
             'Boots': f'Speed={round(10 + 1.6 * t)};MaxHp={round(55 * f)};Defense={round(5 * f ** 0.8)}', 'Ring': f'Attack={9 + t:.0f}%;CritChance=0.03'}[slot]
    return ['eq_relic_' + slug(name), name, slot, 'Epic' if act <= 3 else 'Legendary', '0', extra, '']


# ------------------------------------------------------------------------------------------------ waves

def wave_text(spawns):
    return '+'.join(f'enemy_{k}@{fmt(m)}' for k, m in spawns)


# key -> (rank, HP in battle before the fight multiplier); filled in main() from enemies.csv and ranks.csv.
INFO = {}


def pick(foes, n, ranks=None):
    """n weighted picks among foes of the given ranks (the first listed foes are the chapter's staples)."""
    pool = [k for k in foes if ranks is None or INFO[k][0] in ranks]
    if not pool:
        return []
    weights = [len(pool) - i + 1 for i in range(len(pool))]
    return [RNG.choices(pool, weights)[0] for _ in range(n)]


def ordered(wave, foes):
    """Makes every higher rank in the wave have more HP than every lower rank: an offending lower-rank enemy is swapped
    for a same-or-lower-rank foe that fits, or dropped (the wave keeps at least one enemy)."""
    def ok(k, others):
        r, hp = INFO[k]
        return all((INFO[o][1] > hp if RANKS.index(INFO[o][0]) > RANKS.index(r) else INFO[o][1] < hp if RANKS.index(INFO[o][0]) < RANKS.index(r) else True) for o in others)
    out = sorted(wave, key=lambda k: -RANKS.index(INFO[k][0]))  # place high ranks first
    kept = []
    for k in out:
        if ok(k, kept):
            kept.append(k)
            continue
        r = RANKS.index(INFO[k][0])
        fits = [f for f in foes if RANKS.index(INFO[f][0]) <= r and ok(f, kept)]
        same = [f for f in fits if INFO[f][0] == INFO[k][0]]
        if fits:
            kept.append(same[0] if same else max(fits, key=lambda f: RANKS.index(INFO[f][0])))
    order = {k: i for i, k in enumerate(wave)}
    return sorted(kept, key=lambda k: order.get(k, len(wave))) or wave[:1]


def compose(foes, n, frac, harder):
    """One wave of n enemies: fodder (Minion/Soldier), Elites from the chapter's middle on, a Champion near its end."""
    champs = 1 if (harder or frac >= 0.65) and n >= 3 and any(INFO[k][0] == 'Champion' for k in foes) and RNG.random() < (0.8 if harder else 0.5) else 0
    elites = (1 if frac >= 0.3 or harder else 0) + (1 if frac >= 0.75 and n >= 4 else 0)
    wave = pick(foes, champs, {'Champion'})
    wave += pick(foes, min(elites, n - len(wave) - 1 if n > 1 else 0), {'Elite'})
    wave += pick(foes, n - len(wave), {'Minion', 'Soldier'}) or pick(foes, n - len(wave))
    return ordered(wave, foes)


def build_waves(foes, i, count, mult, boss=None, harder=False):
    frac = i / max(1, count - 1)
    if boss:
        first = compose(foes, 3, frac, harder)
        adds = pick(foes, 1 if frac < 1 or not harder else 2, {'Minion', 'Soldier'}) or pick(foes, 1)
        return wave_text([(k, mult) for k in first]) + ' / ' + wave_text([(k, mult) for k in ordered([boss] + adds, foes + [boss])])
    size = min(5, 2 + round(frac * 3) + (1 if harder else 0))
    if frac < 0.4 and not harder:
        return wave_text([(k, mult) for k in compose(foes, size, frac, harder)])
    return wave_text([(k, mult) for k in compose(foes, max(2, size - 1), frac, harder)]) + ' / ' + wave_text([(k, mult) for k in compose(foes, max(2, size - 2), frac, harder)])


def multiplier(p, frac):
    return 1.0 + 0.06 * p + 0.08 * frac


# ------------------------------------------------------------------------------------------------ main

def main():
    rolled = random.Random(40)
    rolled_fights = [rolled.randint(9, 11) for _ in range(40)]
    assert rolled_fights == FIGHTS_PER_CHAPTER, 'FIGHTS_PER_CHAPTER no longer matches the seeded roll'
    heroes = hero_table()
    new_by_id = {'hero_' + h['key']: h for h in NEW_HEROES}

    # ---- enemies (+ their skills/passives)
    e_comments, e_header, e_rows, _ = strings_of_head('enemies.csv')
    if 'passive' not in e_header:
        i = e_header.index('activeSkill') + 1
        e_header.insert(i, 'passive')
        for r in e_rows:
            r.insert(i, '')
    if 'art' not in e_header:
        e_header.append('art')
        for r in e_rows:
            r.append('')
    if 'isBoss' in e_header:
        e_header[e_header.index('isBoss')] = 'rank'
    if 'extraSkills' not in e_header:
        i = e_header.index('passive') + 1
        e_header[i:i] = ['extraSkills', 'specials']
        for r in e_rows:
            r[i:i] = ['', '']
    ec = {c: i for i, c in enumerate(e_header)}
    existing_enemies = {r[0] for r in e_rows}
    gen_enemies, gen_skills, gen_passives = enemy_rows(existing_enemies)
    # Hand-written enemies (chapters 1-3): bosses get their kits, the others the rank of their skill count.
    for r in e_rows:
        key = r[0][len('enemy_'):]
        if r[ec['rank']] in ('true', 'Boss'):
            r[ec['rank']] = 'Boss'
            kit_passive, extras, specials = boss_kit(key, gen_skills, gen_passives)
            r[ec['passive']] = r[ec['passive']] or kit_passive
            r[ec['extraSkills']], r[ec['specials']] = ';'.join(extras), ';'.join(specials)
        else:
            r[ec['rank']] = SKILL_RANK[bool(r[ec['activeSkill']]) + bool(r[ec['passive']]) + len([x for x in r[ec['extraSkills']].split(';') if x])]
    ranks = {r[0]: float(r[1]) for r in cs.rows(cs.read('ranks.csv')[0])[1:]}
    for r in e_rows + gen_enemies:
        INFO[r[0][len('enemy_'):]] = (r[ec['rank']], float(r[ec['maxHp']]) * ranks[r[ec['rank']]])
    enemy_ids = existing_enemies | {r[0] for r in gen_enemies}
    _, e_sections = cs.read('enemies.csv')
    e_sections[KEY] = cs.to_text(gen_enemies)
    cs.write('enemies.csv', head_text(e_comments, e_header, e_rows), e_sections)
    k_comments, k_header, k_rows, k_sections = strings_of_head('skills.csv')
    if 'threshold' not in k_header:
        k_header.append('threshold')
    k_sections[KEY] = cs.to_text(gen_skills)
    cs.write('skills.csv', head_text(k_comments, k_header, k_rows), k_sections)
    cs.update('passives.csv', KEY, gen_passives)

    # ---- equipment: tier column, tiers 2-8, relics
    q_comments, q_header, q_rows, q_sections = strings_of_head('equipment.csv')
    if 'tier' not in q_header:
        i = q_header.index('rarity') + 1
        q_header.insert(i, 'tier')
        for r in q_rows:
            r.insert(i, '1')
    equipment = equipment_rows()

    # ---- drop tables (hand rows: pools for act 1)
    d_comments, d_header, d_rows, d_sections = strings_of_head('droptables.csv')
    dc = {c: i for i, c in enumerate(d_header)}
    for r in d_rows:
        if r[dc['table']].startswith(('drops_ch', 'first_ch', 'clear_ch')):
            if r[dc['type']] == 'RandomHeroShardByRarity':
                r[dc['refId']] = 'act_1'
            if r[dc['type']] == 'RandomEquipment':
                r[dc['refId']] = '1'
    drops = []

    def D(table, group, type_, ref='', rarity='', weights='', weight=1, lo=1, hi=None, rolls=None):
        mn, mx = rolls if rolls else ('', '')
        drops.append([table, group, str(mn), str(mx), 'false' if rolls else '', type_, ref, rarity, '', weights, str(weight), str(lo), str(hi if hi is not None else lo)])

    def gold(a, base):
        return round(base * (1 + 0.6 * (a - 1)))

    for act in ALL_ACTS:
        a = act['num']
        pool = '+'.join(f'act_{k}' for k in range(1, a + 1))
        if a > 1:
            t = f'drops_a{a}'
            D(t, 'G', 'Currency', 'gold', lo=gold(a, 30), hi=gold(a, 45))
            for k, (rar, w, lo, hi) in enumerate(SHARD_WEIGHTS):
                D(t, 'shards', 'RandomHeroShardByRarity', pool, rar, weight=w, lo=lo, hi=hi, rolls=(1, 1) if k == 0 else None)
            D(t, 'gear', 'RandomEquipment', str(a), weights=GEAR_WEIGHTS[a], weight=60, rolls=(1, 1))
            D(t, 'gear', 'Currency', 'gold', weight=40, lo=gold(a, 15), hi=gold(a, 30))
        t = f'items_a{a}'
        D(t, 'G', 'Currency', 'gold', lo=gold(a, 35), hi=gold(a, 55))
        D(t, 'gear', 'RandomEquipment', str(a), weights=GEAR_WEIGHTS[a], weight=1, rolls=(1, 2))
        t = f'side_a{a}'
        D(t, 'G', 'Currency', 'gold', lo=gold(a, 60), hi=gold(a, 90))
        D(t, 'gear', 'RandomEquipment', str(a), weights=SIDE_WEIGHTS[a], weight=1, rolls=(1, 2))
        t = f'fc_a{a}'
        D(t, 'G', 'Currency', 'gold', lo=gold(a, 80))
        D(t, 'G', 'RandomEquipment', str(a), weights=GEAR_WEIGHTS[min(8, a + 1)])

    # ---- player levels: levels.csv + reward tables per act band (gold and a random shard of the act's pool; every 5th
    # level also gear)
    level_rows = []
    for lv in range(1, MAX_LEVEL + 1):
        a = max(k for k, first in LEVEL_ACT_FROM.items() if lv >= first)
        xp = 0 if lv == MAX_LEVEL else int(round(LEVEL_XP_A * (lv ** LEVEL_XP_POW - (lv - 1) ** LEVEL_XP_POW) / 5) * 5)
        level_rows.append([str(lv), str(xp), '' if lv == 1 else f'level_a{a}_gear' if lv % 5 == 0 else f'level_a{a}'])
    for a in range(1, 9):
        pool = '+'.join(f'act_{k}' for k in range(1, a + 1))
        for t, g in ((f'level_a{a}', 80), (f'level_a{a}_gear', 150)):
            D(t, 'G', 'Currency', 'gold', lo=gold(a, g))
            for k, (rar, w, lo, hi) in enumerate(SHARD_WEIGHTS):
                D(t, 'shards', 'RandomHeroShardByRarity', pool, rar, weight=w, lo=lo, hi=hi, rolls=(1, 1) if k == 0 else None)
        D(f'level_a{a}_gear', 'G', 'RandomEquipment', str(a), weights=GEAR_WEIGHTS[min(8, a + 1)])
    with open(os.path.join(cs.SRC, 'levels.csv'), 'w', newline='') as f:
        f.write('# Player levels. GENERATED by tools/content/gen_campaign.py (MAX_LEVEL, LEVEL_XP_*, LEVEL_ACT_FROM): edit there and re-run.\n'
                '# xpToNext = XP from this level to the next (0 on the last level = the cap); reward = drop table rolled on reaching the level.\n')
        f.write(cs.to_text([['level', 'xpToNext', 'reward']] + level_rows) + '\n')

    # ---- hero placement: which fights hand out each hero's targeted shards (3 grants of 20% each)
    act_fights = {}  # act -> list of (n, fight index) for non-boss main fights of chapters >= 4 (and new ones of 1-3)
    grants = {}  # (n, i) -> list of (hero, shards)
    recruit_at = {}  # hero -> (n, i)
    plan = list(chapters())
    for act in ALL_ACTS:
        a = act['num']
        members = [hid for hid, h in new_by_id.items() if h['act'] == a and hid not in RECRUIT_CHAPTER]
        members += [hid for hid, n in RECRUIT_CHAPTER.items() if any(p[0] == n and p[1]['num'] == a for p in plan)]
        chs = [(n, ch) for n, ac, p, ch in plan if ac['num'] == a and n >= 4]
        if not chs:
            continue

        def first_speech(hid):
            if hid in RECRUIT_CHAPTER:
                return RECRUIT_CHAPTER[hid]
            for n, ch in chs:
                lines = [s for s, _ in ch['beats']] + [s for s, _ in (ch['intro'] or [])] + [q['pre'][0] for q in ch['sides']]
                if hid in lines:
                    return n
            return None

        load = {n: 0 for n, _ in chs}
        for hid in sorted(members, key=lambda h: (first_speech(h) is None, first_speech(h) or 0, h)):
            n = first_speech(hid) or min(load, key=lambda k: (load[k], k))
            count = FIGHTS_PER_CHAPTER[n - 1]
            slots = list(range(1, max(2, int(count * 0.4) + 1)))
            i = slots[load[n] % len(slots)]
            load[n] += 1
            recruit_at[hid] = (n, i)
            share = max(1, round(UNLOCK[heroes[hid]['rarity']] * 0.2))
            grants.setdefault((n, i), []).append((hid, share))
            later = [(m, j) for m, _ in chs for j in range(FIGHTS_PER_CHAPTER[m - 1] - 1) if (m, j) > (n, i)]
            for g in range(2):
                if later:
                    spot = later[min(len(later) - 1, (len(later) * (g + 1)) // 3)]
                    grants.setdefault(spot, []).append((hid, share))
                else:
                    grants.setdefault((n, i), []).append((hid, share))

    # ---- chapters, fights, story
    c_comments, c_header, c_rows, c_sections = strings_of_head('chapters.csv')
    f_comments, f_header, f_rows, f_sections = strings_of_head('fights.csv')
    s_comments, s_header, s_rows, s_sections = strings_of_head('story.csv')
    if 'kind' not in f_header:
        f_header.append('kind')
        for r in f_rows:
            r.append('Main')
    fc = {c: i for i, c in enumerate(f_header)}
    sc = {c: i for i, c in enumerate(s_header)}
    for r in s_rows:
        if r[sc['id']] in STORY_OVERRIDES:
            r[sc['text']] = STORY_OVERRIDES[r[sc['id']]]
    speaker_ok = set(heroes) | enemy_ids | set(SPEAKERS)

    gen_chapters, gen_fights, gen_story, relics = [], [], [], []

    def beat(bid, speaker, text):
        assert speaker in speaker_ok or speaker.replace('hero_', '') in [h['key'] for h in NEW_HEROES], f'unknown speaker {speaker} in {bid}'
        gen_story.append([bid, speaker, SPEAKERS.get(speaker, ''), text])
        return bid

    item_only_count = 0
    for n, act, p, ch in plan:
        a = act['num']
        count = FIGHTS_PER_CHAPTER[n - 1]
        assert len(ch['fights']) == count, f"chapter {n} '{ch['name']}' has {len(ch['fights'])} fights, roll says {count}"
        assert 1 <= len(ch['sides']) <= 3, f'chapter {n}: 1-3 side quests'
        cid = f'chapter_{n}'
        existing = {r[fc['name']]: r for r in f_rows if r[fc['chapter']] == cid}
        if n > 3:
            gen_chapters.append([cid, str(n), ch['name'], f'clear_{n}', ''])
            D(f'clear_{n}', 'G', 'Currency', 'gold', lo=gold(a, 300))
            D(f'clear_{n}', 'G', 'RandomEquipment', str(a), weights=GEAR_WEIGHTS[min(8, a + 2)])
        foes = ch['foes'] + [k for k in NEW_FOES.get(n, []) if k not in ch['foes']]
        mid = [round(count * x) for x in (0.45, 0.7)]
        ids = []
        next_new = 9
        for i, fname in enumerate(ch['fights']):
            boss_fight = i == count - 1
            frac = i / max(1, count - 1)
            if fname in existing:
                r = existing[fname]
                r[fc['order']] = str(i + 1)
                ids.append(r[fc['id']])
                continue
            fid = f'fight_{n}_{next_new if n <= 3 else i + 1}'
            if n <= 3:
                next_new += 1
            ids.append(fid)
            if n <= 3:
                # New fights in chapters 1-3: interpolate the neighbours' (tuned) multipliers.
                prev = [existing[x] for x in ch['fights'][:i] if x in existing]
                ms = [float(x.split('@')[1]) for x in prev[-1][fc['waves']].replace('/', '+').split('+')] if prev else [1.5]
                mult = sum(ms) / len(ms)
            else:
                mult = multiplier(p, frac)
            waves = build_waves(foes, i, count, mult, boss=ch['boss'] if boss_fight else None)
            pre, post = [], []
            if n > 3 and i == 0:
                for k, (sp, text) in enumerate(ch['intro'] or []):
                    pre.append(beat(f'beat_c{n}_intro_{k + 1}', sp, text))
            for hid, (rn, ri) in sorted(recruit_at.items()):
                if (rn, ri) == (n, i):
                    pre.append(beat(f'beat_recruit_{hid[5:]}', hid, new_by_id[hid]['line']))
            for k, (sp, text) in enumerate(ch['beats'] if n > 3 else []):
                if i == mid[k]:
                    pre.append(beat(f'beat_c{n}_b{k + 1}', sp, text))
            if boss_fight and n > 3:
                pre.append(beat(f'beat_c{n}_boss', 'enemy_' + ch['boss'], ch['boss_line']))
                post.append(beat(f'beat_c{n}_outro', 'narrator', ch['outro']))
            first = ''
            g = grants.get((n, i))
            if g:
                first = f'fc_{fid}'
                D(first, 'G', 'Currency', 'gold', lo=gold(a, 70))
                for hid, sh in g:
                    D(first, 'G', 'HeroShard', hid, lo=sh)
            elif boss_fight:
                first = f'fc_{fid}'
                D(first, 'G', 'Currency', 'gold', lo=gold(a, 250))
                D(first, 'G', 'RandomEquipment', str(a), weights=GEAR_WEIGHTS[min(8, a + 2)])
            else:
                first = f'fc_a{a}'
            item_only = not boss_fight and i > 0 and RNG.random() < ITEM_ONLY_SHARE
            item_only_count += item_only
            table = f'items_a{a}' if item_only else (f'drops_ch{n}' if n <= 3 else f'drops_a{a}')
            gen_fights.append([fid, cid, str(i + 1), fname, waves, '0', table, first, str(18 + 2 * a) if boss_fight else '0', ';'.join(pre), ';'.join(post), '', 'Main'])
        # Chapters 1-3: the design's extra beats go on the new fights' neighbours as extra pre-beats of generated fights only.
        if n <= 3:
            for k, (sp, text) in enumerate(ch['beats']):
                target = next((r for r in gen_fights if r[1] == cid), None)
                if target is not None:
                    target[9] = ';'.join(x for x in [target[9], beat(f'beat_c{n}_b{k + 1}', sp, text)] if x)
        # Side quests
        unlock_points = [max(1, round(count * x)) - 1 for x in (0.3, 0.55, 0.8)]
        for j, q in enumerate(ch['sides']):
            sid = f'side_{n}_{j + 1}'
            at = unlock_points[j]
            sp = q['pre'][0]
            if sp in recruit_at and recruit_at[sp][0] == n:
                at = max(at, recruit_at[sp][1])
            opener = ids[at]
            frac = at / max(1, count - 1)
            mult = (multiplier(p, frac) if n > 3 else 1.6 + 0.3 * n) * 1.12
            waves = build_waves(foes, at, count, mult, harder=True)
            pre = [beat(f'beat_side_{n}_{j + 1}', sp, q['pre'][1])]
            post = [beat(f'beat_side_{n}_{j + 1}_lore', 'journal', q['lore'])]
            first = f'fc_{sid}'
            D(first, 'G', 'Currency', 'gold', lo=gold(a, 150))
            if q['relic']:
                rr = relic_row(q['relic'][0], q['relic'][1], a)
                relics.append(rr)
                D(first, 'G', 'Equipment', rr[0], rr[3])
            else:
                D(first, 'G', 'RandomEquipment', str(a), weights=SIDE_WEIGHTS[a])
            gen_fights.append([sid, cid, str(count + j + 1), q['name'], waves, '0', f'side_a{a}', first, '0', ';'.join(pre), ';'.join(post), f'ClearFight:{opener}', 'Side'])

    # Act 1 needs a tier-1 version of the generic tables only for chapters 4-5 (drops_a1 = like drops_ch3 but act-1 pool).
    D('drops_a1', 'G', 'Currency', 'gold', lo=60, hi=90)
    for k, (rar, w, lo, hi) in enumerate(SHARD_WEIGHTS):
        D('drops_a1', 'shards', 'RandomHeroShardByRarity', 'act_1', rar, weight=w, lo=lo, hi=hi, rolls=(1, 1) if k == 0 else None)
    D('drops_a1', 'gear', 'RandomEquipment', '1', weights='Common=25;Uncommon=50;Rare=21;Epic=4', weight=70, rolls=(1, 1))
    D('drops_a1', 'gear', 'Currency', 'gold', weight=30, lo=40, hi=70)
    for r in gen_fights:
        if r[6] in ('drops_ch4', 'drops_ch5'):
            r[6] = 'drops_a1'

    # ---- keep tuned values: same enemies in the same waves -> keep the existing multipliers and recommended power
    def shape(waves):
        return [[x.split('@')[0] for x in w.split('+')] for w in waves.replace(' ', '').split('/')]
    previous = {r[fc['id']]: r for r in cs.rows(f_sections.get(KEY, ''))}
    for r in gen_fights:
        old = previous.get(r[0])
        if old and shape(old[fc['waves']]) == shape(r[4]):
            r[4] = old[fc['waves']]
            r[5] = old[fc['recommendedPower']]

    # ---- write everything
    for name, comments, header, rows_, sections, gen in [
        ('chapters.csv', c_comments, c_header, c_rows, c_sections, gen_chapters),
        ('fights.csv', f_comments, f_header, f_rows, f_sections, gen_fights),
        ('story.csv', s_comments, s_header, s_rows, s_sections, gen_story),
        ('droptables.csv', d_comments, d_header, d_rows, d_sections, drops),
        ('equipment.csv', q_comments, q_header, q_rows, q_sections, equipment + relics),
    ]:
        sections[KEY] = cs.to_text(gen)
        cs.write(name, head_text(comments, header, rows_), sections)

    main_fights = sum(FIGHTS_PER_CHAPTER)
    sides = sum(len(ch['sides']) for _, _, _, ch in plan)
    print(f'40 chapters, {main_fights} main fights ({item_only_count} item-only), {sides} side quests, '
          f'{len(gen_enemies)} new enemies, {len(equipment)} tier items + {len(relics)} relics, {len(gen_story)} new story beats, '
          f'{len(recruit_at)} heroes recruited in the story')


if __name__ == '__main__':
    main()
