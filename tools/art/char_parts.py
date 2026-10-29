"""Vector body parts for the chibi character rig.

Canvas: 400x400 units, character centred at x=200, feet on y~376. Every part function returns SVG markup in the
coordinate space of its rig bone (see char_rig.py). Fills use soft gradients + a dark ink outline so the sprites read
well at phone size.
"""
import math

from svgkit import (capsule, circle, ellipse, g, ink, mix, path, poly, rect, rounded_poly, shade, star_points)

OUT = 5.0  # outline width (canvas units)

BODY = {
    'human': dict(hx=200, hy=140, hrx=64, hry=62, sy=224, sx=44, ty=204, hipy=300, hipx=17, footy=376, arm=64, limb=15, wt=40, wb=34, scale=1.0),
    'slim': dict(hx=200, hy=140, hrx=62, hry=60, sy=224, sx=40, ty=204, hipy=300, hipx=15, footy=376, arm=64, limb=13, wt=35, wb=30, scale=1.0),
    'brute': dict(hx=200, hy=146, hrx=58, hry=56, sy=226, sx=62, ty=200, hipy=304, hipx=24, footy=376, arm=74, limb=21, wt=60, wb=48, scale=1.0),
    'goblin': dict(hx=200, hy=168, hrx=66, hry=58, sy=246, sx=36, ty=230, hipy=314, hipx=15, footy=376, arm=52, limb=12, wt=32, wb=34, scale=1.0),
    'fat': dict(hx=200, hy=150, hrx=62, hry=58, sy=232, sx=56, ty=210, hipy=316, hipx=24, footy=376, arm=64, limb=19, wt=58, wb=64, scale=1.0),
    'skeleton': dict(hx=200, hy=140, hrx=58, hry=58, sy=224, sx=40, ty=204, hipy=300, hipx=16, footy=376, arm=66, limb=8, wt=36, wb=26, scale=1.0),
}


class Painter:
    """Shading helpers bound to one Defs instance."""

    def __init__(self, defs):
        self.d = defs

    def fill(self, c, light=0.28, dark=-0.22, angle='diag'):
        if angle == 'v':
            return self.d.lin([(0, shade(c, light)), (0.55, c), (1, shade(c, dark))], 0, 0, 0, 1)
        return self.d.lin([(0, shade(c, light)), (0.5, c), (1, shade(c, dark))], 0.15, 0, 0.85, 1)

    def metal(self, c):
        return self.d.lin([(0, shade(c, 0.55)), (0.35, shade(c, 0.1)), (0.5, shade(c, -0.25)), (0.62, shade(c, 0.25)), (1, shade(c, -0.3))], 0, 0, 1, 1)

    def shape(self, d, c, sw=OUT, kind='fill', **kw):
        f = self.metal(c) if kind == 'metal' else self.fill(c) if kind == 'fill' else c
        return path(d, f, ink(c), sw, **kw)

    def ell(self, cx, cy, rx, ry, c, sw=OUT, kind='fill', **kw):
        f = self.metal(c) if kind == 'metal' else self.fill(c) if kind == 'fill' else c
        return ellipse(cx, cy, rx, ry, f, ink(c), sw, **kw)

    def cap(self, x1, y1, x2, y2, r1, r2, c, sw=OUT, kind='fill'):
        return self.shape(capsule(x1, y1, x2, y2, r1, r2), c, sw, kind)

    def glow(self, cx, cy, r, c, op=0.8):
        return circle(cx, cy, r, self.d.rad([(0, c, op), (0.45, c, op * 0.45), (1, c, 0)]))

    def shine(self, d, op=0.35, w=4):
        return path(d, 'none', '#ffffff', w, opacity=op)


# =========================================================================== HEAD


def head_path(B, kind='human'):
    x, y, rx, ry = B['hx'], B['hy'], B['hrx'], B['hry']
    if kind == 'goblin':
        rx2 = rx * 1.05
        return ('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (
            x - rx2, y, x - rx2, y - ry * 0.8, x - rx * 0.55, y - ry, x, y - ry,
            x + rx * 0.55, y - ry, x + rx2, y - ry * 0.8, x + rx2, y,
            x + rx2, y + ry * 0.75, x + rx * 0.5, y + ry, x, y + ry,
            x - rx * 0.5, y + ry, x - rx2, y + ry * 0.75, x - rx2, y))
    if kind == 'brute':
        return rounded_poly([(x - rx, y - ry * 0.7), (x - rx * 0.6, y - ry), (x + rx * 0.6, y - ry), (x + rx, y - ry * 0.7),
                             (x + rx * 1.05, y + ry * 0.45), (x + rx * 0.6, y + ry), (x - rx * 0.6, y + ry), (x - rx * 1.05, y + ry * 0.45)], 26)
    return ('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (
        x - rx, y, x - rx, y - ry * 0.75, x - rx * 0.55, y - ry, x, y - ry,
        x + rx * 0.55, y - ry, x + rx, y - ry * 0.75, x + rx, y,
        x + rx, y + ry * 0.62, x + rx * 0.45, y + ry, x, y + ry,
        x - rx * 0.45, y + ry, x - rx, y + ry * 0.62, x - rx, y))


def eyes(P, B, S, expr):
    x, y = B['hx'], B['hy'] + B['hry'] * 0.2
    dx = B['hrx'] * 0.38
    out = []
    iris = S.get('eye', '#4a6fa5')
    style = S.get('eye_style', 'normal')
    lash = '#2a1a22'
    if style == 'glow':
        for sx in (-1, 1):
            out.append(P.glow(x + sx * dx, y, 20, iris, 0.9))
            out.append(ellipse(x + sx * dx, y, 9, 6 if expr != 'angry' else 4.5, '#ffffff'))
        return ''.join(out)
    if style == 'socket':
        for sx in (-1, 1):
            out.append(ellipse(x + sx * dx, y - 2, 15, 17, '#1c1020'))
            if expr not in ('dead',):
                out.append(P.glow(x + sx * dx, y, 15, iris, 0.95))
                out.append(circle(x + sx * dx, y, 4.5, '#ffffff'))
        return ''.join(out)
    for sx in (-1, 1):
        ex = x + sx * dx
        if expr == 'dead':
            out.append(path('M%.1f %.1f L%.1f %.1f M%.1f %.1f L%.1f %.1f' % (ex - 9, y - 9, ex + 9, y + 9, ex - 9, y + 9, ex + 9, y - 9), 'none', lash, 5))
        elif expr == 'hurt':
            out.append(path('M%.1f %.1f L%.1f %.1f L%.1f %.1f' % (ex - sx * 10, y - 9, ex + sx * 7, y, ex - sx * 10, y + 9), 'none', lash, 5))
        elif expr in ('happy', 'blink'):
            arc = 'M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (ex - 11, y + 3, ex, y - 12 if expr == 'happy' else y + 6, ex + 11, y + 3)
            out.append(path(arc, 'none', lash, 5))
        else:
            ry = 13.5 if style != 'fierce' else 11
            if expr in ('angry', 'cast'):
                ry -= 2
            out.append(ellipse(ex, y, 10.5, ry, '#ffffff', lash, 2.5))
            out.append(ellipse(ex, y + 1.5, 8.5, ry - 2.2, P.d.lin([(0, shade(iris, -0.55)), (0.5, iris), (1, shade(iris, 0.45))], 0, 0, 0, 1)))
            out.append(ellipse(ex, y + 2, 4.2, ry * 0.45, '#140c18'))
            out.append(circle(ex - 3.5, y - ry * 0.35, 3.6, '#ffffff'))
            out.append(circle(ex + 3.2, y + ry * 0.4, 1.7, '#ffffff', opacity=0.85))
            # upper lash line
            out.append(path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (ex - 12, y - ry * 0.55, ex, y - ry - 4, ex + 12, y - ry * 0.55), 'none', lash, 4.5))
            if S.get('lashes'):
                out.append(path('M%.1f %.1f l%.1f %.1f' % (ex + sx * 11, y - ry * 0.6, sx * 5, -4), 'none', lash, 3))
        if expr == 'cast' and style != 'glow':
            out.append(P.glow(ex, y, 14, S.get('aura', '#9fdcff'), 0.45))
    return ''.join(out)


def brows(B, S, expr):
    if S.get('no_brows'):
        return ''
    x, y = B['hx'], B['hy'] - B['hry'] * 0.12
    dx = B['hrx'] * 0.38
    c = shade(S.get('hair_color', '#4a3020'), -0.25)
    out = []
    for sx in (-1, 1):
        ex = x + sx * dx
        if expr in ('angry', 'cast'):
            d = 'M%.1f %.1f L%.1f %.1f' % (ex - sx * 12, y - 7, ex + sx * 9, y + 1)
        elif expr == 'hurt':
            d = 'M%.1f %.1f L%.1f %.1f' % (ex - sx * 11, y + 2, ex + sx * 9, y - 6)
        else:
            d = 'M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (ex - 10, y - 1, ex, y - 7, ex + 10, y - 1)
        out.append(path(d, 'none', c, 5 if not S.get('thick_brows') else 8))
    return ''.join(out)


def mouth(B, S, expr):
    x, y = B['hx'], B['hy'] + B['hry'] * 0.62
    lip = '#5a2430'
    style = S.get('mouth', 'normal')
    if style == 'none':
        return ''
    if style == 'fangs' or style == 'tusks':
        w = 18
        base = path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x - w, y - 2, x, y + (10 if expr != 'hurt' else 2), x + w, y - 2), '#3a1420', lip, 3.5)
        if style == 'fangs':
            return base + poly([(x - 10, y), (x - 6, y + 9), (x - 3, y + 1)], '#fff8e8') + poly([(x + 10, y), (x + 6, y + 9), (x + 3, y + 1)], '#fff8e8')
        return base
    if expr in ('angry', 'cast'):
        return path('M%.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (x - 11, y - 2, x, y + 12, x + 11, y - 2), '#7a2a38', lip, 3.5) + \
            rect(x - 8, y - 2, 16, 4, 1, '#ffffff')
    if expr == 'happy':
        return path('M%.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (x - 12, y - 3, x, y + 15, x + 12, y - 3), '#b0404e', lip, 3.5) + \
            ellipse(x, y + 6, 5, 3, '#f08a96')
    if expr == 'hurt':
        return path('M%.1f %.1f q4 -5 8 0 t8 0 t8 0' % (x - 12, y + 3), 'none', lip, 3.5)
    if expr == 'dead':
        return path('M%.1f %.1f L%.1f %.1f' % (x - 8, y + 3, x + 8, y + 3), 'none', lip, 3.5)
    if style == 'smirk':
        return path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x - 9, y + 2, x + 2, y + 7, x + 10, y - 3), 'none', lip, 4)
    return path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x - 8, y + 1, x, y + 7, x + 8, y + 1), 'none', lip, 4)


def ears(P, B, S):
    kind = S.get('ears', 'human')
    x, y, rx = B['hx'], B['hy'] + 8, B['hrx']
    sk = S['skin']
    out = []
    for sx in (-1, 1):
        ex = x + sx * (rx - 4)
        if kind == 'human':
            out.append(P.ell(ex + sx * 4, y, 11, 15, sk))
        elif kind == 'elf':
            out.append(P.shape('M%.1f %.1f L%.1f %.1f L%.1f %.1f Z' % (ex, y - 12, ex + sx * 34, y - 34, ex, y + 14), sk))
        elif kind == 'goblin':
            out.append(P.shape('M%.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (
                ex, y - 22, ex + sx * 40, y - 30, ex + sx * 70, y - 34, ex + sx * 40, y + 6, ex, y + 14), sk))
            out.append(path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (ex + sx * 10, y - 10, ex + sx * 36, y - 18, ex + sx * 56, y - 28), 'none', shade(sk, -0.3), 4))
        elif kind == 'wolf':
            pass
    return ''.join(out)


def face(P, B, S, expr):
    x, y, rx, ry = B['hx'], B['hy'], B['hrx'], B['hry']
    kind = S.get('head', 'human')
    sk = S['skin']
    out = []
    if kind == 'skull':
        bone = S.get('bone', '#ece4cf')
        out.append(P.shape('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f L%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (
            x - rx, y + 4, x - rx, y - ry * 1.1, x + rx, y - ry * 1.1, x + rx, y + 4,
            x + rx, y + ry * 0.5, x + rx * 0.55, y + ry * 0.55, x + rx * 0.45, y + ry, x - rx * 0.45, y + ry,
            x - rx * 0.55, y + ry * 0.55, x - rx, y + ry * 0.5, x - rx, y + 4), bone))
        out.append(path('M%.1f %.1f L%.1f %.1f' % (x - rx * 0.4, y + ry * 0.62, x + rx * 0.4, y + ry * 0.62), 'none', ink(bone), 3))
        for i in range(-3, 4):
            out.append(path('M%.1f %.1f L%.1f %.1f' % (x + i * 9, y + ry * 0.5, x + i * 9, y + ry * 0.95), 'none', ink(bone), 2.5))
        out.append(poly([(x, y + 14), (x - 7, y + 28), (x + 7, y + 28)], '#2a1a24'))
        out.append(path('M%.1f %.1f q10 -8 22 -2' % (x - rx * 0.75, y - ry * 0.55), 'none', '#ffffff', 4, opacity=0.5))
        out.append(path('M%.1f %.1f l10 14 l-4 10' % (x + rx * 0.3, y - ry * 0.9), 'none', ink(bone), 3))
        out.append(eyes(P, B, dict(S, eye_style='socket'), expr))
        return ''.join(out)
    out.append(ears(P, B, S))
    out.append(P.shape(head_path(B, 'goblin' if kind == 'goblin' else 'brute' if kind == 'brute' else 'human'), sk))
    # soft cheek shading + blush
    out.append(ellipse(x + rx * 0.35, y + ry * 0.55, rx * 0.55, ry * 0.35, shade(sk, -0.35), opacity=0.18))
    if S.get('blush', True):
        for sx in (-1, 1):
            out.append(ellipse(x + sx * rx * 0.58, y + ry * 0.45, 11, 6.5, '#ff7a8a', opacity=0.35))
    if S.get('freckles'):
        for sx in (-1, 1):
            for k in range(3):
                out.append(circle(x + sx * (rx * 0.5 + k * 5), y + ry * 0.36 + (k % 2) * 4, 1.6, shade(sk, -0.4)))
    if kind == 'goblin':
        out.append(P.ell(x, y + ry * 0.38, 13, 10, shade(sk, -0.08)))
    elif kind == 'brute':
        out.append(path('M%.1f %.1f q8 6 16 0' % (x - 8, y + ry * 0.36), 'none', ink(sk), 3.5))
    else:
        out.append(path('M%.1f %.1f q3 3 6 0' % (x - 3, y + ry * 0.4), 'none', shade(sk, -0.35), 3))
    if S.get('scar'):
        out.append(path('M%.1f %.1f l14 22' % (x + rx * 0.25, y - ry * 0.1), 'none', shade(sk, -0.4), 3.5))
    out.append(eyes(P, B, S, expr))
    out.append(brows(B, S, expr))
    out.append(mouth(B, S, expr))
    if S.get('mouth') == 'tusks':
        for sx in (-1, 1):
            out.append(P.shape('M%.1f %.1f Q%.1f %.1f %.1f %.1f L%.1f %.1f Z' % (
                x + sx * 13, y + ry * 0.68, x + sx * 18, y + ry * 0.25, x + sx * 14, y + ry * 0.15, x + sx * 7, y + ry * 0.66), '#fff4dc', 3))
    if S.get('beard'):
        bc = S.get('hair_color', '#6b4a30')
        out.append(P.shape('M%.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (
            x - rx * 0.85, y + ry * 0.25, x - rx * 0.7, y + ry * 1.45, x, y + ry * 1.5,
            x + rx * 0.7, y + ry * 1.45, x + rx * 0.85, y + ry * 0.25, x, y + ry * 0.95, x - rx * 0.85, y + ry * 0.25), bc))
    if S.get('mask'):
        mc = S['mask']
        out.append(P.shape('M%.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (
            x - rx * 0.98, y + ry * 0.28, x, y + ry * 0.18, x + rx * 0.98, y + ry * 0.28,
            x + rx * 0.6, y + ry * 1.04, x, y + ry * 1.02, x - rx * 0.6, y + ry * 1.04, x - rx * 0.98, y + ry * 0.28), mc))
    return ''.join(out)


# --------------------------------------------------------------------------- hair


def _fringe(x0, x1, y_top, y_base, n, depth, jag=0.5):
    """Scalloped/jagged fringe path points from x0 to x1."""
    pts = []
    for i in range(n + 1):
        t = i / n
        xx = x0 + (x1 - x0) * t
        yy = y_base + (depth if i % 2 else 0) * (1 if 0 < i < n else 0.3)
        pts.append((xx, yy))
    return pts


def hair_back(P, B, S):
    st = S.get('hair', 'none')
    c = S.get('hair_color', '#5a3a28')
    x, y, rx, ry = B['hx'], B['hy'], B['hrx'], B['hry']
    if st in ('long', 'long_wavy'):
        w = 'M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f L%.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f L%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (
            x - rx - 6, y, x - rx - 8, y - ry * 1.2, x + rx + 8, y - ry * 1.2, x + rx + 6, y,
            x + rx + 14, y + ry * 1.9, x + rx * 0.5, y + ry * 2.05, x, y + ry * 1.75,
            x - rx * 0.5, y + ry * 2.05, x - rx - 14, y + ry * 1.9,
            x - rx - 6, y, x - rx - 6, y, x - rx - 6, y, x - rx - 6, y)
        return P.shape(w, c)
    if st == 'ponytail':
        return P.shape('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (
            x + rx * 0.5, y - ry * 0.7, x + rx * 1.8, y - ry * 0.9, x + rx * 1.7, y + ry * 1.2, x + rx * 1.05, y + ry * 1.75,
            x + rx * 1.25, y + ry * 0.9, x + rx * 1.0, y - ry * 0.2, x + rx * 0.5, y - ry * 0.7), c) + \
            P.ell(x + rx * 0.72, y - ry * 0.62, 10, 10, S.get('tie', '#d04050'))
    if st == 'bob':
        return P.shape('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f L%.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (
            x - rx - 8, y + 10, x - rx - 10, y - ry * 1.25, x + rx + 10, y - ry * 1.25, x + rx + 8, y + 10,
            x + rx + 6, y + ry * 0.95, x, y + ry * 1.25, x - rx - 6, y + ry * 0.95), c)
    if st == 'mane':
        out = []
        for i in range(9):
            a = math.radians(-180 + i * 22.5)
            px, py = x + math.cos(a) * rx * 1.15, y + math.sin(a) * ry * 1.05 + 10
            out.append(P.ell(px, py, 24, 24, c))
        return ''.join(out)
    return ''


def hair_front(P, B, S):
    st = S.get('hair', 'none')
    if st in ('none', 'bald'):
        return ''
    c = S.get('hair_color', '#5a3a28')
    x, y, rx, ry = B['hx'], B['hy'], B['hrx'], B['hry']
    top = y - ry - 6
    if st == 'spiky':
        pts = [(x - rx - 4, y + 4), (x - rx - 22, y - ry * 0.3), (x - rx * 0.8, y - ry * 0.55), (x - rx * 0.95, y - ry * 1.15),
               (x - rx * 0.4, y - ry * 0.95), (x - rx * 0.2, y - ry * 1.4), (x + rx * 0.15, y - ry * 1.0), (x + rx * 0.55, y - ry * 1.35),
               (x + rx * 0.65, y - ry * 0.85), (x + rx * 1.15, y - ry * 0.95), (x + rx * 0.9, y - ry * 0.45), (x + rx + 20, y - ry * 0.2),
               (x + rx + 4, y + 4), (x + rx * 0.75, y - ry * 0.35), (x + rx * 0.45, y - ry * 0.12), (x + rx * 0.2, y - ry * 0.42),
               (x - rx * 0.05, y - ry * 0.15), (x - rx * 0.3, y - ry * 0.45), (x - rx * 0.55, y - ry * 0.1), (x - rx * 0.75, y - ry * 0.35)]
        return P.shape(rounded_poly(pts, 4), c) + P.shine('M%.1f %.1f q20 -14 44 -10' % (x - rx * 0.5, y - ry * 0.85))
    if st == 'messy':
        pts = [(x - rx - 6, y + 12), (x - rx - 14, y - ry * 0.5), (x - rx * 0.7, y - ry * 1.12), (x - rx * 0.1, y - ry * 1.22),
               (x + rx * 0.6, y - ry * 1.15), (x + rx + 12, y - ry * 0.6), (x + rx + 6, y + 12), (x + rx * 0.82, y - ry * 0.25),
               (x + rx * 0.62, y - ry * 0.05), (x + rx * 0.45, y - ry * 0.4), (x + rx * 0.1, y - ry * 0.1), (x - rx * 0.12, y - ry * 0.45),
               (x - rx * 0.4, y - ry * 0.05), (x - rx * 0.6, y - ry * 0.4), (x - rx * 0.82, y - ry * 0.15)]
        return P.shape(rounded_poly(pts, 6), c) + P.shine('M%.1f %.1f q18 -12 40 -8' % (x - rx * 0.45, y - ry * 0.85))
    if st == 'swept':
        pts = [(x - rx - 6, y + 14), (x - rx - 8, y - ry * 0.7), (x - rx * 0.3, y - ry * 1.2), (x + rx * 0.7, y - ry * 1.1),
               (x + rx + 8, y - ry * 0.5), (x + rx + 6, y + 14), (x + rx * 0.85, y - ry * 0.25), (x + rx * 0.2, y - ry * 0.55),
               (x - rx * 0.4, y - ry * 0.2), (x - rx * 0.85, y - ry * 0.05)]
        return P.shape(rounded_poly(pts, 10), c) + P.shine('M%.1f %.1f q24 -16 52 -8' % (x - rx * 0.45, y - ry * 0.85))
    # default fringe styles: short, long, ponytail, bob
    n = 7
    base = y - ry * 0.18
    pts = [(x - rx - 5, y + (22 if st in ('long', 'bob', 'long_wavy') else 6))]
    pts.append((x - rx - 8, y - ry * 0.7))
    pts.append((x - rx * 0.5, top))
    pts.append((x + rx * 0.5, top))
    pts.append((x + rx + 8, y - ry * 0.7))
    pts.append((x + rx + 5, y + (22 if st in ('long', 'bob', 'long_wavy') else 6)))
    fr = []
    for i in range(n + 1):
        t = i / n
        xx = x + rx * 0.95 - t * rx * 1.9
        yy = base + (16 if i % 2 else 0) * (1 if 0 < i < n else 0) - 6 * math.sin(t * math.pi)
        fr.append((xx, yy))
    pts += fr
    return P.shape(rounded_poly(pts, 7), c) + P.shine('M%.1f %.1f q22 -14 48 -9' % (x - rx * 0.5, y - ry * 0.8))


# --------------------------------------------------------------------------- headgear


def headgear_back(P, B, S):
    hg = S.get('headgear')
    x, y, rx, ry = B['hx'], B['hy'], B['hrx'], B['hry']
    c = S.get('headgear_color', '#7a4a9a')
    if hg == 'hood' or hg == 'hood_up':
        return P.shape('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f L%.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (
            x - rx - 16, y + ry * 0.9, x - rx - 26, y - ry * 1.5, x + rx + 26, y - ry * 1.5, x + rx + 16, y + ry * 0.9,
            x + rx * 0.7, y + ry * 1.25, x, y + ry * 1.35, x - rx * 0.7, y + ry * 1.25), c)
    if hg == 'veil':
        return P.shape('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f L%.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (
            x - rx - 10, y, x - rx - 14, y - ry * 1.35, x + rx + 14, y - ry * 1.35, x + rx + 10, y,
            x + rx + 22, y + ry * 2.0, x, y + ry * 2.15, x - rx - 22, y + ry * 2.0), c)
    return ''


def headgear_front(P, B, S):
    hg = S.get('headgear')
    if not hg:
        return ''
    x, y, rx, ry = B['hx'], B['hy'], B['hrx'], B['hry']
    c = S.get('headgear_color', '#9aa3b5')
    acc = S.get('headgear_accent', '#e8b84a')
    out = []
    if hg == 'helm_knight':
        out.append(P.shape('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f L%.1f %.1f L%.1f %.1f Q%.1f %.1f %.1f %.1f L%.1f %.1f Z' % (
            x - rx - 8, y + 18, x - rx - 10, y - ry * 1.35, x + rx + 10, y - ry * 1.35, x + rx + 8, y + 18,
            x + rx - 6, y + 22, x + rx - 8, y - ry * 0.2, x, y - ry * 0.42, x - rx + 8, y - ry * 0.2, x - rx + 6, y + 22), c, kind='metal'))
        out.append(P.shape('M%.1f %.1f L%.1f %.1f L%.1f %.1f L%.1f %.1f Z' % (x - 7, y - ry * 0.45, x + 7, y - ry * 0.45, x + 5, y + 4, x - 5, y + 4), c, 4, 'metal'))
        out.append(path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x - rx * 0.6, y - ry * 0.95, x, y - ry * 1.15, x + rx * 0.6, y - ry * 0.95), 'none', '#ffffff', 5, opacity=0.45))
        plume = S.get('plume', '#c8323c')
        out.append(P.shape('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (
            x - 6, y - ry - 2, x - 10, y - ry - 60, x + 50, y - ry - 70, x + 70, y - ry - 20,
            x + 40, y - ry - 40, x + 14, y - ry - 30, x + 8, y - ry - 2), plume))
        for i in range(4):
            out.append(rect(x - rx * 0.62 + i * rx * 0.41, y - ry * 0.32, 5, 5, 2, acc))
    elif hg == 'wizard_hat':
        out.append(P.shape('M%.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (
            x - rx - 42, y - ry * 0.45, x, y - ry * 0.95, x + rx + 42, y - ry * 0.45, x, y - ry * 0.1, x - rx - 42, y - ry * 0.45), c))
        out.append(P.shape('M%.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (
            x - rx * 0.78, y - ry * 0.55, x - rx * 0.2, y - ry * 1.9, x + rx * 0.7, y - ry * 2.45,
            x + rx * 0.45, y - ry * 1.6, x + rx * 0.78, y - ry * 0.55, x, y - ry * 0.72, x - rx * 0.78, y - ry * 0.55), c))
        out.append(path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x - rx * 0.76, y - ry * 0.68, x, y - ry * 0.88, x + rx * 0.76, y - ry * 0.68), 'none', acc, 9))
        out.append(poly(star_points(x - rx * 0.1, y - ry * 1.3, 11, 5), acc, ink(acc), 2.5))
        out.append(poly(star_points(x + rx * 0.4, y - ry * 1.85, 7, 3), acc))
    elif hg == 'hood' or hg == 'hood_up':
        out.append(P.shape('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f L%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (
            x - rx - 14, y + ry * 0.7, x - rx - 18, y - ry * 1.55, x + rx + 18, y - ry * 1.55, x + rx + 14, y + ry * 0.7,
            x + rx - 4, y + ry * 0.4, x + rx * 0.9, y - ry * 0.75, x - rx * 0.9, y - ry * 0.75, x - rx + 4, y + ry * 0.4), c))
        out.append(path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x - rx * 0.6, y - ry * 1.0, x, y - ry * 1.25, x + rx * 0.6, y - ry * 1.0), 'none', '#ffffff', 4, opacity=0.25))
    elif hg == 'hood_shadow':  # cultist: face hidden in darkness
        out.append(P.shape('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (
            x - rx - 14, y + ry * 1.0, x - rx - 24, y - ry * 1.7, x + rx + 24, y - ry * 1.7, x + rx + 14, y + ry * 1.0,
            x + rx * 0.4, y + ry * 1.15, x, y + ry * 1.2, x - rx * 0.4, y + ry * 1.15, x - rx - 14, y + ry * 1.0), c))
        out.append(ellipse(x, y + 10, rx * 0.78, ry * 0.85, P.d.rad([(0, '#05020a'), (0.8, '#120818'), (1, '#120818', 0.0)])))
    elif hg == 'veil':
        out.append(P.shape('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f L%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (
            x - rx - 10, y + 8, x - rx - 14, y - ry * 1.4, x + rx + 14, y - ry * 1.4, x + rx + 10, y + 8,
            x + rx - 2, y + 6, x + rx * 0.9, y - ry * 0.85, x - rx * 0.9, y - ry * 0.85, x - rx + 2, y + 6), c))
        out.append(path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x - rx * 0.92, y - ry * 0.55, x, y - ry * 0.95, x + rx * 0.92, y - ry * 0.55), 'none', acc, 9))
        out.append(P.glow(x, y - ry * 0.78, 16, acc, 0.6))
        out.append(poly(star_points(x, y - ry * 0.78, 10, 4, 4, -45), '#fffbe6', ink(acc), 2))
    elif hg == 'circlet':
        out.append(path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x - rx * 0.98, y - ry * 0.3, x, y - ry * 0.75, x + rx * 0.98, y - ry * 0.3), 'none', ink(acc), 11))
        out.append(path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x - rx * 0.98, y - ry * 0.3, x, y - ry * 0.75, x + rx * 0.98, y - ry * 0.3), 'none', acc, 6))
        gem = S.get('gem', '#58c4ff')
        out.append(P.glow(x, y - ry * 0.55, 18, gem, 0.7))
        out.append(poly([(x, y - ry * 0.75), (x + 9, y - ry * 0.55), (x, y - ry * 0.35), (x - 9, y - ry * 0.55)], gem, ink(gem), 3))
    elif hg == 'feather_cap':
        out.append(P.shape('M%.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (
            x - rx - 12, y - ry * 0.35, x - rx * 0.6, y - ry * 1.5, x + rx * 0.4, y - ry * 1.25,
            x + rx + 26, y - ry * 1.05, x + rx + 14, y - ry * 0.35, x, y - ry * 0.6, x - rx - 12, y - ry * 0.35), c))
        out.append(path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x - rx - 8, y - ry * 0.42, x, y - ry * 0.72, x + rx + 10, y - ry * 0.42), 'none', acc, 8))
        fc = S.get('feather', '#f4f0e0')
        out.append(P.shape('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (
            x + rx * 0.4, y - ry * 0.85, x + rx * 0.3, y - ry * 1.9, x - rx * 0.6, y - ry * 2.3, x - rx * 1.1, y - ry * 2.1,
            x - rx * 0.4, y - ry * 1.8, x - rx * 0.1, y - ry * 1.3, x + rx * 0.4, y - ry * 0.85), fc))
        out.append(path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x + rx * 0.35, y - ry * 0.9, x - rx * 0.2, y - ry * 1.8, x - rx * 1.0, y - ry * 2.08), 'none', shade(fc, -0.35), 3))
    elif hg == 'winged_helm':
        for sx in (-1, 1):
            out.append(P.shape('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (
                x + sx * rx * 0.85, y - ry * 0.5, x + sx * rx * 1.3, y - ry * 1.2, x + sx * rx * 1.6, y - ry * 1.5, x + sx * rx * 1.9, y - ry * 1.55,
                x + sx * rx * 1.6, y - ry * 1.2, x + sx * rx * 1.75, y - ry * 1.0, x + sx * rx * 1.45, y - ry * 0.75,
                x + sx * rx * 1.3, y - ry * 0.55, x + sx * rx * 1.1, y - ry * 0.35, x + sx * rx * 0.85, y - ry * 0.5), '#fbf6ea'))
        out.append(P.shape('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (
            x - rx - 6, y - ry * 0.1, x - rx - 8, y - ry * 1.32, x + rx + 8, y - ry * 1.32, x + rx + 6, y - ry * 0.1,
            x, y - ry * 0.6, x - rx - 6, y - ry * 0.1), c, kind='metal'))
        out.append(P.glow(x, y - ry * 0.68, 14, '#ffffff', 0.6))
        out.append(poly(star_points(x, y - ry * 0.68, 10, 4.5, 8), '#fff6c8', ink(c), 2))
    elif hg in ('dragon_helm', 'horned_helm'):
        hc = S.get('horn_color', '#f1e6c8')
        for sx in (-1, 1):
            out.append(P.shape('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (
                x + sx * rx * 0.55, y - ry * 0.75, x + sx * rx * 1.0, y - ry * 1.3, x + sx * rx * 1.3, y - ry * 1.7, x + sx * rx * 1.15, y - ry * 2.1,
                x + sx * rx * 1.6, y - ry * 1.6, x + sx * rx * 1.3, y - ry * 0.9, x + sx * rx * 0.9, y - ry * 0.45), hc))
        out.append(P.shape('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f L%.1f %.1f Q%.1f %.1f %.1f %.1f L%.1f %.1f Z' % (
            x - rx - 8, y + 14, x - rx - 10, y - ry * 1.35, x + rx + 10, y - ry * 1.35, x + rx + 8, y + 14,
            x + rx - 8, y - ry * 0.15, x, y - ry * 0.5, x - rx + 8, y - ry * 0.15, x - rx - 8, y + 14), c, kind='metal'))
        if hg == 'dragon_helm':
            out.append(P.shape('M%.1f %.1f L%.1f %.1f L%.1f %.1f L%.1f %.1f Z' % (x, y - ry * 1.45, x + 12, y - ry * 0.9, x, y - ry * 0.45, x - 12, y - ry * 0.9), acc, 3.5, 'metal'))
            out.append(P.glow(x, y - ry * 0.85, 10, '#ffd36a', 0.8))
        out.append(path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x - rx * 0.6, y - ry * 0.95, x, y - ry * 1.15, x + rx * 0.6, y - ry * 0.95), 'none', '#ffffff', 5, opacity=0.4))
    elif hg == 'crown' or hg == 'skull_crown':
        cc = acc
        pts = [(x - rx * 0.85, y - ry * 0.55), (x - rx * 0.95, y - ry * 1.35), (x - rx * 0.5, y - ry * 0.95), (x - rx * 0.25, y - ry * 1.6),
               (x, y - ry * 1.0), (x + rx * 0.25, y - ry * 1.6), (x + rx * 0.5, y - ry * 0.95), (x + rx * 0.95, y - ry * 1.35), (x + rx * 0.85, y - ry * 0.55)]
        if hg == 'skull_crown':
            pts = [(x - rx * 0.9, y - ry * 0.45), (x - rx * 1.05, y - ry * 1.5), (x - rx * 0.55, y - ry * 0.95), (x - rx * 0.3, y - ry * 1.8),
                   (x, y - ry * 1.05), (x + rx * 0.3, y - ry * 1.8), (x + rx * 0.55, y - ry * 0.95), (x + rx * 1.05, y - ry * 1.5), (x + rx * 0.9, y - ry * 0.45)]
        out.append(P.shape(rounded_poly(pts, 3), cc, kind='metal'))
        gem = S.get('gem', '#e0303c')
        for gx in (-0.5, 0, 0.5):
            out.append(P.glow(x + gx * rx, y - ry * 0.72, 9, gem, 0.6))
            out.append(circle(x + gx * rx, y - ry * 0.72, 6, gem, ink(gem), 2.5))
    elif hg == 'bandana':
        out.append(P.shape('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f L%.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (
            x - rx - 6, y - ry * 0.15, x - rx - 6, y - ry * 1.3, x + rx + 6, y - ry * 1.3, x + rx + 6, y - ry * 0.15,
            x + rx, y - ry * 0.2, x, y - ry * 0.6, x - rx, y - ry * 0.2), c))
        out.append(P.shape('M%.1f %.1f l26 -6 l-8 22 Z' % (x + rx + 2, y - ry * 0.4), c))
    elif hg == 'feathers':
        for i, a in enumerate((-50, -25, 0, 25, 50)):
            fc = (S.get('feather', '#e05a3a'), '#f2c14e', '#3aa0a0')[i % 3]
            rad = math.radians(a - 90)
            px, py = x + math.cos(rad) * 70, y - ry * 0.55 + math.sin(rad) * 70
            out.append(P.shape(capsule(x + math.cos(rad) * 30, y - ry * 0.55 + math.sin(rad) * 30, px, py, 4, 11), fc))
        out.append(path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x - rx * 0.95, y - ry * 0.4, x, y - ry * 0.85, x + rx * 0.95, y - ry * 0.4), 'none', '#7a3a20', 10))
        for i in range(5):
            out.append(circle(x - rx * 0.6 + i * rx * 0.3, y - ry * 0.55 - (4 if i in (1, 3) else 8), 4.5, '#f6e7b8', '#5a3a20', 2))
    elif hg == 'dragon_mask':
        out.append(P.shape('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f L%.1f %.1f L%.1f %.1f L%.1f %.1f Z' % (
            x - rx - 4, y + ry * 0.2, x - rx - 8, y - ry * 1.3, x + rx + 8, y - ry * 1.3, x + rx + 4, y + ry * 0.2,
            x + rx * 0.4, y + ry * 0.95, x, y + ry * 1.15, x - rx * 0.4, y + ry * 0.95), c, kind='metal'))
        for sx in (-1, 1):
            out.append(poly([(x + sx * 8, y - 2), (x + sx * 34, y - 14), (x + sx * 30, y + 6)], '#ffcf4a'))
            out.append(P.glow(x + sx * 24, y - 4, 14, '#ff8a2a', 0.7))
            out.append(P.shape('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f L%.1f %.1f Z' % (
                x + sx * rx * 0.55, y - ry * 0.85, x + sx * rx * 0.9, y - ry * 1.6, x + sx * rx * 1.4, y - ry * 1.8, x + sx * rx * 1.6, y - ry * 1.5,
                x + sx * rx * 0.85, y - ry * 0.5), S.get('horn_color', '#2a1a1a')))
        out.append(path('M%.1f %.1f L%.1f %.1f' % (x - 14, y + ry * 0.75, x + 14, y + ry * 0.75), 'none', ink(c), 4))
    elif hg == 'horns':
        hc = S.get('horn_color', '#3a2a2a')
        for sx in (-1, 1):
            out.append(P.shape('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (
                x + sx * rx * 0.45, y - ry * 0.8, x + sx * rx * 0.8, y - ry * 1.5, x + sx * rx * 1.25, y - ry * 1.55, x + sx * rx * 1.45, y - ry * 1.9,
                x + sx * rx * 1.5, y - ry * 1.3, x + sx * rx * 1.1, y - ry * 0.8, x + sx * rx * 0.8, y - ry * 0.55), hc))
    return ''.join(out)


# =========================================================================== TORSO / LEGS


def torso(P, B, S):
    T = S.get('torso', {})
    kind = T.get('kind', 'tunic')
    c = T.get('color', '#5a7ab0')
    trim = T.get('trim', '#e8c060')
    x, ty, hipy, wt, wb = 200, B['ty'], B['hipy'], B['wt'], B['wb']
    out = []
    # neck
    if kind != 'ribs':
        out.append(P.cap(x, ty - 14, x, ty + 6, 11, 11, S['skin']))
    else:
        out.append(P.cap(x, ty - 18, x, ty + 10, 6, 6, S.get('bone', '#ece4cf')))
    body = rounded_poly([(x - wt, ty + 4), (x + wt, ty + 4), (x + wb + 2, hipy + 8), (x - wb - 2, hipy + 8)], 18)
    if kind == 'robe' or kind == 'gown':
        skirt = T.get('skirt', c)
        flare = T.get('flare', 30)
        out.append(P.shape(rounded_poly([(x - wb - 2, hipy - 14), (x + wb + 2, hipy - 14), (x + wb + flare, B['footy'] - 10), (x - wb - flare, B['footy'] - 10)], 14), skirt))
        out.append(path('M%.1f %.1f L%.1f %.1f M%.1f %.1f L%.1f %.1f' % (x - 8, hipy, x - 18, B['footy'] - 16, x + 10, hipy, x + 20, B['footy'] - 16), 'none', shade(skirt, -0.3), 3.5))
        out.append(path('M%.1f %.1f L%.1f %.1f' % (x - wb - flare + 6, B['footy'] - 16, x + wb + flare - 6, B['footy'] - 16), 'none', trim, 6))
        if T.get('ragged'):
            pts = []
            for i in range(9):
                xx = x - wb - flare + 6 + i * (wb + flare - 6) * 2 / 8
                pts.append((xx, B['footy'] - 6 + (10 if i % 2 else -4)))
            out.append(poly([(x - wb - flare + 6, B['footy'] - 14)] + pts + [(x + wb + flare - 6, B['footy'] - 14)], skirt, ink(skirt), 4))
    if kind == 'ribs':
        bone = S.get('bone', '#ece4cf')
        out.append(P.cap(x, ty + 4, x, hipy, 6, 6, bone))
        for i in range(4):
            yy = ty + 20 + i * 18
            w = wt - 4 - i * 5
            out.append(path('M%.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f' % (x - w, yy + 10, x - w * 0.6, yy - 6, x, yy, x + w * 0.6, yy - 6, x + w, yy + 10),
                            'none', ink(bone), 11))
            out.append(path('M%.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f' % (x - w, yy + 10, x - w * 0.6, yy - 6, x, yy, x + w * 0.6, yy - 6, x + w, yy + 10),
                            'none', bone, 6))
        out.append(P.ell(x, hipy, wb + 4, 14, bone, 4))
        if T.get('cloth'):
            cc = T['cloth']
            out.append(P.shape('M%.1f %.1f L%.1f %.1f L%.1f %.1f L%.1f %.1f L%.1f %.1f L%.1f %.1f Z' % (
                x - wb - 6, hipy - 8, x + wb + 6, hipy - 8, x + wb + 2, hipy + 36, x + 6, hipy + 22, x - 10, hipy + 40, x - wb - 4, hipy + 24), cc))
        return ''.join(out)
    out.append(P.shape(body, c, kind='metal' if kind == 'plate' else 'fill'))
    if kind == 'plate':
        out.append(path('M%.1f %.1f L%.1f %.1f' % (x, ty + 14, x, hipy - 10), 'none', shade(c, -0.35), 4))
        out.append(path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x - wt + 8, ty + 50, x, ty + 64, x + wt - 8, ty + 50), 'none', shade(c, -0.35), 4))
        out.append(P.shine('M%.1f %.1f q10 -6 22 -4' % (x - wt + 10, ty + 22), 0.55, 5))
    elif kind == 'leather':
        sh = T.get('shirt', '#e8dcc0')
        out.append(P.shape(rounded_poly([(x - 14, ty + 4), (x + 14, ty + 4), (x + 10, ty + 46), (x - 10, ty + 46)], 6), sh))
        out.append(path('M%.1f %.1f L%.1f %.1f' % (x - wt + 6, ty + 14, x + wb - 4, hipy - 6), 'none', shade(c, -0.45), 7))
        out.append(path('M%.1f %.1f L%.1f %.1f' % (x - wt + 6, ty + 14, x + wb - 4, hipy - 6), 'none', trim, 3))
    elif kind == 'tunic':
        out.append(path('M%.1f %.1f L%.1f %.1f L%.1f %.1f' % (x - 16, ty + 4, x, ty + 24, x + 16, ty + 4), 'none', shade(c, -0.4), 4))
    elif kind in ('robe', 'gown'):
        out.append(path('M%.1f %.1f L%.1f %.1f L%.1f %.1f' % (x - 18, ty + 4, x, ty + 30, x + 18, ty + 4), 'none', trim, 5))
        out.append(path('M%.1f %.1f L%.1f %.1f' % (x, ty + 30, x, hipy - 10), 'none', trim, 4))
    elif kind == 'rags':
        out.append(path('M%.1f %.1f l12 18 l-6 12 M%.1f %.1f l-10 14' % (x - wt + 10, ty + 20, x + wt - 12, ty + 30), 'none', shade(c, -0.35), 3.5))
    elif kind == 'belly':
        out.append(P.ell(x, hipy - 26, wb * 0.75, (hipy - ty) * 0.42, mix(c, '#f0d8a8', 0.6), 4))
    # tabard / emblem
    tab = T.get('tabard')
    if tab:
        out.append(P.shape(rounded_poly([(x - 20, ty + 24), (x + 20, ty + 24), (x + 18, hipy + 34), (x, hipy + 44), (x - 18, hipy + 34)], 6), tab))
    emb = T.get('emblem')
    if emb:
        ey = ty + 46
        ec = T.get('emblem_color', trim)
        if emb == 'shield':
            out.append(P.shape('M%.1f %.1f L%.1f %.1f L%.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (x - 12, ey - 12, x + 12, ey - 12, x + 12, ey, x + 10, ey + 12, x, ey + 18, x - 10, ey + 12, x - 12, ey), ec, 3))
        elif emb == 'sun':
            out.append(P.glow(x, ey, 22, ec, 0.6))
            out.append(poly(star_points(x, ey, 15, 7, 8), ec, ink(ec), 2.5))
        elif emb == 'star':
            out.append(poly(star_points(x, ey, 13, 5.5, 4, -90), ec, ink(ec), 2.5))
        elif emb == 'dragon':
            out.append(P.shape('M%.1f %.1f q10 -14 22 -6 q-6 2 -6 8 q10 4 6 16 q-6 -8 -14 -4 q-8 -6 -8 -14 Z' % (x - 12, ey + 2), ec, 3))
        elif emb == 'eye':
            out.append(ellipse(x, ey, 14, 8, ec, ink(ec), 2.5))
            out.append(circle(x, ey, 4, '#1a0a1a'))
        elif emb == 'note':
            out.append(path('M%.1f %.1f l0 -20 l14 -4 l0 20' % (x - 6, ey + 8), 'none', ec, 4))
            out.append(circle(x - 10, ey + 8, 5, ec))
            out.append(circle(x + 4, ey + 4, 5, ec))
    belt = T.get('belt', '#5a3a24')
    if belt and kind not in ('ribs',):
        out.append(rect(x - wb - 2, hipy - 10, (wb + 2) * 2, 14, 5, belt, ink(belt), 3.5))
        out.append(rect(x - 8, hipy - 12, 16, 18, 3, T.get('buckle', '#e8c060'), ink('#e8c060'), 3))
    return ''.join(out)


def leg(P, B, S, side):
    """One leg in hip-local coordinates (hip joint at (hx, hipy))."""
    L = S.get('legs', {})
    kind = L.get('kind', 'pants')
    c = L.get('color', '#4a4a62')
    boot = L.get('boot', '#5a3a28')
    x = 200 + side * B['hipx']
    top, foot = B['hipy'], B['footy']
    out = []
    if kind == 'bones':
        bone = S.get('bone', '#ece4cf')
        out.append(P.cap(x, top, x + side * 2, foot - 14, 6, 5, bone, 4))
        out.append(P.ell(x + side * 2, (top + foot) / 2, 8, 7, bone, 3.5))
        out.append(P.shape('M%.1f %.1f q%.1f -4 %.1f 4 q2 10 -%.1f 10 Z' % (x - 10 + side * 2, foot - 12, side * 4 + 14, side * 8 + 12, 0), bone, 4))
        out.append(P.ell(x + side * 6, foot - 6, 14, 7, bone, 4))
        return ''.join(out)
    if kind == 'paws':
        out.append(P.cap(x, top - 10, x + side * 4, foot - 10, B['limb'] * 1.2, B['limb'], c))
        out.append(P.ell(x + side * 5, foot - 8, B['limb'] * 1.25, 11, c))
        for k in (-1, 0, 1):
            out.append(path('M%.1f %.1f l0 7' % (x + side * 5 + k * 7, foot - 12), 'none', ink(c), 3))
        return ''.join(out)
    w = B['limb'] * (1.08 if kind != 'armor' else 1.15)
    out.append(P.cap(x, top - 6, x + side * 3, foot - 20, w, w * 0.88, c, kind='metal' if kind == 'armor' else 'fill'))
    if kind == 'armor':
        out.append(P.ell(x + side * 2, (top + foot) / 2 - 2, w * 0.95, 9, shade(c, 0.1), 4, 'metal'))
    # boot
    bx = x + side * 4
    out.append(P.shape('M%.1f %.1f L%.1f %.1f Q%.1f %.1f %.1f %.1f L%.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (
        bx - w, foot - 34, bx + w, foot - 34, bx + w + 2, foot - 16, bx + w + side * 10 + (4 if side > 0 else 0), foot - 6,
        bx + side * 18 - (side < 0) * 0, foot + 2, bx - side * 14, foot + 4, bx - w - 2, foot - 12), boot,
        kind='metal' if L.get('boot_metal') else 'fill'))
    out.append(path('M%.1f %.1f L%.1f %.1f' % (bx - w + 1, foot - 30, bx + w - 1, foot - 30), 'none', shade(boot, 0.35), 4))
    return ''.join(out)


# =========================================================================== ARMS


def arm(P, B, S, side, holding=None):
    """Arm in shoulder-local space (shoulder at 0,0, hanging down +y). side: -1 viewer-left, +1 viewer-right."""
    A = S.get('arms', {})
    kind = A.get('kind', 'sleeve')
    c = A.get('color', S.get('torso', {}).get('color', '#5a7ab0'))
    glove = A.get('glove')
    L = B['arm']
    r = B['limb']
    out = []
    if kind == 'bones':
        bone = S.get('bone', '#ece4cf')
        out.append(P.cap(0, 0, 0, L * 0.5, 5.5, 5, bone, 4))
        out.append(P.cap(0, L * 0.5, 0, L * 0.92, 5, 4.5, bone, 4))
        out.append(P.ell(0, L * 0.5, 7, 7, bone, 3.5))
        out.append(P.ell(0, L, 9, 10, bone, 4))
        return ''.join(out)
    if kind == 'wing':
        return ''
    upper_c = c
    lower_c = c if kind in ('sleeve', 'armor', 'robe') else S['skin']
    if kind == 'bare':
        upper_c = lower_c = S['skin']
    if kind == 'robe':
        out.append(P.shape('M%.1f %.1f L%.1f %.1f L%.1f %.1f Q%.1f %.1f %.1f %.1f L%.1f %.1f Z' % (
            -r, -4, r, -4, r + 10, L * 0.78, 0, L * 0.92, -r - 10, L * 0.78, -r, -4), c))
        out.append(path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (-r - 9, L * 0.78, 0, L * 0.92, r + 9, L * 0.78), 'none', S.get('torso', {}).get('trim', '#e8c060'), 4))
    else:
        out.append(P.cap(0, 0, 0, L * 0.55, r * 1.02, r * 0.9, upper_c, kind='metal' if kind == 'armor' else 'fill'))
        out.append(P.cap(0, L * 0.45, 0, L * 0.9, r * 0.9, r * 0.82, lower_c, kind='metal' if kind == 'armor' else 'fill'))
    if A.get('bracer'):
        out.append(rect(-r * 0.95, L * 0.62, r * 1.9, L * 0.2, 4, A['bracer'], ink(A['bracer']), 3.5))
    hand = glove or S['skin']
    if A.get('claws'):
        for k in (-1, 0, 1):
            out.append(poly([(k * 6 - 3, L + 6), (k * 8, L + 24), (k * 6 + 3, L + 6)], '#f2ead6', ink('#f2ead6'), 2.5))
    out.append(P.ell(0, L, r * 0.92, r * 0.95, hand))
    if A.get('pauldron'):
        pc = A['pauldron']
        out.append(P.shape('M%.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (-r * 1.7, 8, 0, -r * 1.8, r * 1.7, 8, 0, r * 0.9, -r * 1.7, 8), pc, kind='metal'))
        out.append(P.shine('M%.1f %.1f q6 -6 14 -6' % (-r * 0.9, -2), 0.5, 4))
    return ''.join(out)


# =========================================================================== WEAPONS (hand-local: grip at 0,0, pointing -y)


def weapon(P, kind, S):
    W = S.get('weapon_colors', {})
    metal = W.get('metal', '#c8d0dc')
    wood = W.get('wood', '#8a5a34')
    acc = W.get('accent', '#e8b84a')
    gem = W.get('gem', S.get('aura', '#7ad8ff'))
    out = []
    if kind in ('sword', 'blade'):
        out.append(P.cap(0, 12, 0, -10, 5, 5, W.get('grip', '#5a3424'), 3.5))
        out.append(P.ell(0, 16, 7, 7, acc, 3.5, 'metal'))
        out.append(P.shape('M-5 -14 L5 -14 L7 -84 L0 -100 L-7 -84 Z', metal, 4, 'metal'))
        out.append(path('M0 -18 L0 -90', 'none', '#ffffff', 2.5, opacity=0.7))
        out.append(P.shape(rounded_poly([(-22, -16), (22, -16), (20, -8), (-20, -8)], 3), acc, 4, 'metal'))
    elif kind == 'greatsword':
        out.append(P.cap(0, 20, 0, -12, 6, 6, W.get('grip', '#3a2020'), 3.5))
        out.append(P.ell(0, 24, 9, 9, acc, 3.5, 'metal'))
        out.append(P.shape('M-11 -18 L11 -18 L13 -128 L0 -152 L-13 -128 Z', metal, 4.5, 'metal'))
        out.append(path('M0 -24 L0 -140', 'none', shade(metal, -0.35), 3))
        rune = W.get('rune', '#ff6a3a')
        for i in range(3):
            out.append(P.glow(0, -50 - i * 28, 9, rune, 0.9))
            out.append(path('M-4 %d l4 -6 l4 6 l-4 6 Z' % (-50 - i * 28), rune, None, None))
        out.append(P.shape('M-32 -22 Q0 -8 32 -22 L28 -10 Q0 4 -28 -10 Z', acc, 4, 'metal'))
    elif kind == 'dagger':
        out.append(P.cap(0, 8, 0, -6, 4, 4, W.get('grip', '#3a2a3a'), 3))
        out.append(P.shape('M-5 -8 L5 -8 L3 -52 L0 -60 L-3 -52 Z', metal, 3.5, 'metal'))
        out.append(P.shape(rounded_poly([(-13, -10), (13, -10), (11, -5), (-11, -5)], 2), acc, 3, 'metal'))
    elif kind == 'bow':
        out.append(P.shape('M0 -78 Q-46 -40 -8 0 Q-46 40 0 78 L4 72 Q-34 40 0 0 Q-34 -40 4 -72 Z', wood, 4))
        out.append(path('M2 -76 L2 76', 'none', '#f2ecd8', 2.5))
        out.append(rect(-9, -8, 12, 16, 3, W.get('grip', '#6a3a24'), ink('#6a3a24'), 3))
        if W.get('leaf'):
            out.append(P.shape('M-20 -48 q-14 -4 -16 -18 q14 2 16 18 Z', W['leaf'], 2.5))
            out.append(P.shape('M-20 48 q-14 4 -16 18 q14 -2 16 -18 Z', W['leaf'], 2.5))
    elif kind.startswith('staff'):
        out.append(P.cap(0, 70, 0, -110, 5.5, 5, wood, 4))
        top = kind.split('_')[1] if '_' in kind else 'orb'
        if top == 'orb':
            out.append(P.shape('M-14 -104 Q0 -124 14 -104 L8 -96 Q0 -106 -8 -96 Z', acc, 3.5, 'metal'))
            out.append(P.glow(0, -124, 32, gem, 0.75))
            out.append(circle(0, -124, 15, P.d.rad([(0, '#ffffff'), (0.35, shade(gem, 0.4)), (1, gem)], 0.38, 0.35, 0.65), ink(gem), 3.5))
        elif top == 'storm':
            out.append(P.glow(0, -128, 40, gem, 0.8))
            out.append(poly([(0, -160), (12, -128), (4, -128), (10, -98), (-10, -134), (-2, -134), (-8, -160)], '#f6fbff', ink(gem), 3.5))
            out.append(P.shape('M-18 -106 Q0 -96 18 -106 L14 -98 Q0 -88 -14 -98 Z', acc, 3, 'metal'))
        elif top == 'skull':
            out.append(P.glow(0, -124, 30, gem, 0.75))
            out.append(P.ell(0, -126, 17, 16, '#ece4cf', 3.5))
            out.append(ellipse(-6, -126, 4.5, 5, '#1a0a14'))
            out.append(ellipse(6, -126, 4.5, 5, '#1a0a14'))
            out.append(circle(-6, -126, 2, gem))
            out.append(circle(6, -126, 2, gem))
            for k in (-1, 1):
                out.append(P.shape('M%d -112 q%d 10 %d 26 q%d -10 %d -26 Z' % (k * 8, k * 8, k * 4, k * 6, -k * 12), W.get('feather', '#c84a3a'), 2.5))
        elif top == 'sun':
            out.append(P.glow(0, -126, 36, '#ffe9a0', 0.85))
            out.append(poly(star_points(0, -126, 26, 12, 8), acc, ink(acc), 3))
            out.append(circle(0, -126, 10, '#fffbe8', ink(acc), 2.5))
        elif top == 'dragon':
            out.append(P.glow(0, -128, 30, '#ff5a2a', 0.8))
            out.append(P.shape('M-16 -100 Q-24 -140 0 -150 Q24 -140 16 -100 Q8 -118 0 -112 Q-8 -118 -16 -100 Z', '#2a1a1a', 3.5))
            out.append(circle(0, -130, 10, '#ff8a3a', ink('#ff8a3a'), 3))
        elif top == 'lantern':
            out.append(P.glow(0, -116, 30, '#9a5aff', 0.85))
            out.append(rect(-11, -130, 22, 26, 5, '#3a2a40', ink('#3a2a40'), 3.5))
            out.append(rect(-6, -126, 12, 18, 3, '#d6b8ff'))
    elif kind == 'lute':
        out.append(P.cap(0, -10, 0, -84, 5, 4, wood, 3.5))
        out.append(P.shape('M-8 -86 L8 -86 L6 -102 L-6 -102 Z', shade(wood, -0.2), 3.5))
        out.append(P.ell(0, 14, 30, 36, W.get('body', '#c8783a'), 4.5))
        out.append(circle(0, 8, 9, '#3a1e10'))
        out.append(path('M-3 -80 L-3 40 M3 -80 L3 40', 'none', '#f8f0d8', 1.5))
        out.append(rect(-14, 30, 28, 6, 2, '#3a1e10'))
    elif kind == 'club':
        out.append(P.shape('M-6 14 L6 14 L18 -70 Q0 -96 -18 -70 Z', wood, 4.5))
        for (sx, sy) in ((-14, -60), (12, -50), (-8, -78), (14, -78)):
            out.append(poly([(sx - 4, sy), (sx + (8 if sx > 0 else -8), sy - 5), (sx + 2, sy + 6)], metal, ink(metal), 2.5))
    elif kind == 'axe':
        out.append(P.cap(0, 20, 0, -96, 6, 5, wood, 4))
        out.append(P.shape('M0 -92 Q40 -110 46 -72 Q42 -48 0 -56 Z', metal, 4.5, 'metal'))
        out.append(P.shape('M0 -92 Q-28 -102 -32 -78 Q-28 -60 0 -62 Z', metal, 4, 'metal'))
    elif kind == 'mace':
        out.append(P.cap(0, 14, 0, -60, 5, 5, wood, 4))
        out.append(P.ell(0, -72, 18, 18, metal, 4, 'metal'))
        for a in range(0, 360, 60):
            rad = math.radians(a)
            out.append(poly([(math.cos(rad) * 16, -72 + math.sin(rad) * 16), (math.cos(rad) * 28, -72 + math.sin(rad) * 28), (math.cos(rad + 0.4) * 16, -72 + math.sin(rad + 0.4) * 16)], metal, ink(metal), 2.5))
    elif kind == 'scythe':
        out.append(P.cap(0, 70, 0, -120, 5, 5, wood, 4))
        out.append(P.shape('M0 -118 Q70 -130 96 -70 Q60 -104 2 -100 Z', metal, 4, 'metal'))
    elif kind == 'claw':
        pass
    elif kind == 'tome':
        out.append(P.shape(rounded_poly([(-22, -28), (22, -28), (22, 22), (-22, 22)], 4), W.get('cover', '#6a2a3a'), 4))
        out.append(rect(-17, -24, 34, 4, 1, '#f4ecd8'))
        out.append(P.glow(0, -2, 14, gem, 0.8))
        out.append(poly(star_points(0, -2, 9, 4, 4, -45), acc, ink(acc), 2))
    return ''.join(out)


def offhand(P, kind, S):
    W = S.get('weapon_colors', {})
    acc = W.get('accent', '#e8b84a')
    out = []
    if kind == 'shield_round':
        c = W.get('shield', '#8a5a34')
        out.append(P.ell(0, 0, 40, 40, c, 5))
        out.append(circle(0, 0, 33, 'none', shade(c, -0.3), 3))
        out.append(circle(0, 0, 40, 'none', W.get('rim', '#a8b0bc'), 6))
        out.append(P.ell(0, 0, 11, 11, W.get('rim', '#a8b0bc'), 4, 'metal'))
        out.append(P.shine('M-26 -18 q8 -12 22 -16', 0.4, 4))
    elif kind == 'shield_kite':
        c = W.get('shield', '#f2f0ea')
        out.append(P.shape('M-36 -44 L36 -44 Q38 0 0 56 Q-38 0 -36 -44 Z', c, 5, 'metal'))
        out.append(path('M-28 -36 L28 -36 Q30 0 0 44 Q-30 0 -28 -36 Z', 'none', acc, 5))
        out.append(P.glow(0, -4, 20, '#fff2b0', 0.6))
        out.append(poly(star_points(0, -4, 16, 7, 8), acc, ink(acc), 2.5))
    elif kind == 'tome':
        out.append(weapon(P, 'tome', S))
    elif kind == 'orb':
        gem = W.get('gem', S.get('aura', '#7ad8ff'))
        out.append(P.glow(0, -8, 30, gem, 0.8))
        out.append(circle(0, -8, 14, P.d.rad([(0, '#ffffff'), (0.4, shade(gem, 0.3)), (1, gem)], 0.38, 0.35, 0.65), ink(gem), 3))
    elif kind == 'dagger':
        out.append(weapon(P, 'dagger', S))
    elif kind == 'candle':
        out.append(rect(-7, -30, 14, 34, 3, '#2a1a1a', ink('#2a1a1a'), 3))
        out.append(P.glow(0, -40, 18, '#ffb84a', 0.9))
        out.append(path('M0 -48 q6 8 0 14 q-6 -6 0 -14 Z', '#ffe08a', '#ff9a3a', 2))
    return ''.join(out)


# =========================================================================== BACK ITEMS


def back_items(P, B, S):
    out = []
    x, ty, hipy = 200, B['ty'], B['hipy']
    cape = S.get('cape')
    if cape:
        cc = cape if isinstance(cape, str) else cape['color']
        lining = shade(cc, -0.35)
        out.append(P.shape('M%.1f %.1f L%.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (
            x - B['wt'] - 2, ty + 6, x + B['wt'] + 2, ty + 6,
            x + B['wt'] + 30, B['footy'] - 60, x + B['wt'] + 26, B['footy'] - 6,
            x + 20, B['footy'] - 18, x, B['footy'] - 2,
            x - 20, B['footy'] - 18, x - B['wt'] - 26, B['footy'] - 6,
            x - B['wt'] - 30, B['footy'] - 60, x - B['wt'] - 2, ty + 6), cc))
        out.append(path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x - B['wt'] - 18, B['footy'] - 40, x, B['footy'] - 10, x + B['wt'] + 18, B['footy'] - 40), 'none', lining, 3, opacity=0.6))
    if S.get('quiver'):
        qc = S['quiver']
        out.append(g([P.shape(rounded_poly([(-12, -50), (12, -50), (10, 40), (-10, 40)], 6), qc),
                      path('M-6 -50 L-10 -74 M0 -50 L0 -78 M6 -50 L10 -72', 'none', '#7a5a3a', 3.5),
                      poly([(-14, -74), (-10, -86), (-6, -74)], '#e8e0d0'), poly([(-4, -78), (0, -90), (4, -78)], '#e8e0d0'),
                      poly([(6, -72), (10, -84), (14, -72)], '#e8e0d0')], 'translate(%.1f %.1f) rotate(24)' % (x + 34, ty + 50)))
    if S.get('back_weapon') == 'greatsword':
        out.append(g(weapon(P, 'greatsword', S), 'translate(%.1f %.1f) rotate(-30)' % (x + 40, ty + 90)))
    return ''.join(out)


def wings(P, B, S, flap=0.0):
    """Dragon/bat wings attached at the upper back. flap in [-1, 1]."""
    W = S.get('wings')
    if not W:
        return ''
    c = W.get('color', '#8a2a2a')
    mem = W.get('membrane', shade(c, 0.25))
    span = W.get('span', 1.0)
    x, y = 200, B['ty'] + 20
    out = []
    for sx in (-1, 1):
        a = -18 + flap * 24
        tf = 'rotate(%.1f %.1f %.1f)' % (sx * a, x + sx * 16, y)
        X = lambda v: x + sx * v * span
        Y = lambda v: y + v * span
        d = 'M%.1f %.1f L%.1f %.1f L%.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (
            X(16), Y(0), X(90), Y(-90), X(170), Y(-70),
            X(150), Y(-20), X(160), Y(30),
            X(126), Y(10), X(118), Y(56),
            X(86), Y(30), X(70), Y(74),
            X(50), Y(30), X(16), Y(40))
        bones = 'M%.1f %.1f L%.1f %.1f L%.1f %.1f M%.1f %.1f L%.1f %.1f M%.1f %.1f L%.1f %.1f M%.1f %.1f L%.1f %.1f' % (
            X(16), Y(0), X(90), Y(-90), X(170), Y(-70), X(90), Y(-90), X(160), Y(30), X(90), Y(-90), X(118), Y(56), X(90), Y(-90), X(70), Y(74))
        out.append(g([path(d, P.fill(mem), ink(c), OUT), path(bones, 'none', c, 7), path(bones, 'none', ink(c), 2, opacity=0.6)], tf))
    return ''.join(out)


def tail(P, B, S, swing=0.0):
    T = S.get('tail')
    if not T:
        return ''
    c = T.get('color', '#8a2a2a')
    x, y = 200, B['hipy']
    sx = 1
    end = (x + sx * (110 + swing * 14), y - 30 + swing * 10)
    d = 'M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x + 10, y + 10, x + 90, y + 70, end[0], end[1])
    out = [path(d, 'none', ink(c), T.get('w', 26) + OUT * 2), path(d, 'none', c, T.get('w', 26))]
    if T.get('spade', True):
        out.append(P.shape('M%.1f %.1f l18 -26 l12 30 Z' % (end[0] - 10, end[1] + 4), T.get('tip', c)))
    else:
        out.append(P.ell(end[0], end[1], 22, 16, T.get('tip', c)))
    return ''.join(out)
