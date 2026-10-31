"""UI kit: 9-slice panels/buttons (grayscale so the Theme colors still tint them), rarity card frames/backs, rarity gems,
stars, speech bubble, logo, and the full-color icon set.

Outputs a slices.json next to the sprites with 9-slice borders (left, bottom, right, top in pixels) for the importer.
"""
import json
import math
import os

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

from paths import APP_ICON, CHAR_DIR, FONT, ICON_DIR, UI_DIR
from svgkit import (Defs, capsule, circle, ellipse, g, ink, mix, path, poly, rasterize, rect, render, rounded_poly, save, shade,
                    star_points)

RARITY = {'common': '#B0B4BA', 'uncommon': '#4FBF5A', 'rare': '#3D8BFF', 'epic': '#A54BFF', 'legendary': '#FFAA22'}
SLICES = {}


def slice_(name, border):
    SLICES[name] = border


# =========================================================================== tintable 9-slices


def gen_nine_slices():
    # panel: soft white body, light top bevel, darker rim; tinted by Theme.panel etc.
    d = Defs()
    s = 128
    body = [rect(4, 6, 120, 118, 30, '#000000', opacity=0.35),
            rect(4, 2, 120, 118, 30, d.lin([(0, '#ffffff'), (1, '#d6d6d6')])),
            rect(4, 2, 120, 118, 30, 'none', '#8a8a8a', 4),
            rect(10, 8, 108, 106, 24, 'none', '#ffffff', 3, opacity=0.55)]
    render(''.join(body), os.path.join(UI_DIR, 'panel.png'), s, s, defs=d)
    slice_('panel', [40, 40, 40, 40])

    # button: glossy pill with a 3D lip at the bottom
    d = Defs()
    body = [rect(4, 10, 120, 114, 34, '#000000', opacity=0.35),
            rect(4, 4, 120, 112, 34, '#9a9a9a'),
            rect(4, 0, 120, 104, 34, d.lin([(0, '#ffffff'), (0.5, '#f2f2f2'), (0.52, '#e2e2e2'), (1, '#cfcfcf')])),
            rect(4, 0, 120, 112, 34, 'none', '#6e6e6e', 4),
            path('M22 14 Q64 6 106 14', 'none', '#ffffff', 6, opacity=0.9)]
    render(''.join(body), os.path.join(UI_DIR, 'button.png'), s, s, defs=d)
    slice_('button', [44, 46, 44, 44])

    # progress bar background (recessed) and fill (glossy)
    d = Defs()
    render(''.join([rect(2, 2, 60, 28, 14, d.lin([(0, '#7a7a7a'), (1, '#c8c8c8')])), rect(2, 2, 60, 28, 14, 'none', '#4a4a4a', 3)]),
           os.path.join(UI_DIR, 'bar_bg.png'), 64, 32, defs=d)
    slice_('bar_bg', [15, 15, 15, 15])
    d = Defs()
    render(''.join([rect(1, 1, 62, 30, 15, d.lin([(0, '#ffffff'), (0.45, '#f0f0f0'), (0.5, '#d8d8d8'), (1, '#c0c0c0')])),
                    path('M10 8 L54 8', 'none', '#ffffff', 4, opacity=0.9)]), os.path.join(UI_DIR, 'bar_fill.png'), 64, 32, defs=d)
    slice_('bar_fill', [15, 15, 15, 15])

    # speech bubble (white) + tail
    d = Defs()
    render(''.join([rect(6, 8, 116, 112, 36, '#000000', opacity=0.25), rect(4, 4, 116, 112, 36, '#ffffff'), rect(4, 4, 116, 112, 36, 'none', '#2a2234', 5)]),
           os.path.join(UI_DIR, 'bubble.png'), 128, 128, defs=d)
    slice_('bubble', [44, 44, 44, 44])
    render(''.join([poly([(4, 0), (60, 0), (14, 56)], '#ffffff'), path('M4 2 L14 56 L60 2', 'none', '#2a2234', 5)]),
           os.path.join(UI_DIR, 'bubble_tail.png'), 64, 64)

    # soft glow + ring
    d = Defs()
    render(circle(128, 128, 126, d.rad([(0, '#ffffff', 1), (0.35, '#ffffff', 0.55), (1, '#ffffff', 0)])), os.path.join(UI_DIR, 'glow.png'), 256, 256, defs=d)
    d = Defs()
    render(''.join([circle(128, 128, 112, 'none', '#ffffff', 14), circle(128, 128, 122, 'none', '#ffffff', 4, opacity=0.5),
                    circle(128, 128, 102, 'none', '#ffffff', 3, opacity=0.4)]), os.path.join(UI_DIR, 'ring.png'), 256, 256, defs=d)
    # vignette for backgrounds / dialogue backdrop
    d = Defs()
    render(rect(0, 0, 256, 256, 0, d.rad([(0, '#000000', 0), (0.6, '#000000', 0.15), (1, '#000000', 0.75)], 0.5, 0.45, 0.75)), os.path.join(UI_DIR, 'vignette.png'), 256, 256, defs=d)


# =========================================================================== hero card frames + backs


def card_frame(rarity, c):
    """600x800 ornate frame: transparent window, bottom name plate, corner ornaments; the gem/class icons go on top in code."""
    d = Defs()
    W, H = 600, 800
    out = []
    edge = d.lin([(0, shade(c, 0.55)), (0.25, shade(c, 0.05)), (0.5, shade(c, -0.35)), (0.75, shade(c, 0.15)), (1, shade(c, -0.45))], 0, 0, 1, 1)
    gold = '#f2c14e' if rarity in ('legendary', 'epic') else '#d8dde6' if rarity in ('common', 'uncommon') else '#cfe0ff'
    # outer frame with window cut out (even-odd)
    frame = ('M20 6 H580 Q594 6 594 20 V780 Q594 794 580 794 H20 Q6 794 6 780 V20 Q6 6 20 6 Z '
             'M44 40 H556 Q562 40 562 46 V754 Q562 760 556 760 H44 Q38 760 38 754 V46 Q38 40 44 40 Z')
    out.append(path(frame, '#000000', None, None, fill_rule='evenodd', opacity=0.4, transform='translate(0 6)'))
    out.append(path(frame, edge, ink(c), 6, fill_rule='evenodd'))
    out.append(path('M24 14 H576 Q586 14 586 24 V776 Q586 786 576 786 H24 Q14 786 14 776 V24 Q14 14 24 14 Z', 'none', '#ffffff', 3, opacity=0.5))
    out.append(path('M44 40 H556 Q562 40 562 46 V754 Q562 760 556 760 H44 Q38 760 38 754 V46 Q38 40 44 40 Z', 'none', shade(c, -0.6), 4))
    # name plate (bottom)
    plate = rounded_poly([(30, 600), (570, 600), (586, 640), (570, 700), (30, 700), (14, 640)], 12)
    out.append(path(plate, '#000000', None, None, opacity=0.35, transform='translate(0 6)'))
    out.append(path(plate, d.lin([(0, shade(c, -0.25)), (0.5, shade(c, -0.55)), (1, shade(c, -0.7))]), ink(c), 6))
    out.append(path(rounded_poly([(44, 612), (556, 612), (568, 640), (556, 688), (44, 688), (32, 640)], 8), 'none', gold, 3, opacity=0.8))
    # corner ornaments
    for (x, y, sx, sy) in ((6, 6, 1, 1), (594, 6, -1, 1), (6, 794, 1, -1), (594, 794, -1, -1)):
        orn = 'M%d %d l%d 0 q%d 0 %d %d l0 %d q%d -%d -%d -%d Z' % (x, y, sx * 70, sx * 10, sx * 10, sy * 10, sy * 60, -sx * 0, 0, sx * 0, 0)
        out.append(path('M%d %d L%d %d L%d %d Z' % (x, y, x + sx * 80, y, x, y + sy * 80), d.lin([(0, shade(gold, 0.4)), (1, shade(gold, -0.3))], 0, 0, 1, 1), ink(gold), 5))
        out.append(circle(x + sx * 26, y + sy * 26, 9, gold, ink(gold), 3))
    # top crest
    crest = rounded_poly([(240, 6), (360, 6), (340, 46), (300, 62), (260, 46)], 8)
    out.append(path(crest, d.lin([(0, shade(gold, 0.35)), (1, shade(gold, -0.35))]), ink(gold), 5))
    if rarity in ('epic', 'legendary'):
        out.append(circle(300, 30, 26, d.rad([(0, '#ffffff', 0.9), (1, c, 0)])))
    # legendary flourish: flames along the sides
    if rarity == 'legendary':
        for side in (1, -1):
            x0 = 22 if side == 1 else 578
            for i in range(5):
                y0 = 160 + i * 90
                out.append(path('M%d %d q%d -30 0 -60 q%d 20 %d 30 q-%d 10 0 30 Z' % (x0, y0, side * 22, side * 14, side * 6, side * 10), '#ffd36a', ink('#ffaa22'), 3, opacity=0.9))
    return render(''.join(out), os.path.join(UI_DIR, 'card_frame_%s.png' % rarity), W, H, defs=d)


def card_back(rarity, c):
    d = Defs()
    W, H = 600, 800
    out = [rect(0, 0, W, H, 0, d.rad([(0, shade(c, 0.1)), (0.55, shade(c, -0.45)), (1, shade(c, -0.78))], 0.5, 0.36, 0.8))]
    # light rays
    for i in range(16):
        a = math.radians(i * 22.5)
        x2, y2 = 300 + math.cos(a) * 900, 300 + math.sin(a) * 900
        a2 = math.radians(i * 22.5 + 8)
        x3, y3 = 300 + math.cos(a2) * 900, 300 + math.sin(a2) * 900
        out.append(poly([(300, 300), (x2, y2), (x3, y3)], '#ffffff', None, None, opacity=0.06))
    # diamond pattern
    for yy in range(0, H + 60, 60):
        for xx in range(0, W + 60, 60):
            off = 30 if (yy // 60) % 2 else 0
            out.append(poly([(xx + off, yy - 10), (xx + off + 10, yy), (xx + off, yy + 10), (xx + off - 10, yy)], '#ffffff', None, None, opacity=0.05))
    out.append(circle(300, 300, 230, d.rad([(0, '#ffffff', 0.35), (1, '#ffffff', 0)])))
    out.append(rect(0, 520, W, 280, 0, d.lin([(0, '#000000', 0), (1, '#000000', 0.55)])))
    render(''.join(out), os.path.join(UI_DIR, 'card_back_%s.png' % rarity), W, H, defs=d)


# =========================================================================== gems, stars, misc


def gem(rarity, c):
    """Rarity gems keep the colorblind-friendly shapes: circle, diamond, triangle, hexagon shard, star."""
    d = Defs()
    s = 128
    hi, lo = shade(c, 0.6), shade(c, -0.45)
    fill = d.lin([(0, hi), (0.45, c), (1, lo)], 0.2, 0, 0.8, 1)
    out = []
    if rarity == 'common':
        out += [circle(64, 66, 50, fill, ink(c), 7), circle(64, 66, 34, 'none', '#ffffff', 3, opacity=0.4)]
    elif rarity == 'uncommon':
        pts = [(64, 10), (116, 64), (64, 118), (12, 64)]
        out += [poly(pts, fill, ink(c), 7), poly([(64, 10), (64, 118), (12, 64)], '#ffffff', None, None, opacity=0.18),
                path('M64 10 L64 118 M12 64 L116 64', 'none', '#ffffff', 2.5, opacity=0.4)]
    elif rarity == 'rare':
        pts = [(64, 10), (120, 110), (8, 110)]
        out += [path(rounded_poly(pts, 10), fill, ink(c), 7), poly([(64, 18), (64, 104), (18, 104)], '#ffffff', None, None, opacity=0.18)]
    elif rarity == 'epic':
        pts = [(64, 6), (114, 36), (114, 92), (64, 122), (14, 92), (14, 36)]
        out += [poly(pts, fill, ink(c), 7), poly([(64, 6), (64, 64), (14, 36)], '#ffffff', None, None, opacity=0.3),
                poly([(64, 64), (114, 92), (64, 122)], '#000000', None, None, opacity=0.15), path('M64 6 L64 122', 'none', '#ffffff', 2, opacity=0.3)]
    else:
        out += [circle(64, 64, 60, d.rad([(0, '#ffffff', 0.7), (1, c, 0)])), poly(star_points(64, 68, 58, 26), fill, ink(c), 7),
                poly(star_points(64, 68, 30, 13), '#ffffff', None, None, opacity=0.3)]
    out.append(ellipse(46, 40, 13, 8, '#ffffff', opacity=0.8, transform='rotate(-30 46 40)'))
    render(''.join(out), os.path.join(UI_DIR, 'gem_%s.png' % rarity), s, s, defs=d)


def stars_and_misc():
    d = Defs()
    gold = '#ffc83a'
    render(''.join([poly(star_points(64, 70, 58, 26), '#000000', None, None, opacity=0.3, transform='translate(0 4)'),
                    poly(star_points(64, 66, 58, 26), d.lin([(0, '#fff3b0'), (0.5, gold), (1, '#e07a10')]), '#7a3a08', 6),
                    poly(star_points(64, 66, 30, 13), '#ffffff', None, None, opacity=0.35)]), os.path.join(UI_DIR, 'star_full.png'), 128, 128, defs=d)
    d = Defs()
    render(''.join([poly(star_points(64, 66, 58, 26), d.lin([(0, '#6a7088'), (1, '#3a3e50')]), '#20222c', 6)]), os.path.join(UI_DIR, 'star_empty.png'), 128, 128, defs=d)
    d = Defs()
    render(''.join([circle(32, 32, 26, d.lin([(0, '#ff7a80'), (1, '#d01a2a')]), '#ffffff', 5), ellipse(26, 24, 8, 5, '#ffffff', opacity=0.7)]),
           os.path.join(UI_DIR, 'red_dot.png'), 64, 64, defs=d)
    # victory/defeat ribbon (wide) - grayscale so it can be tinted
    d = Defs()
    rib = [poly([(0, 40), (110, 40), (110, 180), (0, 180), (40, 110)], '#8a8a8a', '#3a3a3a', 6),
           poly([(1000, 40), (890, 40), (890, 180), (1000, 180), (960, 110)], '#8a8a8a', '#3a3a3a', 6),
           rect(80, 20, 840, 160, 20, d.lin([(0, '#ffffff'), (0.5, '#e6e6e6'), (1, '#b8b8b8')]), '#3a3a3a', 6),
           path('M100 40 L900 40', 'none', '#ffffff', 6, opacity=0.8)]
    render(''.join(rib), os.path.join(UI_DIR, 'ribbon.png'), 1000, 200, defs=d)


# =========================================================================== icons (full color)


def icon(name, body, defs, s=128, shadow=True):
    content = body
    if shadow:
        content = g(body, None, filter=defs.shadow(0, 3, 2, '#000000', 0.45))
    render(content, os.path.join(ICON_DIR, name + '.png'), s, s, vb=(0, 0, 128, 128), defs=defs)


def gen_icons():
    D = Defs
    # ---- currency & shard
    d = D()
    coin = [circle(64, 64, 50, d.lin([(0, '#fff1a8'), (0.5, '#ffc83a'), (1, '#c8780a')]), '#6a3a06', 6),
            circle(64, 64, 36, 'none', '#a8600a', 4), circle(64, 64, 36, d.lin([(0, '#ffd85a'), (1, '#e89a1a')])),
            poly(star_points(64, 66, 22, 9), '#fff4c0', '#8a4a06', 3),
            ellipse(46, 40, 12, 7, '#ffffff', opacity=0.75, transform='rotate(-35 46 40)')]
    icon('currency_gold', ''.join(coin), d)
    d = D()
    sh = [poly([(64, 8), (96, 50), (80, 118), (48, 118), (32, 50)], d.lin([(0, '#e8f6ff'), (0.4, '#7ad0ff'), (1, '#2a5ad0')], 0, 0, 1, 1), '#14286a', 6),
          poly([(64, 8), (64, 118), (48, 118), (32, 50)], '#ffffff', None, None, opacity=0.25), path('M32 50 L96 50 M64 8 L64 118', 'none', '#ffffff', 2.5, opacity=0.5)]
    icon('shard', ''.join(sh), d)
    d = D()
    pw = [path('M64 10 L76 46 L114 46 L84 68 L96 106 L64 84 L32 106 L44 68 L14 46 L52 46 Z', d.lin([(0, '#ffe08a'), (1, '#ff7a2a')]), '#6a2408', 6)]
    pw = [poly([(70, 8), (34, 70), (60, 70), (50, 120), (96, 52), (68, 52), (82, 8)], d.lin([(0, '#fff2a0'), (1, '#ff8a1a')]), '#6a2a06', 6)]
    icon('power', ''.join(pw), d)

    # ---- tab icons
    d = D()
    home = [poly([(64, 14), (118, 60), (104, 60), (104, 112), (24, 112), (24, 60), (10, 60)], d.lin([(0, '#ffe0a8'), (1, '#e8a050')]), '#5a2e10', 6),
            poly([(64, 14), (118, 60), (10, 60)], d.lin([(0, '#ff7a5a'), (1, '#c83a2a')]), '#5a1a10', 6),
            rect(52, 74, 24, 38, 4, '#7a4a24', '#3a1e0a', 4), rect(84, 70, 14, 14, 2, '#bfe8ff', '#3a1e0a', 3)]
    icon('tab_home', ''.join(home), d)
    d = D()
    mp = [poly([(10, 26), (44, 14), (84, 26), (118, 14), (118, 104), (84, 116), (44, 104), (10, 116)], d.lin([(0, '#fbecc4'), (1, '#e0c080')]), '#5a3a14', 6),
          path('M44 14 L44 104 M84 26 L84 116', 'none', '#a88050', 3), path('M24 92 Q40 60 60 74 T100 40', 'none', '#c83a2a', 5, stroke_dasharray='8 7'),
          path('M92 32 l12 12 m0 -12 l-12 12', 'none', '#c83a2a', 6)]
    icon('tab_campaign', ''.join(mp), d)
    d = D()
    hel = [path('M24 74 C24 30 104 30 104 74 L104 96 L82 96 L82 70 L46 70 L46 96 L24 96 Z', d.metal('#b8c4d8') if hasattr(d, 'metal') else d.lin([(0, '#f2f6ff'), (0.5, '#a8b4c8'), (1, '#5a6478')]), '#1e2638', 6),
           rect(58, 30, 12, 66, 4, '#e8c060', '#5a3a08', 3), path('M64 30 C60 10 86 4 98 14 C84 14 74 20 70 30', '#3a6ad0', '#14286a', 4),
           path('M34 56 Q44 40 60 38', 'none', '#ffffff', 4, opacity=0.7)]
    icon('tab_heroes', ''.join(hel), d)
    d = D()
    bag = [path('M30 46 Q22 112 64 116 Q106 112 98 46 Z', d.lin([(0, '#d8a060'), (1, '#8a5424')]), '#3a1e08', 6),
           path('M40 46 Q64 30 88 46', 'none', '#3a1e08', 10), path('M40 46 Q64 30 88 46', 'none', '#b07a40', 5),
           rect(36, 38, 56, 14, 6, '#c8903a', '#3a1e08', 5), circle(64, 80, 12, '#ffc83a', '#6a3a06', 4), path('M42 62 Q46 92 58 104', 'none', '#ffffff', 4, opacity=0.35)]
    icon('tab_inventory', ''.join(bag), d)
    d = D()
    teeth = []
    for i in range(8):
        a = i * 45
        teeth.append(rect(56, 6, 16, 26, 4, '#b8c0d0', '#1e2638', 5, transform='rotate(%d 64 64)' % a))
    gear = teeth + [circle(64, 64, 40, d.lin([(0, '#e8eef8'), (1, '#7a849a')]), '#1e2638', 6), circle(64, 64, 16, '#3a4256', '#1e2638', 5)]
    icon('tab_settings', ''.join(gear), d)

    # ---- equipment slots
    d = D()
    sw = [g([poly([(58, 10), (70, 10), (72, 82), (64, 94), (56, 82)], d.lin([(0, '#ffffff'), (0.5, '#c8d0dc'), (1, '#8a94a4')], 0, 0, 1, 0), '#2a3040', 5),
             rect(36, 80, 56, 12, 5, '#e8c060', '#5a3a08', 4), rect(58, 92, 12, 22, 4, '#6a3a24', '#2a1408', 4), circle(64, 118, 7, '#e8c060', '#5a3a08', 3)],
            'rotate(40 64 64)')]
    icon('slot_weapon', ''.join(sw), d)
    d = D()
    hm = [path('M20 80 C20 24 108 24 108 80 L108 100 L80 100 L80 72 L48 72 L48 100 L20 100 Z', d.lin([(0, '#f2f6ff'), (0.5, '#a8b4c8'), (1, '#5a6478')]), '#1e2638', 6),
          rect(58, 40, 12, 60, 4, '#9aa6ba', '#1e2638', 3), path('M32 60 Q42 40 60 36', 'none', '#ffffff', 4, opacity=0.7)]
    icon('slot_helmet', ''.join(hm), d)
    d = D()
    ch = [path('M26 22 L50 14 Q64 26 78 14 L102 22 L116 54 L98 62 L96 112 L32 112 L30 62 L12 54 Z', d.lin([(0, '#e8eef8'), (0.5, '#a8b4c8'), (1, '#5a6478')]), '#1e2638', 6),
          path('M64 30 L64 108', 'none', '#5a6478', 4), path('M40 60 Q64 72 88 60', 'none', '#5a6478', 4), path('M38 34 Q46 28 54 30', 'none', '#ffffff', 4, opacity=0.7)]
    icon('slot_chest', ''.join(ch), d)
    d = D()
    gl = [path('M36 116 L36 70 L28 44 Q26 34 36 34 Q42 34 44 44 L48 62 L48 22 Q48 14 56 14 Q64 14 64 22 L66 56 L68 18 Q68 10 76 10 Q84 10 84 18 L84 56 L88 26 Q90 18 98 20 Q104 22 102 30 L98 80 Q96 100 92 116 Z',
               d.lin([(0, '#c89060'), (1, '#7a4a24')]), '#3a1e08', 5), rect(32, 96, 64, 20, 6, '#5a3a24', '#2a1408', 4)]
    icon('slot_gloves', ''.join(gl), d)
    d = D()
    bt = [path('M40 12 L80 12 L80 74 Q112 78 116 100 L116 114 L28 114 L28 96 Q38 90 40 74 Z', d.lin([(0, '#a87048'), (1, '#5a3420')]), '#2a1408', 6),
          rect(36, 12, 48, 16, 4, '#7a4a30', '#2a1408', 4), path('M28 104 L116 104', 'none', '#2a1408', 5), path('M48 34 L48 70', 'none', '#ffffff', 4, opacity=0.3)]
    icon('slot_boots', ''.join(bt), d)
    d = D()
    rg = [circle(64, 76, 34, 'none', '#5a3a08', 18), circle(64, 76, 34, 'none', d.lin([(0, '#fff1a8'), (1, '#c8780a')]), 11),
          poly([(64, 12), (84, 32), (64, 52), (44, 32)], d.lin([(0, '#ffd0f0'), (0.5, '#e04ab0'), (1, '#8a1a6a')]), '#4a0a3a', 5), path('M50 32 L78 32', 'none', '#ffffff', 2, opacity=0.6)]
    icon('slot_ring', ''.join(rg), d)

    # ---- classes
    d = D()
    icon('class_tank', ''.join([path('M64 8 L110 22 Q112 80 64 120 Q16 80 18 22 Z', d.lin([(0, '#8ab4ff'), (1, '#2a4aa0')]), '#0e1a40', 6),
                                path('M64 22 L96 32 Q96 76 64 104 Z', '#ffffff', None, None, opacity=0.2), poly(star_points(64, 58, 20, 9), '#ffe08a', '#6a3a06', 3)]), d)
    d = D()
    blade = lambda rot: g([poly([(60, 6), (68, 6), (70, 80), (64, 90), (58, 80)], d.lin([(0, '#ffffff'), (1, '#9aa4b4')], 0, 0, 1, 0), '#2a3040', 4),
                           rect(42, 80, 44, 10, 4, '#e8c060', '#5a3a08', 3), rect(59, 90, 10, 22, 3, '#6a3a24', '#2a1408', 3)], 'rotate(%d 64 64)' % rot)
    icon('class_warrior', blade(-40) + blade(40), d)
    d = D()
    icon('class_ranger', ''.join([path('M30 14 Q104 30 30 114', 'none', '#3a1e08', 12), path('M30 14 Q104 30 30 114', 'none', '#b07a40', 7),
                                  path('M30 14 L30 114', 'none', '#f2ecd8', 3), path('M24 64 L112 64', 'none', '#5a3a1e', 5),
                                  poly([(112, 64), (96, 54), (98, 74)], '#c8d0dc', '#2a3040', 3), poly([(24, 64), (14, 56), (14, 72)], '#e05a3a', '#5a1a0a', 3)]), d)
    d = D()
    icon('class_mage', ''.join([circle(64, 64, 46, d.rad([(0, '#ffffff', 1), (0.4, '#c08aff', 0.8), (1, '#6a2ad0', 0)])),
                                poly(star_points(64, 64, 44, 14, 4, -90), d.lin([(0, '#ffffff'), (1, '#b07aff')]), '#3a0e7a', 5),
                                poly(star_points(98, 28, 14, 5, 4, -90), '#ffffff', '#3a0e7a', 3), poly(star_points(28, 100, 10, 4, 4, -90), '#ffffff', '#3a0e7a', 3)]), d)
    d = D()
    heart = 'M64 112 C20 80 8 56 20 36 C32 16 56 20 64 38 C72 20 96 16 108 36 C120 56 108 80 64 112 Z'
    icon('class_support', ''.join([path(heart, d.lin([(0, '#ffb0c0'), (1, '#e0304a')]), '#5a0a1a', 6), rect(56, 44, 16, 44, 4, '#ffffff'), rect(42, 58, 44, 16, 4, '#ffffff'),
                                   path('M30 44 Q34 32 46 30', 'none', '#ffffff', 5, opacity=0.6)]), d)

    # ---- statuses (arrow badges)
    def badge(name, color, glyph, up):
        d = D()
        body = [circle(64, 64, 54, d.lin([(0, shade(color, 0.35)), (1, shade(color, -0.35))]), ink(color), 6), glyph]
        arrow = poly([(98, 66), (116, 92), (106, 92), (106, 118), (90, 118), (90, 92), (80, 92)], '#7af07a' if up else '#ff5a5a', '#14280e' if up else '#3a0808', 4)
        if not up:
            arrow = g(arrow, 'rotate(180 98 92)')
        icon(name, ''.join(body) + arrow, d)
    sword_g = g([poly([(60, 22), (68, 22), (70, 76), (64, 84), (58, 76)], '#ffffff', '#2a3040', 3), rect(46, 76, 36, 8, 3, '#e8c060', '#5a3a08', 2.5), rect(60, 84, 8, 16, 3, '#6a3a24')], 'rotate(35 64 64)')
    shield_g = path('M64 26 L92 36 Q94 76 64 100 Q34 76 36 36 Z', '#f2f6ff', '#1e2638', 4)
    boot_g = path('M50 28 L72 28 L72 66 Q94 68 96 84 L96 96 L40 96 L40 84 Q48 80 50 66 Z', '#ffffff', '#2a1408', 4)
    crit_g = poly(star_points(64, 62, 34, 13, 8), '#fff2a0', '#6a2a06', 3)
    for nm, col, gl in (('attack', '#e0503a', sword_g), ('defense', '#3a7ad0', shield_g), ('speed', '#3ab0a0', boot_g), ('crit', '#e0a020', crit_g)):
        badge('status_%s_up' % nm, col, gl, True)
        badge('status_%s_down' % nm, shade(col, -0.25), gl, False)
    d = D()
    stun = [circle(64, 64, 54, d.lin([(0, '#fff3a0'), (1, '#e0a020')]), '#5a3a06', 6)]
    for i, (x, y, r) in enumerate(((42, 50, 16), (84, 48, 13), (64, 84, 15))):
        stun.append(poly(star_points(x, y, r, r * 0.42), '#ffffff', '#5a3a06', 3))
    icon('status_stun', ''.join(stun), d)
    d = D()
    icon('status_shield', ''.join([path('M64 8 L112 24 Q114 84 64 120 Q14 84 16 24 Z', d.lin([(0, '#bfe8ff'), (1, '#2a78d0')]), '#0e2a5a', 6),
                                   path('M64 22 L98 34 Q98 78 64 104 Z', '#ffffff', None, None, opacity=0.3)]), d)
    d = D()
    icon('status_heal', ''.join([circle(64, 64, 54, d.lin([(0, '#b8ffb8'), (1, '#2aa04a')]), '#0a3a14', 6), rect(54, 30, 20, 68, 6, '#ffffff', '#0a3a14', 3),
                                 rect(30, 54, 68, 20, 6, '#ffffff', '#0a3a14', 3)]), d)

    # ---- statuses of the 100-hero roster
    def named(name, color, body):
        d = D()
        icon(name, ''.join([circle(64, 64, 54, d.lin([(0, shade(color, 0.4)), (1, shade(color, -0.35))]), ink(color), 6)] + body), d)
    flame = 'M64 104 C36 100 32 72 48 52 C50 64 58 66 60 58 C58 42 66 30 76 22 C76 40 96 50 94 74 C92 94 80 104 64 104 Z'
    drop = 'M64 22 C80 50 92 64 92 80 C92 98 78 108 64 108 C50 108 36 98 36 80 C36 64 48 50 64 22 Z'
    named('status_burn', '#e8641a', [path(flame, '#ffd36a', '#5a1a04', 4)])
    named('status_poison', '#4aa02a', [path(drop, '#c8ff6a', '#14380a', 4), circle(56, 76, 6, '#14380a'), circle(72, 70, 5, '#14380a')])
    named('status_bleed', '#b0102a', [path(drop, '#ff5a6a', '#3a0408', 4)])
    named('status_regen', '#2aa06a', [path('M64 100 C30 90 30 50 64 28 C98 50 98 90 64 100 Z', '#b8ffd0', '#0a3a20', 4), path('M64 96 L64 46', 'none', '#0a3a20', 4)])
    named('status_taunt', '#6a7a90', [path('M64 24 L94 34 Q96 74 64 98 Q32 74 34 34 Z', '#f2f6ff', '#1e2638', 4), rect(58, 40, 12, 32, 4, '#e0303c'), circle(64, 84, 6, '#e0303c')])
    named('status_silence', '#7a4ab0', [ellipse(64, 66, 30, 18, '#f2e8ff', '#2a0e4a', 4), path('M30 100 L98 30', 'none', '#e0303c', 9)])
    named('status_thorns', '#7a5a2a', [poly(star_points(64, 64, 42, 18, 10), '#c8a060', '#2a1808', 4)])
    named('status_dodge', '#2ab0c0', [path('M30 90 Q60 30 104 38', 'none', '#ffffff', 9), path('M26 64 Q54 22 90 24', 'none', '#ffffff', 6, opacity=0.6)])
    named('status_mark', '#c02a2a', [circle(64, 64, 34, 'none', '#ffffff', 7), circle(64, 64, 16, 'none', '#ffffff', 7), circle(64, 64, 5, '#ffffff')])

    # ---- battle controls & misc
    d = D()
    icon('ui_pause', ''.join([rect(30, 22, 24, 84, 8, '#ffffff', '#1a1a24', 5), rect(74, 22, 24, 84, 8, '#ffffff', '#1a1a24', 5)]), d)
    d = D()
    icon('ui_speed', ''.join([poly([(18, 22), (64, 64), (18, 106)], '#ffffff', '#1a1a24', 5), poly([(64, 22), (110, 64), (64, 106)], '#ffffff', '#1a1a24', 5)]), d)
    d = D()
    icon('ui_auto', ''.join([path('M64 20 A44 44 0 1 1 22 76', 'none', '#1a1a24', 18), path('M64 20 A44 44 0 1 1 22 76', 'none', '#ffffff', 10),
                             poly([(64, 4), (88, 22), (64, 38)], '#ffffff', '#1a1a24', 4), path('M48 84 L64 44 L80 84 M53 72 L75 72', 'none', '#ffffff', 7)]), d)
    d = D()
    icon('ui_skip', ''.join([poly([(16, 22), (62, 64), (16, 106)], '#ffffff', '#1a1a24', 5), poly([(56, 22), (102, 64), (56, 106)], '#ffffff', '#1a1a24', 5),
                             rect(98, 22, 16, 84, 5, '#ffffff', '#1a1a24', 5)]), d)
    d = D()
    icon('ui_lock', ''.join([path('M38 58 L38 42 Q38 14 64 14 Q90 14 90 42 L90 58', 'none', '#2a2a34', 18), path('M38 58 L38 42 Q38 14 64 14 Q90 14 90 42 L90 58', 'none', '#c8ccd8', 10),
                             rect(24, 54, 80, 62, 12, d.lin([(0, '#ffe08a'), (1, '#d08a1a')]), '#4a2a06', 6), circle(64, 80, 9, '#4a2a06'), rect(60, 82, 8, 20, 3, '#4a2a06')]), d)
    d = D()
    icon('ui_check', path('M18 66 L50 98 L112 30', 'none', '#0a3a14', 26) + path('M18 66 L50 98 L112 30', 'none', '#6af07a', 15), d)
    d = D()
    icon('ui_close', path('M28 28 L100 100 M100 28 L28 100', 'none', '#3a0808', 26) + path('M28 28 L100 100 M100 28 L28 100', 'none', '#ff6a6a', 15), d)
    d = D()
    icon('ui_back', poly([(14, 64), (60, 20), (60, 46), (114, 46), (114, 82), (60, 82), (60, 108)], '#ffffff', '#1a1a24', 6), d)
    d = D()
    icon('ui_party', ''.join([circle(40, 46, 18, '#ffd0a8', '#3a1e0a', 5), path('M14 110 Q14 70 40 70 Q66 70 66 110 Z', '#5a8ad0', '#0e1a40', 5),
                              circle(88, 46, 18, '#ffd0a8', '#3a1e0a', 5), path('M62 110 Q62 70 88 70 Q114 70 114 110 Z', '#d05a5a', '#400e0e', 5)]), d)
    d = D()
    icon('ui_boss', ''.join([path('M64 10 C30 10 18 36 22 62 C24 76 34 80 36 92 L36 108 L92 108 L92 92 C94 80 104 76 106 62 C110 36 98 10 64 10 Z', d.lin([(0, '#fff6e4'), (1, '#c8b890')]), '#2a1a14', 6),
                             ellipse(46, 60, 12, 14, '#3a0a0a'), ellipse(82, 60, 12, 14, '#3a0a0a'), circle(46, 62, 4, '#ff3a3a'), circle(82, 62, 4, '#ff3a3a'),
                             poly([(64, 70), (58, 84), (70, 84)], '#3a0a0a'), path('M48 98 L48 108 M58 98 L58 108 M70 98 L70 108 M80 98 L80 108', 'none', '#2a1a14', 3)]), d)
    d = D()
    icon('ui_story', ''.join([path('M14 30 Q14 16 30 16 L98 16 Q114 16 114 30 L114 76 Q114 90 98 90 L56 90 L32 114 L36 90 L30 90 Q14 90 14 76 Z', '#ffffff', '#1a1a24', 6),
                              circle(42, 54, 7, '#1a1a24'), circle(64, 54, 7, '#1a1a24'), circle(86, 54, 7, '#1a1a24')]), d)
    d = D()
    icon('ui_gift', ''.join([rect(16, 50, 96, 66, 8, d.lin([(0, '#ff8aa0'), (1, '#c8304a')]), '#4a0a14', 6), rect(10, 36, 108, 24, 6, '#ff6a80', '#4a0a14', 6),
                             rect(56, 36, 16, 80, 2, '#ffd36a', '#6a3a06', 4), path('M64 36 C40 4 22 28 64 36 C106 28 88 4 64 36', '#ffd36a', '#6a3a06', 5)]), d)


# =========================================================================== logo


def logo():
    W, H = 1400, 560
    font = ImageFont.truetype(FONT, 250)
    words = ('SQUAD', 'FIGHT')
    canvas = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    # text mask (bold via stroke)
    mask = Image.new('L', (W, H), 0)
    md = ImageDraw.Draw(mask)
    y = 40
    pos = []
    for i, wd in enumerate(words):
        bb = md.textbbox((0, 0), wd, font=font, stroke_width=10)
        w = bb[2] - bb[0]
        x = (W - w) // 2 + (-60 if i == 0 else 60)
        pos.append((x - bb[0], y - bb[1]))
        y += 230
    for (x, yy), wd in zip(pos, words):
        md.text((x, yy), wd, font=font, fill=255, stroke_width=10, stroke_fill=255)
    outline = mask.filter(ImageFilter.MaxFilter(31))
    outline2 = mask.filter(ImageFilter.MaxFilter(17))
    shadow = outline.filter(ImageFilter.GaussianBlur(10))
    sh = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    sh.putalpha(shadow.point(lambda v: int(v * 0.6)))
    canvas.alpha_composite(sh, (0, 14))
    canvas.alpha_composite(Image.merge('RGBA', [Image.new('L', (W, H), 30), Image.new('L', (W, H), 14), Image.new('L', (W, H), 40), outline]))
    canvas.alpha_composite(Image.merge('RGBA', [Image.new('L', (W, H), 255), Image.new('L', (W, H), 236), Image.new('L', (W, H), 200), outline2]))
    # gradient fill: gold top -> orange bottom per line
    grad = Image.new('RGBA', (W, H))
    gd = ImageDraw.Draw(grad)
    for yy in range(H):
        t = (yy % 230) / 230
        top, bot = (255, 244, 170), (240, 120, 20)
        c = tuple(int(top[k] + (bot[k] - top[k]) * t) for k in range(3))
        gd.line([(0, yy), (W, yy)], fill=c + (255,))
    fill = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    fill.paste(grad, (0, 0), mask)
    canvas.alpha_composite(fill)
    # shine on the upper half of each glyph
    hl = Image.new('L', (W, H), 0)
    hd = ImageDraw.Draw(hl)
    for (x, yy) in pos:
        hd.rectangle([0, yy + 70, W, yy + 150], fill=110)
    hl = ImageChops.multiply(hl, mask.filter(ImageFilter.MinFilter(9)))
    canvas.alpha_composite(Image.merge('RGBA', [Image.new('L', (W, H), 255)] * 3 + [hl]))
    # crossed swords behind
    d = Defs()
    swords = g([g([poly([(694, 40), (706, 40), (710, 430), (700, 460), (690, 430)], d.lin([(0, '#ffffff'), (1, '#8a94a4')], 0, 0, 1, 0), '#1e2230', 8),
                   rect(640, 430, 120, 22, 8, '#e8c060', '#5a3a08', 6), rect(690, 452, 20, 60, 6, '#6a3a24', '#2a1408', 5)], 'rotate(%d 700 280)' % r) for r in (-38, 38)])
    back = rasterize('<svg xmlns="http://www.w3.org/2000/svg" width="%d" height="%d">%s%s</svg>' % (W, H, d.svg(), swords))
    back.alpha_composite(canvas)
    save(back, os.path.join(UI_DIR, 'logo.png'))


# =========================================================================== app icon


def app_icon():
    """Home-screen icon, 1024 px, opaque and full-bleed (iOS and Android launchers apply their own corner mask).
    The squad (knight up front, two allies behind) over crossed swords on a sunburst."""
    S = 1024
    d = Defs()
    rays = ''.join(poly([(512, 560), (512 + 900 * math.cos(math.radians(a - 7)), 560 + 900 * math.sin(math.radians(a - 7))),
                         (512 + 900 * math.cos(math.radians(a + 7)), 560 + 900 * math.sin(math.radians(a + 7)))], '#ffffff', opacity=0.09)
                   for a in range(0, 360, 30))
    # blades point down behind the squad so the gold hilts show in the top corners
    swords = g([g([poly([(512, 40), (550, 130), (554, 700), (470, 700), (474, 130)], d.lin([(0, '#ffffff'), (0.5, '#d6dce6'), (1, '#7a8494')], 0, 0, 1, 0), '#1e2230', 12),
                   path('M512 120 L512 660', stroke='#9aa4b4', sw=8),
                   rect(392, 690, 240, 46, 16, '#f2c94c', '#5a3a08', 10), rect(490, 736, 44, 104, 12, '#7a4228', '#2a1408', 9),
                   circle(512, 856, 34, '#f2c94c', '#5a3a08', 10)], 'rotate(%d 512 500)' % r) for r in (140, 220)])
    body = (rect(0, 0, S, S, 0, d.rad([(0, '#ffcf5a'), (0.45, '#f07a1e'), (1, '#8a1f2c')], 0.5, 0.55, 0.75)) + rays +
            circle(512, 520, 330, d.rad([(0, '#fff4c0'), (1, '#fff4c0')]), opacity=0.25) + swords)
    img = rasterize('<svg xmlns="http://www.w3.org/2000/svg" width="%d" height="%d">%s%s</svg>' % (S, S, d.svg(), body))

    def hero(hid, size, cx, bottom, dim=0.0):
        p = Image.open(os.path.join(CHAR_DIR, hid, 'portrait.png')).convert('RGBA').resize((size, size), Image.LANCZOS)
        a = p.getchannel('A')
        sh = Image.new('RGBA', p.size, (40, 8, 16, 0))
        sh.putalpha(a.filter(ImageFilter.MaxFilter(15)).filter(ImageFilter.GaussianBlur(8)).point(lambda v: int(v * 0.55)))
        img.alpha_composite(sh, (cx - size // 2, bottom - size + 12))
        if dim:
            p = Image.composite(Image.blend(p, Image.new('RGBA', p.size, (90, 30, 40, 255)), dim), p, a)
        img.alpha_composite(p, (cx - size // 2, bottom - size))

    hero('hero_cindermage', 520, 250, S + 30, dim=0.18)
    hero('hero_archer', 520, 774, S + 30, dim=0.18)
    hero('hero_knight', 720, 512, S + 40)
    # soft vignette so the corners do not look flat under the launcher mask
    vig = Image.new('L', (S, S), 0)
    ImageDraw.Draw(vig).ellipse([-260, -260, S + 260, S + 260], fill=255)
    vig = ImageChops.invert(vig.filter(ImageFilter.GaussianBlur(120))).point(lambda v: int(v * 0.5))
    img.alpha_composite(Image.merge('RGBA', [Image.new('L', (S, S), 30), Image.new('L', (S, S), 4), Image.new('L', (S, S), 10), vig]))
    save(img.convert('RGB'), APP_ICON)


def main():
    os.makedirs(UI_DIR, exist_ok=True)
    os.makedirs(ICON_DIR, exist_ok=True)
    gen_nine_slices()
    for r, c in RARITY.items():
        card_frame(r, c)
        card_back(r, c)
        gem(r, c)
    stars_and_misc()
    gen_icons()
    logo()
    app_icon()
    with open(os.path.join(UI_DIR, 'slices.json'), 'w') as f:
        json.dump(SLICES, f, indent=1)


if __name__ == '__main__':
    main()
