"""Equipment icons, one per template id in Content/Source/equipment.csv (256x256, full color, transparent)."""
import math
import os

from paths import ITEM_DIR
from svgkit import Defs, circle, ellipse, g, ink, path, poly, rect, render, rounded_poly, shade, star_points

MAT = {
    'wood': '#a8703c', 'iron': '#a8b0bc', 'steel': '#d0d8e4', 'leather': '#9a6038', 'cloth': '#c8b08a', 'gold': '#f2c14e',
    'silver': '#dfe4ec', 'copper': '#d0804a', 'runed': '#8a94b0', 'dragon': '#c8302a', 'shadow': '#4a3a5a', 'wind': '#6ad0c0',
    'knight': '#c0c8d6', 'crown': '#f2c14e',
}


def lin(d, c, light=0.45, dark=-0.4):
    return d.lin([(0, shade(c, light)), (0.5, c), (1, shade(c, dark))], 0.1, 0, 0.9, 1)


def sword(d, blade, guard, grip, length=1.0, width=1.0, glow=None):
    L = 150 * length
    w = 11 * width
    out = []
    if glow:
        out.append(circle(128, 120, 100, d.rad([(0, glow, 0.6), (1, glow, 0)])))
    body = [poly([(128 - w, 170 - 6), (128 + w, 170 - 6), (128 + w * 1.15, 170 - L), (128, 170 - L - 22), (128 - w * 1.15, 170 - L)],
                 d.lin([(0, shade(blade, 0.6)), (0.48, shade(blade, 0.1)), (0.52, shade(blade, -0.25)), (1, shade(blade, -0.45))], 0, 0, 1, 0), ink(blade), 7),
            path('M128 %.1f L128 %.1f' % (160, 175 - L), 'none', '#ffffff', 3, opacity=0.6),
            rect(84, 162, 88, 18, 8, lin(d, guard), ink(guard), 6),
            rect(119, 178, 18, 40, 6, lin(d, grip), ink(grip), 5),
            circle(128, 224, 12, lin(d, guard), ink(guard), 5)]
    if glow:
        for i in range(3):
            y = 140 - i * 32 * length
            body.append(circle(128, y, 8, d.rad([(0, '#ffffff'), (1, glow, 0)])))
            body.append(poly([(128, y - 6), (133, y), (128, y + 6), (123, y)], glow))
    out.append(g(body, 'rotate(40 128 128)'))
    return ''.join(out)


def helmet(d, c, kind):
    out = []
    if kind == 'cap':
        out += [path('M44 150 C44 70 212 70 212 150 Q128 128 44 150 Z', lin(d, c), ink(c), 8),
                path('M36 152 Q128 120 220 152 L216 170 Q128 146 40 170 Z', lin(d, shade(c, -0.2)), ink(c), 7),
                path('M128 78 L128 136', 'none', shade(c, -0.35), 5)]
    elif kind == 'crown':
        pts = [(48, 190), (40, 90), (84, 130), (104, 64), (128, 116), (152, 64), (172, 130), (216, 90), (208, 190)]
        out += [circle(128, 128, 110, d.rad([(0, '#fff2b0', 0.5), (1, '#fff2b0', 0)])), path(rounded_poly(pts, 6), lin(d, c, 0.55, -0.45), ink(c), 8),
                rect(46, 168, 164, 26, 6, lin(d, shade(c, -0.1)), ink(c), 6)]
        for x, gc in ((84, '#e0303c'), (128, '#3a8aff'), (172, '#3ac05a')):
            out += [circle(x, 181, 9, gc, ink(gc), 3), circle(x - 3, 178, 3, '#ffffff', opacity=0.8)]
        for x, y in ((40, 90), (104, 64), (152, 64), (216, 90)):
            out.append(circle(x, y, 9, '#fff6c8', ink(c), 4))
    else:
        out += [path('M40 160 C40 50 216 50 216 160 L216 206 L164 206 L164 152 L92 152 L92 206 L40 206 Z', lin(d, c, 0.6, -0.5), ink(c), 8),
                rect(118, 70, 20, 136, 6, lin(d, shade(c, -0.15)), ink(c), 4),
                path('M66 110 Q84 76 116 70', 'none', '#ffffff', 7, opacity=0.6)]
        if kind == 'knight':
            out += [path('M128 62 C120 20 170 8 190 26 C164 26 146 38 138 62', '#d0303a', '#4a0a10', 6)]
            for x in (70, 100, 156, 186):
                out.append(rect(x - 6, 168, 12, 12, 3, '#f2c14e', '#5a3a08', 2))
    return ''.join(out)


def chest(d, c, kind):
    body = 'M70 40 L104 28 Q128 50 152 28 L186 40 L216 96 L182 112 L180 222 L76 222 L74 112 L40 96 Z'
    out = [path(body, lin(d, c, 0.5, -0.45), ink(c), 8)]
    if kind in ('plate', 'dragon'):
        out += [path('M128 60 L128 216', 'none', shade(c, -0.4), 5), path('M84 112 Q128 132 172 112', 'none', shade(c, -0.4), 5),
                path('M84 160 Q128 180 172 160', 'none', shade(c, -0.4), 5)]
    if kind == 'chain':
        for yy in range(64, 220, 14):
            for xx in range(84, 176, 14):
                out.append(circle(xx + (7 if (yy // 14) % 2 else 0), yy, 5, 'none', shade(c, -0.35), 2.5))
    if kind == 'padded':
        for yy in range(70, 220, 30):
            out.append(path('M78 %d L178 %d' % (yy, yy), 'none', shade(c, -0.3), 4))
        out.append(path('M128 50 L128 220', 'none', shade(c, -0.3), 4))
    if kind == 'dragon':
        for yy in range(70, 210, 22):
            for xx in range(90, 170, 22):
                out.append(path('M%d %d q11 14 22 0' % (xx + (11 if (yy // 22) % 2 else 0) - 11, yy), 'none', shade(c, 0.35), 3))
        out.append(poly(star_points(128, 92, 16, 7, 6), '#ffd36a', '#6a2a06', 3))
    out.append(path('M86 60 Q100 48 116 52', 'none', '#ffffff', 6, opacity=0.6))
    return ''.join(out)


def gloves(d, c, kind):
    hand = ('M84 226 L84 140 L70 92 Q66 74 82 72 Q94 72 98 92 L104 124 L104 52 Q104 36 118 36 Q132 36 132 52 L136 116 L140 44 Q140 28 154 28 Q168 28 168 44 '
            'L168 116 L176 66 Q180 50 194 54 Q206 58 202 74 L194 160 Q190 200 182 226 Z')
    out = [path(hand, lin(d, c, 0.45, -0.4), ink(c), 8), rect(76, 190, 114, 38, 10, lin(d, shade(c, -0.25)), ink(c), 6)]
    if kind in ('hunter', 'shadow'):
        for x in (104, 136, 168):
            out.append(rect(x - 10, 92, 20, 16, 4, '#c8d0dc' if kind == 'hunter' else '#a07ac0', '#2a2030', 3))
    if kind == 'shadow':
        out.insert(0, circle(128, 128, 110, d.rad([(0, '#a07aff', 0.55), (1, '#a07aff', 0)])))
    return ''.join(out)


def boots(d, c, kind):
    b = 'M74 26 L150 26 L150 140 Q214 146 222 190 L222 222 L54 222 L54 186 Q72 176 74 140 Z'
    if kind == 'sandals':
        return ''.join([path('M56 190 Q60 168 110 168 L200 172 Q226 176 224 200 L222 214 L58 214 Z', lin(d, c), ink(c), 8),
                        path('M84 172 L120 112 L148 172 M100 140 L150 140', 'none', ink(c), 12), path('M84 172 L120 112 L148 172 M100 140 L150 140', 'none', shade(c, 0.3), 6)])
    out = [path(b, lin(d, c, 0.45, -0.45), ink(c), 8), rect(66, 26, 92, 30, 8, lin(d, shade(c, -0.2)), ink(c), 6), path('M54 204 L222 204', 'none', ink(c), 8)]
    if kind == 'wind':
        out.insert(0, circle(128, 128, 112, d.rad([(0, '#b0fff0', 0.5), (1, '#b0fff0', 0)])))
        for i in range(3):
            out.append(path('M%d %d q20 -18 40 0' % (14 + i * 6, 80 + i * 36), 'none', '#e8fff8', 6, opacity=0.9))
        out.append(path('M150 70 C180 40 210 60 196 90 C190 70 172 66 150 90 Z', '#ffffff', '#3a8a80', 4))
    if kind == 'swift':
        out.append(path('M150 60 C170 40 196 52 190 76 C180 62 166 64 150 80 Z', '#ffd36a', '#6a3a06', 4))
    out.append(path('M92 50 L92 130', 'none', '#ffffff', 6, opacity=0.35))
    return ''.join(out)


def ring(d, band, gem, glow=None):
    out = []
    if glow:
        out.append(circle(128, 120, 110, d.rad([(0, glow, 0.6), (1, glow, 0)])))
    out += [ellipse(128, 156, 72, 64, 'none', ink(band), 34), ellipse(128, 156, 72, 64, 'none', lin(d, band, 0.6, -0.45), 24),
            path('M70 140 Q80 112 108 102', 'none', '#ffffff', 6, opacity=0.6)]
    if gem:
        out += [path(rounded_poly([(128, 40), (168, 80), (128, 120), (88, 80)], 6), d.lin([(0, shade(gem, 0.7)), (0.5, gem), (1, shade(gem, -0.5))], 0, 0, 1, 1), ink(gem), 7),
                poly([(128, 46), (128, 114), (94, 80)], '#ffffff', None, None, opacity=0.25), circle(114, 66, 6, '#ffffff', opacity=0.8)]
    return ''.join(out)


ITEMS = {
    'eq_wooden_sword': lambda d: sword(d, MAT['wood'], '#7a4a24', '#5a3420', 0.85, 1.2),
    'eq_iron_sword': lambda d: sword(d, MAT['iron'], '#8a7a6a', '#5a3420'),
    'eq_steel_blade': lambda d: sword(d, MAT['steel'], '#3a64b8', '#2a2a3a', 1.05, 1.0),
    'eq_runed_greatsword': lambda d: sword(d, MAT['runed'], '#f2c14e', '#3a1414', 1.15, 1.55, '#ff6a2a'),
    'eq_leather_cap': lambda d: helmet(d, MAT['leather'], 'cap'),
    'eq_iron_helm': lambda d: helmet(d, MAT['iron'], 'helm'),
    'eq_knight_helm': lambda d: helmet(d, MAT['knight'], 'knight'),
    'eq_crown_of_ages': lambda d: helmet(d, MAT['crown'], 'crown'),
    'eq_padded_vest': lambda d: chest(d, MAT['cloth'], 'padded'),
    'eq_chainmail': lambda d: chest(d, MAT['iron'], 'chain'),
    'eq_plate_armor': lambda d: chest(d, MAT['steel'], 'plate'),
    'eq_dragonscale_mail': lambda d: chest(d, MAT['dragon'], 'dragon'),
    'eq_cloth_gloves': lambda d: gloves(d, MAT['cloth'], 'cloth'),
    'eq_leather_gloves': lambda d: gloves(d, MAT['leather'], 'leather'),
    'eq_hunter_gauntlets': lambda d: gloves(d, '#6a7a4a', 'hunter'),
    'eq_shadow_grips': lambda d: gloves(d, MAT['shadow'], 'shadow'),
    'eq_sandals': lambda d: boots(d, MAT['leather'], 'sandals'),
    'eq_travel_boots': lambda d: boots(d, '#8a5a34', 'travel'),
    'eq_swift_boots': lambda d: boots(d, '#3a6ad0', 'swift'),
    'eq_windwalkers': lambda d: boots(d, MAT['wind'], 'wind'),
    'eq_copper_ring': lambda d: ring(d, MAT['copper'], None),
    'eq_silver_ring': lambda d: ring(d, MAT['silver'], '#5ab0ff'),
    'eq_gold_ring': lambda d: ring(d, MAT['gold'], '#e0303c'),
    'eq_starfire_ring': lambda d: ring(d, MAT['gold'], '#ff8a2a', '#ffb04a'),
}


TIER_COLOR = {2: '#3a8a8a', 3: '#5f8a3a', 4: '#d8a050', 5: '#9ad0e0', 6: '#8a92a8', 7: '#a01a2a', 8: '#d8dce8'}
TIER_GEM = {2: '#5ad0e0', 3: '#9ae05a', 4: '#ffcf4a', 5: '#e0f8ff', 6: '#ff8a2a', 7: '#ff3a5a', 8: '#b08aff'}


def generated_items():
    """Icons for the tiered gear and side-quest relics written by tools/content/gen_campaign.py."""
    import csv
    import random
    import sys
    sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', 'content'))
    import csvsections
    _, sections = csvsections.read('equipment.csv')
    out = {}
    for r in csv.reader(sections.get('gen_campaign', '').splitlines()):
        if not r or r[0].startswith('#'):
            continue
        iid, name, slot, tier = r[0], r[1], r[2], int(r[4])
        rnd = random.Random(iid)
        t = tier or rnd.randint(2, 8)
        c, gem = TIER_COLOR[t], TIER_GEM[t]
        glow = gem if tier == 0 or t >= 6 else None
        if slot == 'Weapon':
            out[iid] = (lambda c=c, gem=gem, glow=glow, big='axe' in iid or rnd.random() < 0.3: lambda d: sword(d, c, gem, '#2a1a14', 1.15 if big else 1.0, 1.5 if big else 1.0, glow))()
        elif slot == 'Helmet':
            out[iid] = (lambda c=c, k=['helm', 'knight', 'crown'][min(2, t // 3)]: lambda d: helmet(d, c, k))()
        elif slot == 'Chest':
            out[iid] = (lambda c=c, k=['chain', 'plate', 'dragon'][min(2, t // 3)]: lambda d: chest(d, c, k))()
        elif slot == 'Gloves':
            out[iid] = (lambda c=c, k=['leather', 'hunter', 'shadow'][min(2, t // 3)]: lambda d: gloves(d, c, k))()
        elif slot == 'Boots':
            out[iid] = (lambda c=c, k=['travel', 'swift', 'wind'][min(2, t // 3)]: lambda d: boots(d, c, k))()
        else:
            out[iid] = (lambda c=c, gem=gem, glow=glow: lambda d: ring(d, c, gem, glow))()
    return out


def main():
    os.makedirs(ITEM_DIR, exist_ok=True)
    # --generated: only the tiered gear and relics (the 24 original icons stay untouched)
    items = generated_items() if '--generated' in os.sys.argv else {**ITEMS, **generated_items()}
    for iid, fn in items.items():
        d = Defs()
        body = g(fn(d), None, filter=d.shadow(0, 5, 4, '#000000', 0.45))
        render(body, os.path.join(ITEM_DIR, iid + '.png'), 256, 256, defs=d)


if __name__ == '__main__':
    main()
