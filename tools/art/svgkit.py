"""Small SVG authoring + rasterising toolkit shared by every art generator.

All art is authored as SVG strings (so it stays resolution independent and easy to tweak) and rasterised with resvg.
"""
import colorsys
import io
import math
import os

import resvg_py
from PIL import Image

# --------------------------------------------------------------------------- colors


def rgb(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def hexc(c):
    return '#%02x%02x%02x' % tuple(max(0, min(255, int(round(v)))) for v in c[:3])


def mix(a, b, t):
    a, b = rgb(a), rgb(b)
    return hexc([a[i] + (b[i] - a[i]) * t for i in range(3)])


def shade(c, k):
    """k < 0 darkens (towards a hue-preserving deep tone), k > 0 lightens towards white."""
    r, g, b = [v / 255 for v in rgb(c)]
    h, l, s = colorsys.rgb_to_hls(r, g, b)
    if k < 0:
        l = l * (1 + k)
        s = min(1, s * (1 - k * 0.35))
    else:
        l = l + (1 - l) * k
        s = s * (1 - k * 0.3)
    r, g, b = colorsys.hls_to_rgb(h, max(0, min(1, l)), max(0, min(1, s)))
    return hexc((r * 255, g * 255, b * 255))


def hue_shift(c, deg):
    r, g, b = [v / 255 for v in rgb(c)]
    h, l, s = colorsys.rgb_to_hls(r, g, b)
    r, g, b = colorsys.hls_to_rgb((h + deg / 360) % 1, l, s)
    return hexc((r * 255, g * 255, b * 255))


def ink(c):
    """Outline color for a fill: a dark, slightly saturated version of it."""
    return shade(mix(c, '#1a1020', 0.35), -0.62)


# --------------------------------------------------------------------------- defs


class Defs:
    """Collects <defs> entries with unique ids (gradients, filters, clip paths)."""

    def __init__(self, prefix='d'):
        self.items = []
        self.prefix = prefix
        self.n = 0
        self.cache = {}

    def _id(self):
        self.n += 1
        return '%s%d' % (self.prefix, self.n)

    def add(self, key, make):
        if key in self.cache:
            return self.cache[key]
        i = self._id()
        self.items.append(make(i))
        self.cache[key] = i
        return i

    def lin(self, stops, x1=0, y1=0, x2=0, y2=1, units='objectBoundingBox'):
        key = ('lin', tuple(stops), x1, y1, x2, y2, units)
        return 'url(#%s)' % self.add(key, lambda i: '<linearGradient id="%s" x1="%s" y1="%s" x2="%s" y2="%s" gradientUnits="%s">%s</linearGradient>' % (
            i, x1, y1, x2, y2, units, _stops(stops)))

    def rad(self, stops, cx=0.5, cy=0.5, r=0.5, fx=None, fy=None, units='objectBoundingBox'):
        key = ('rad', tuple(stops), cx, cy, r, fx, fy, units)
        f = '' if fx is None else ' fx="%s" fy="%s"' % (fx, fy)
        return 'url(#%s)' % self.add(key, lambda i: '<radialGradient id="%s" cx="%s" cy="%s" r="%s"%s gradientUnits="%s">%s</radialGradient>' % (
            i, cx, cy, r, f, units, _stops(stops)))

    def blur(self, sd):
        key = ('blur', sd)
        return 'url(#%s)' % self.add(key, lambda i: '<filter id="%s" x="-50%%" y="-50%%" width="200%%" height="200%%"><feGaussianBlur stdDeviation="%s"/></filter>' % (i, sd))

    def glow(self, sd, color, opacity=1.0):
        key = ('glow', sd, color, opacity)
        return 'url(#%s)' % self.add(key, lambda i: (
            '<filter id="%s" x="-60%%" y="-60%%" width="220%%" height="220%%">'
            '<feGaussianBlur in="SourceAlpha" stdDeviation="%s" result="b"/>'
            '<feFlood flood-color="%s" flood-opacity="%s"/><feComposite in2="b" operator="in" result="g"/>'
            '<feMerge><feMergeNode in="g"/><feMergeNode in="g"/><feMergeNode in="SourceGraphic"/></feMerge></filter>') % (i, sd, color, opacity))

    def shadow(self, dx, dy, sd, color='#000', opacity=0.5):
        key = ('shadow', dx, dy, sd, color, opacity)
        return 'url(#%s)' % self.add(key, lambda i: (
            '<filter id="%s" x="-30%%" y="-30%%" width="160%%" height="160%%">'
            '<feGaussianBlur in="SourceAlpha" stdDeviation="%s"/><feOffset dx="%s" dy="%s" result="o"/>'
            '<feFlood flood-color="%s" flood-opacity="%s"/><feComposite in2="o" operator="in" result="s"/>'
            '<feMerge><feMergeNode in="s"/><feMergeNode in="SourceGraphic"/></feMerge></filter>') % (i, sd, dx, dy, color, opacity))

    def clip(self, inner):
        key = ('clip', inner)
        return 'url(#%s)' % self.add(key, lambda i: '<clipPath id="%s">%s</clipPath>' % (i, inner))

    def mask(self, inner):
        key = ('mask', inner)
        return 'url(#%s)' % self.add(key, lambda i: '<mask id="%s" maskUnits="userSpaceOnUse" x="-5000" y="-5000" width="10000" height="10000">%s</mask>' % (i, inner))

    def svg(self):
        return '<defs>%s</defs>' % ''.join(self.items)


def _stops(stops):
    out = []
    for s in stops:
        if len(s) == 2:
            out.append('<stop offset="%s" stop-color="%s"/>' % s)
        else:
            out.append('<stop offset="%s" stop-color="%s" stop-opacity="%s"/>' % s)
    return ''.join(out)


# --------------------------------------------------------------------------- shapes


def attrs(**kw):
    out = []
    for k, v in kw.items():
        if v is None:
            continue
        k = k.rstrip('_').replace('_', '-')
        out.append('%s="%s"' % (k, v))
    return ' '.join(out)


def path(d, fill='none', stroke=None, sw=None, **kw):
    return '<path d="%s" %s/>' % (d, attrs(fill=fill, stroke=stroke, stroke_width=sw, stroke_linejoin='round' if stroke else None,
                                          stroke_linecap='round' if stroke else None, **kw))


def circle(cx, cy, r, fill='none', stroke=None, sw=None, **kw):
    return '<circle cx="%.2f" cy="%.2f" r="%.2f" %s/>' % (cx, cy, r, attrs(fill=fill, stroke=stroke, stroke_width=sw, **kw))


def ellipse(cx, cy, rx, ry, fill='none', stroke=None, sw=None, **kw):
    return '<ellipse cx="%.2f" cy="%.2f" rx="%.2f" ry="%.2f" %s/>' % (cx, cy, rx, ry, attrs(fill=fill, stroke=stroke, stroke_width=sw, **kw))


def rect(x, y, w, h, rx=0, fill='none', stroke=None, sw=None, **kw):
    return '<rect x="%.2f" y="%.2f" width="%.2f" height="%.2f" rx="%.2f" %s/>' % (x, y, w, h, rx, attrs(fill=fill, stroke=stroke, stroke_width=sw, stroke_linejoin='round' if stroke else None, **kw))


def g(content, transform=None, **kw):
    if isinstance(content, (list, tuple)):
        content = ''.join(content)
    return '<g %s>%s</g>' % (attrs(transform=transform, **kw), content)


def poly(points, fill='none', stroke=None, sw=None, close=True, **kw):
    d = 'M' + ' L'.join('%.2f %.2f' % p for p in points) + (' Z' if close else '')
    return path(d, fill, stroke, sw, **kw)


def star_points(cx, cy, r_out, r_in, n=5, rot=-90):
    pts = []
    for i in range(n * 2):
        r = r_out if i % 2 == 0 else r_in
        a = math.radians(rot + i * 180 / n)
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def capsule(x1, y1, x2, y2, r1, r2=None):
    """Path for a tapered capsule from (x1,y1) radius r1 to (x2,y2) radius r2."""
    r2 = r1 if r2 is None else r2
    dx, dy = x2 - x1, y2 - y1
    L = math.hypot(dx, dy) or 1
    nx, ny = -dy / L, dx / L
    a1 = (x1 + nx * r1, y1 + ny * r1)
    b1 = (x1 - nx * r1, y1 - ny * r1)
    a2 = (x2 + nx * r2, y2 + ny * r2)
    b2 = (x2 - nx * r2, y2 - ny * r2)
    return ('M%.2f %.2f L%.2f %.2f A%.2f %.2f 0 0 1 %.2f %.2f L%.2f %.2f A%.2f %.2f 0 0 1 %.2f %.2f Z' %
            (a1[0], a1[1], a2[0], a2[1], r2, r2, b2[0], b2[1], b1[0], b1[1], r1, r1, a1[0], a1[1]))


def rounded_poly(pts, r):
    """Closed polygon with rounded corners (quadratic corners)."""
    n = len(pts)
    d = []
    for i in range(n):
        p0, p1, p2 = pts[i - 1], pts[i], pts[(i + 1) % n]
        v1 = (p0[0] - p1[0], p0[1] - p1[1])
        v2 = (p2[0] - p1[0], p2[1] - p1[1])
        l1, l2 = math.hypot(*v1) or 1, math.hypot(*v2) or 1
        rr = min(r, l1 / 2, l2 / 2)
        a = (p1[0] + v1[0] / l1 * rr, p1[1] + v1[1] / l1 * rr)
        b = (p1[0] + v2[0] / l2 * rr, p1[1] + v2[1] / l2 * rr)
        d.append(('M' if i == 0 else 'L') + '%.2f %.2f Q%.2f %.2f %.2f %.2f' % (a[0], a[1], p1[0], p1[1], b[0], b[1]))
    return ' '.join(d) + ' Z'


# --------------------------------------------------------------------------- render


def document(body, w, h, vb=None, defs=None):
    vb = vb or (0, 0, w, h)
    return ('<svg xmlns="http://www.w3.org/2000/svg" width="%d" height="%d" viewBox="%s">%s%s</svg>' %
            (w, h, ' '.join('%.2f' % v for v in vb), defs.svg() if defs else '', body))


def rasterize(svg):
    data = resvg_py.svg_to_bytes(svg_string=svg)
    return Image.open(io.BytesIO(bytes(data))).convert('RGBA')


def render(body, out_path, w, h, vb=None, defs=None):
    img = rasterize(document(body, w, h, vb, defs))
    save(img, out_path)
    return img


def save(img, out_path):
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    img.save(out_path, optimize=True)
