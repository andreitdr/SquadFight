"""Portrait scene backgrounds (1080x1920) for home + each chapter, plus 1024x400 chapter banners cropped from them.

Battle layout: enemies stand in the upper-middle band, heroes in the lower band, so the middle of each scene is a calm
ground plane and the detail lives at the top (sky/horizon) and the edges.
"""
import math
import os
import random

from PIL import Image

from paths import BG_DIR
from svgkit import Defs, circle, ellipse, g, path, poly, rasterize, rect, save, shade, mix, document, star_points

W, H = 1080, 1920


def ridge(rnd, y0, amp, step, rough=0.5, x0=-40, x1=W + 40):
    pts = []
    x = x0
    y = y0
    while x <= x1:
        y = y0 + math.sin(x / (step * 2.3) + rnd.random()) * amp * 0.5 + rnd.uniform(-amp, amp) * rough
        pts.append((x, y))
        x += step
    return pts


def ridge_path(pts, bottom=H + 10):
    d = 'M%.1f %.1f ' % (pts[0][0], bottom)
    d += ' '.join('L%.1f %.1f' % p for p in pts)
    d += ' L%.1f %.1f Z' % (pts[-1][0], bottom)
    return d


def mountains(rnd, y0, peak, n, color, snow=None):
    pts = [(-60, y0)]
    xs = sorted(rnd.uniform(0, W) for _ in range(n))
    out = []
    for x in xs:
        h = rnd.uniform(0.5, 1.0) * peak
        w = rnd.uniform(140, 260)
        pts += [(x - w, y0), (x, y0 - h), (x + w, y0)]
        if snow:
            out.append(poly([(x - w * 0.28, y0 - h * 0.72), (x, y0 - h), (x + w * 0.28, y0 - h * 0.72), (x + w * 0.1, y0 - h * 0.66), (x - 6, y0 - h * 0.74), (x - w * 0.12, y0 - h * 0.64)], snow))
    pts.append((W + 60, y0))
    return path('M' + ' L'.join('%.1f %.1f' % p for p in pts) + ' L%d %d L-60 %d Z' % (W + 60, H, H), color) + ''.join(out)


def tree(x, y, s, c, kind='pine'):
    trunk = rect(x - 7 * s, y - 30 * s, 14 * s, 40 * s, 3, shade(c, -0.5))
    if kind == 'pine':
        layers = []
        for i in range(4):
            yy = y - 30 * s - i * 46 * s
            w = (70 - i * 13) * s
            layers.append(poly([(x - w, yy), (x, yy - 80 * s), (x + w, yy)], shade(c, 0.06 * i)))
        return trunk + ''.join(layers)
    blobs = ''.join(circle(x + dx * s, y - 90 * s + dy * s, r * s, shade(c, k)) for dx, dy, r, k in
                    ((-40, 10, 50, -0.1), (40, 10, 50, -0.1), (0, -30, 62, 0.0), (-20, -10, 44, 0.08), (24, -40, 36, 0.14)))
    return trunk + blobs


def sky(d, top, mid, bottom):
    return rect(0, 0, W, H, 0, d.lin([(0, top), (0.45, mid), (1, bottom)]))


def light_rays(d, cx, cy, color, n=9, op=0.10):
    out = []
    for i in range(n):
        a = math.radians(70 + i * 5)
        a2 = math.radians(72.5 + i * 5)
        out.append(poly([(cx, cy), (cx + math.cos(a) * 2600, cy + math.sin(a) * 2600), (cx + math.cos(a2) * 2600, cy + math.sin(a2) * 2600)], color, None, None, opacity=op))
    return ''.join(out)


def ground(d, y, c1, c2):
    return rect(0, y, W, H - y, 0, d.lin([(0, c1), (1, c2)]))


def greenwood(rnd):
    d = Defs()
    out = [sky(d, '#7cc4ff', '#cfeeff', '#fff2c8')]
    out.append(circle(820, 300, 260, d.rad([(0, '#fffbe0', 0.95), (0.3, '#fff2b0', 0.5), (1, '#fff2b0', 0)])))
    out.append(circle(820, 300, 80, '#fffbe8'))
    for cx, cy, s in ((200, 240, 1.0), (560, 160, 0.7), (950, 520, 0.8)):
        out.append(g([ellipse(cx + dx * s, cy + dy * s, r * s, r * 0.6 * s, '#ffffff', opacity=0.9) for dx, dy, r in ((-60, 10, 70), (0, -10, 90), (70, 10, 70), (20, 20, 80))]))
    out.append(mountains(rnd, 760, 320, 6, '#9cc0d8', '#ffffff'))
    out.append(path(ridge_path(ridge(rnd, 760, 40, 60)), '#7fae8a'))
    for i in range(22):
        x = rnd.uniform(-40, W + 40)
        out.append(tree(x, 800 + rnd.uniform(-20, 20), rnd.uniform(0.6, 0.9), '#4f8a5a'))
    out.append(ground(d, 840, '#8cc46a', '#4f8a3a'))
    # dirt road winding through the battlefield
    out.append(path('M470 840 C430 1000 760 1150 600 1350 C460 1530 300 1700 380 1930 L760 1930 C700 1700 860 1520 900 1350 C960 1120 600 1000 610 840 Z', d.lin([(0, '#d8b880'), (1, '#b08850')]), opacity=0.85))
    for i in range(160):
        x, y = rnd.uniform(0, W), rnd.uniform(860, H)
        s = 0.6 + (y - 860) / 900
        out.append(path('M%.1f %.1f l%.1f %.1f M%.1f %.1f l0 %.1f M%.1f %.1f l%.1f %.1f' % (x, y, -5 * s, -12 * s, x + 4, y, -14 * s, x + 8, y, 5 * s, -11 * s), 'none', rnd.choice(['#6aa850', '#5a9a44', '#7ab858']), 3 * s))
    for i in range(30):
        x, y = rnd.uniform(0, W), rnd.uniform(900, H)
        out.append(circle(x, y, rnd.uniform(4, 7), rnd.choice(['#ffffff', '#ffe060', '#ff8aa0']), opacity=0.9))
    # framing trees
    for x, y, s in ((-30, 1150, 2.6), (1110, 1250, 2.8), (-60, 1950, 3.6), (1140, 1990, 3.4)):
        out.append(tree(x, y, s, '#3e7a48', 'round'))
    out.append(light_rays(d, 820, 300, '#fff6c8'))
    out.append(rect(0, 0, W, H, 0, d.rad([(0, '#000000', 0), (0.7, '#000000', 0.05), (1, '#000000', 0.45)], 0.5, 0.5, 0.75)))
    return d, out


def crypt(rnd):
    d = Defs()
    out = [sky(d, '#0e0a1e', '#2a1e46', '#3a2a50')]
    out.append(circle(260, 300, 300, d.rad([(0, '#d8e8ff', 0.5), (1, '#d8e8ff', 0)])))
    out.append(circle(260, 300, 90, '#eef4ff'))
    out.append(circle(290, 280, 80, '#d8e2f4', opacity=0.6))
    for i in range(80):
        out.append(circle(rnd.uniform(0, W), rnd.uniform(0, 700), rnd.uniform(1, 3), '#ffffff', opacity=rnd.uniform(0.3, 0.9)))
    out.append(path(ridge_path(ridge(rnd, 700, 60, 50)), '#241a38'))
    # ruined crypt facade
    cx = 700
    out.append(rect(cx - 220, 420, 440, 320, 0, '#2e2440'))
    out.append(poly([(cx - 250, 430), (cx, 280), (cx + 250, 430)], '#3a2e50'))
    for i in range(5):
        out.append(rect(cx - 200 + i * 92, 470, 40, 270, 4, '#46395e'))
    out.append(path('M%d 740 L%d 560 Q%d 500 %d 560 L%d 740 Z' % (cx - 50, cx - 50, cx, cx + 50, cx + 50), d.rad([(0, '#7affc8', 0.9), (1, '#1a1030')], 0.5, 0.8, 0.8)))
    out.append(circle(cx, 680, 140, d.rad([(0, '#7affc8', 0.35), (1, '#7affc8', 0)])))
    for x in (cx - 260, cx + 260):
        out.append(rect(x - 30, 380, 60, 360, 6, '#3a2e50'))
    # dead trees
    for x, s in ((80, 1.6), (1000, 1.3)):
        out.append(path('M%.1f 760 L%.1f 520 L%.1f 400 M%.1f 560 L%.1f 470 M%.1f 470 L%.1f 380' % (x, x + 10 * s, x - 30 * s, x + 8 * s, x + 80 * s, x - 14 * s, x + 40 * s), 'none', '#160e22', 18 * s))
    out.append(ground(d, 740, '#3e3450', '#1e1828'))
    # flagstones
    for row in range(14):
        y = 760 + row ** 1.45 * 14
        h = 14 + row * 6
        off = (row % 2) * 60
        for col in range(-1, 10):
            x = col * 120 + off
            out.append(rect(x + 3, y + 3, 114, h - 6, 6, rnd.choice(['#4a405e', '#423852', '#504566']), opacity=0.85))
    # graves at the edges
    for x, y, s in ((60, 1000, 1.2), (1020, 1080, 1.3), (40, 1500, 1.8), (1050, 1600, 1.9), (150, 830, 0.8), (930, 840, 0.8)):
        out.append(path('M%.1f %.1f L%.1f %.1f Q%.1f %.1f %.1f %.1f L%.1f %.1f Z' % (x - 40 * s, y, x - 40 * s, y - 80 * s, x, y - 120 * s, x + 40 * s, y - 80 * s, x + 40 * s, y), '#6a6080', '#1a1424', 5))
        out.append(path('M%.1f %.1f l0 %.1f M%.1f %.1f l%.1f 0' % (x, y - 90 * s, 46 * s, x - 18 * s, y - 72 * s, 36 * s), 'none', '#3a3048', 6 * s))
    # candles + green fog
    for x, y in ((240, 900), (840, 920), (120, 1300), (960, 1340)):
        out.append(rect(x - 8, y - 40, 16, 40, 3, '#e8e0d0'))
        out.append(circle(x, y - 52, 40, d.rad([(0, '#ffcf6a', 0.9), (1, '#ffcf6a', 0)])))
        out.append(ellipse(x, y - 52, 6, 12, '#fff2b0'))
    for i in range(14):
        out.append(ellipse(rnd.uniform(-100, W + 100), rnd.uniform(800, H), rnd.uniform(200, 380), rnd.uniform(40, 80), '#7affc8', opacity=0.06))
    out.append(rect(0, 0, W, H, 0, d.rad([(0, '#000000', 0), (0.6, '#000000', 0.15), (1, '#000000', 0.7)], 0.5, 0.5, 0.75)))
    return d, out


def peak(rnd):
    d = Defs()
    out = [sky(d, '#2a0a10', '#8a2a1a', '#ff8a3a')]
    out.append(mountains(rnd, 820, 520, 5, '#4a1a1e'))
    # the volcano peak with a glowing crater + smoke
    out.append(poly([(200, 820), (480, 230), (600, 230), (900, 820)], d.lin([(0, '#3a1216'), (1, '#5a2020')])))
    out.append(ellipse(540, 232, 70, 18, '#ffcf6a'))
    out.append(circle(540, 230, 200, d.rad([(0, '#ffb04a', 0.8), (1, '#ff5a1a', 0)])))
    for i in range(6):
        out.append(path('M%.1f %.1f q-20 -60 10 -120' % (500 + i * 16, 220), 'none', '#ffcf6a', 7, opacity=0.6))
    for i in range(8):
        out.append(ellipse(540 + rnd.uniform(-160, 160), 130 - i * 14 + rnd.uniform(-30, 30), rnd.uniform(90, 180), rnd.uniform(40, 70), '#3a2a2a', opacity=0.5))
    # lava rivers on the slope
    out.append(path('M540 240 C520 380 600 480 560 620 C540 720 600 780 590 830', 'none', '#ff7a1a', 14, opacity=0.9))
    out.append(path('M540 240 C520 380 600 480 560 620 C540 720 600 780 590 830', 'none', '#ffd36a', 5))
    out.append(path(ridge_path(ridge(rnd, 820, 40, 50)), '#3a1416'))
    out.append(ground(d, 840, '#5a2a22', '#24100e'))
    # cracked basalt with lava seams
    for i in range(26):
        x0, y0 = rnd.uniform(0, W), rnd.uniform(900, H)
        pts = [(x0, y0)]
        for k in range(4):
            pts.append((pts[-1][0] + rnd.uniform(-90, 90), pts[-1][1] + rnd.uniform(10, 60)))
        dd = 'M' + ' L'.join('%.1f %.1f' % p for p in pts)
        out.append(path(dd, 'none', '#ff6a1a', 7, opacity=0.65))
        out.append(path(dd, 'none', '#ffd36a', 2, opacity=0.8))
    for x, y, s in ((40, 1100, 1.4), (1040, 1150, 1.5), (60, 1700, 2.2), (1030, 1760, 2.4)):
        out.append(poly([(x - 80 * s, y), (x - 30 * s, y - 160 * s), (x + 20 * s, y - 120 * s), (x + 70 * s, y)], '#2a1214', '#120808', 5))
    for i in range(70):
        x, y = rnd.uniform(0, W), rnd.uniform(0, H)
        out.append(circle(x, y, rnd.uniform(2, 5), rnd.choice(['#ffcf6a', '#ff8a3a', '#ffe8a0']), opacity=rnd.uniform(0.4, 0.95)))
    out.append(rect(0, 0, W, H, 0, d.rad([(0, '#000000', 0), (0.65, '#000000', 0.12), (1, '#000000', 0.65)], 0.5, 0.5, 0.75)))
    return d, out


def home(rnd):
    d = Defs()
    out = [sky(d, '#3a5ab8', '#a87ad0', '#ffb88a')]
    out.append(circle(540, 900, 520, d.rad([(0, '#fff0c0', 0.8), (1, '#fff0c0', 0)])))
    for i in range(40):
        out.append(circle(rnd.uniform(0, W), rnd.uniform(0, 600), rnd.uniform(1, 2.6), '#ffffff', opacity=rnd.uniform(0.3, 0.8)))
    out.append(mountains(rnd, 1060, 420, 6, '#7a6ab0', '#f4ecff'))
    # castle silhouette on a hill
    out.append(path('M140 1120 Q540 900 940 1120 Z', '#5a4a8a'))
    c = '#4a3a74'
    cx = 540
    out.append(rect(cx - 170, 780, 340, 220, 0, c))
    for x in (cx - 200, cx - 60, cx + 120):
        out.append(rect(x, 680, 80, 320, 0, c))
        out.append(poly([(x - 14, 690), (x + 40, 580), (x + 94, 690)], '#c84a5a'))
        out.append(path('M%d 580 l0 -50 l40 14 l-40 14' % (x + 40), '#ffd36a', '#4a3a74', 3))
    for x in range(cx - 160, cx + 160, 40):
        out.append(rect(x, 760, 22, 26, 0, c))
    for (x, y) in ((cx - 170, 740), (cx - 30, 760), (cx + 150, 740), (cx - 100, 880), (cx + 60, 880)):
        out.append(rect(x, y, 18, 28, 8, '#ffd36a'))
        out.append(circle(x + 9, y + 14, 30, d.rad([(0, '#ffd36a', 0.6), (1, '#ffd36a', 0)])))
    out.append(path(ridge_path(ridge(rnd, 1100, 30, 60)), '#4a6a8a'))
    out.append(ground(d, 1140, '#5a8a6a', '#24402e'))
    for i in range(14):
        out.append(tree(rnd.uniform(-40, W + 40), 1160 + rnd.uniform(-10, 30), rnd.uniform(0.7, 1.1), '#2e5a44'))
    for x, y, s in ((-40, 1700, 3.2), (1120, 1760, 3.4)):
        out.append(tree(x, y, s, '#24483a', 'round'))
    out.append(rect(0, 0, W, H, 0, d.rad([(0, '#000000', 0), (0.65, '#000000', 0.1), (1, '#000000', 0.6)], 0.5, 0.45, 0.8)))
    return d, out


SCENES = {'home': home, 'chapter_1': greenwood, 'chapter_2': crypt, 'chapter_3': peak}
BANNER_Y = {'home': 520, 'chapter_1': 300, 'chapter_2': 360, 'chapter_3': 160}


# Chapters 4-40 reuse the three scenes (re-seeded per chapter) with an act colour grade: (scene, hue, strength, saturation, brightness).
ACT_LOOK = {1: (peak, 0.07, 0.5, 0.35, 0.75), 2: (crypt, 0.52, 0.6, 0.8, 0.9), 3: (greenwood, 0.22, 0.5, 0.6, 0.75), 4: (peak, 0.11, 0.7, 0.7, 1.1),
            5: (greenwood, 0.55, 0.6, 0.3, 1.15), 6: (peak, 0.04, 0.3, 1.0, 0.9), 7: (crypt, 0.97, 0.7, 0.9, 0.8), 8: (crypt, 0.72, 0.5, 0.35, 0.7)}


def grade(img, hue, strength, sat, bright):
    import numpy as np
    hsv = np.asarray(img.convert('HSV')).astype(np.float32) / 255.0
    dh = ((hue - hsv[..., 0] + 0.5) % 1.0) - 0.5
    hsv[..., 0] = (hsv[..., 0] + dh * strength) % 1.0
    hsv[..., 1] = np.clip(hsv[..., 1] * sat, 0, 1)
    hsv[..., 2] = np.clip(hsv[..., 2] * bright, 0, 1)
    chans = (hsv * 255).astype('uint8')
    return Image.merge('HSV', [Image.fromarray(chans[..., i]) for i in range(3)]).convert('RGB')


def chapter_scenes():
    import sys
    sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', 'content'))
    from campaign_design import ACTS
    from campaign_design_late import ACTS_LATE
    n = 0
    for act in ACTS + ACTS_LATE:
        for _ in act['chapters']:
            n += 1
            if n > 3:
                yield 'chapter_%d' % n, act['num']


def main():
    os.makedirs(BG_DIR, exist_ok=True)
    for name, a in chapter_scenes():
        fn, hue, strength, sat, bright = ACT_LOOK[a]
        d, body = fn(random.Random(name))
        img = grade(rasterize(document(''.join(body), W, H, defs=d)).convert('RGB'), hue, strength, sat, bright)
        img.save(os.path.join(BG_DIR, name + '.jpg'), quality=82, optimize=True, progressive=False)
        y = BANNER_Y['chapter_%d' % {peak: 3, crypt: 2, greenwood: 1}[fn]]
        img.crop((0, y, W, y + int(W * 400 / 1024))).resize((1024, 400), Image.LANCZOS).save(os.path.join(BG_DIR, name + '_banner.jpg'), quality=85, optimize=True)
    if '--chapters-only' in os.sys.argv:
        return
    for name, fn in SCENES.items():
        d, body = fn(random.Random(name))
        img = rasterize(document(''.join(body), W, H, defs=d)).convert('RGB')
        img.save(os.path.join(BG_DIR, name + '.jpg'), quality=88, optimize=True, progressive=False)
        y = BANNER_Y[name]
        banner = img.crop((0, y, W, y + int(W * 400 / 1024))).resize((1024, 400), Image.LANCZOS)
        banner.save(os.path.join(BG_DIR, name + '_banner.jpg'), quality=88, optimize=True)


if __name__ == '__main__':
    main()
