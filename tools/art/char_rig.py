"""Cut-out rig: composes character parts per pose and renders animation frames, portraits and round icons.

Bones (all rotations in degrees, SVG convention: positive = clockwise on screen):
  root  (rx, ry offset; rr rotation and sx/sy scale around the feet)
  torso (tr) around the hips -> carries head (hr, hy, hs) and both arms
  armF  (aF) weapon arm on the viewer's left, wF = wrist rotation of the weapon
  armB  (aB) off arm on the viewer's right, wB = wrist rotation of the off-hand item
  legs  (lF, lB) around the hips
"""
import json
import math
import os

import char_parts as cp
from svgkit import Defs, circle, document, ellipse, g, ink, path, poly, rasterize, save, shade, star_points

VIEW = (-40, -70, 480, 480)  # frame viewBox (units)

WEAPON_TIP = {'sword': (0, -100), 'blade': (0, -100), 'greatsword': (0, -150), 'dagger': (0, -58), 'bow': (-30, 0),
              'staff': (0, -124), 'staff_orb': (0, -124), 'staff_storm': (0, -130), 'staff_skull': (0, -126), 'staff_sun': (0, -126),
              'staff_dragon': (0, -130), 'staff_lantern': (0, -116), 'lute': (0, 10), 'club': (0, -80), 'axe': (20, -80),
              'mace': (0, -72), 'scythe': (60, -100), 'tome': (0, -2), 'claw': (0, 20), None: (0, 0)}

ARCHETYPE = {'sword': 'melee', 'blade': 'melee', 'greatsword': 'heavy', 'dagger': 'melee', 'club': 'heavy', 'axe': 'heavy', 'mace': 'melee',
             'scythe': 'heavy', 'bow': 'ranged', 'lute': 'bard', 'tome': 'caster', 'claw': 'beast'}


def archetype(S):
    if S.get('archetype'):
        return S['archetype']
    w = S.get('weapon')
    if w and w.startswith('staff'):
        return 'caster'
    return ARCHETYPE.get(w, 'melee')


# =========================================================================== beast heads


def beast_head(P, B, S, expr, jaw=0.0):
    kind = S['head']
    x, y, rx, ry = B['hx'], B['hy'], B['hrx'], B['hry']
    c = S['skin']
    out = []
    if kind == 'wolf':
        fur2 = S.get('fur2', '#e8e4dc')
        for sx in (-1, 1):  # ears
            out.append(P.shape('M%.1f %.1f L%.1f %.1f L%.1f %.1f Z' % (x + sx * rx * 0.25, y - ry * 0.75, x + sx * rx * 0.95, y - ry * 1.55, x + sx * rx * 1.0, y - ry * 0.35), c))
            out.append(poly([(x + sx * rx * 0.45, y - ry * 0.75), (x + sx * rx * 0.88, y - ry * 1.3), (x + sx * rx * 0.88, y - ry * 0.55)], '#d89aa0'))
        # head with cheek fluff
        pts = [(x - rx * 0.6, y - ry * 0.95), (x + rx * 0.6, y - ry * 0.95), (x + rx, y - ry * 0.2), (x + rx * 1.18, y + ry * 0.45),
               (x + rx * 0.92, y + ry * 0.5), (x + rx * 1.02, y + ry * 0.78), (x + rx * 0.62, y + ry * 0.8), (x + rx * 0.45, y + ry * 1.0),
               (x, y + ry * 0.92), (x - rx * 0.45, y + ry * 1.0), (x - rx * 0.62, y + ry * 0.8), (x - rx * 1.02, y + ry * 0.78),
               (x - rx * 0.92, y + ry * 0.5), (x - rx * 1.18, y + ry * 0.45), (x - rx, y - ry * 0.2)]
        out.append(P.shape(cp.rounded_poly(pts, 14), c))
        # muzzle
        out.append(P.shape('M%.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (
            x - rx * 0.42, y + ry * 0.05, x - rx * 0.55, y + ry * 0.75, x, y + ry * 0.85,
            x + rx * 0.55, y + ry * 0.75, x + rx * 0.42, y + ry * 0.05, x, y - ry * 0.2, x - rx * 0.42, y + ry * 0.05), fur2))
        if jaw > 0.05:
            out.append(P.shape('M%.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (x - 22, y + ry * 0.55, x, y + ry * 0.55 + 40 * jaw, x + 22, y + ry * 0.55), '#7a1e2a', 4))
            for sx in (-1, 1):
                out.append(poly([(x + sx * 16, y + ry * 0.56), (x + sx * 11, y + ry * 0.56 + 14), (x + sx * 7, y + ry * 0.56)], '#ffffff', ink('#ffffff'), 2))
        out.append(P.ell(x, y + ry * 0.32, 13, 9, '#1e1a22', 3))
        out.append(ellipse(x - 4, y + ry * 0.28, 4, 2.5, '#ffffff', opacity=0.6))
        out.append(path('M%.1f %.1f L%.1f %.1f' % (x, y + ry * 0.4, x, y + ry * 0.55), 'none', '#1e1a22', 3.5))
        out.append(cp.eyes(P, dict(B, hy=B['hy'] - 18), dict(S, eye_style='fierce'), expr))
        for sx in (-1, 1):
            out.append(path('M%.1f %.1f L%.1f %.1f' % (x + sx * 36, y - 34, x + sx * 12, y - 26), 'none', shade(c, -0.45), 6))
        return ''.join(out)
    if kind == 'dragon':
        belly = S.get('belly', '#f2c27a')
        hc = S.get('horn_color', '#f1e6c8')
        for sx in (-1, 1):
            out.append(P.shape('M%.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f C%.1f %.1f %.1f %.1f %.1f %.1f Z' % (
                x + sx * rx * 0.45, y - ry * 0.7, x + sx * rx * 0.8, y - ry * 1.4, x + sx * rx * 1.25, y - ry * 1.6, x + sx * rx * 1.4, y - ry * 2.0,
                x + sx * rx * 1.5, y - ry * 1.3, x + sx * rx * 1.05, y - ry * 0.75, x + sx * rx * 0.8, y - ry * 0.5), hc))
            out.append(P.shape('M%.1f %.1f L%.1f %.1f L%.1f %.1f Z' % (x + sx * rx * 0.85, y - ry * 0.1, x + sx * rx * 1.45, y - ry * 0.35, x + sx * rx * 0.95, y + ry * 0.3), shade(c, -0.15)))
        out.append(P.shape(cp.head_path(B), c))
        # spikes
        for i, k in enumerate((-0.35, 0, 0.35)):
            out.append(P.shape('M%.1f %.1f L%.1f %.1f L%.1f %.1f Z' % (x + k * rx - 9, y - ry * 0.92, x + k * rx, y - ry * 1.3 - (8 if k == 0 else 0), x + k * rx + 9, y - ry * 0.92), hc, 3.5))
        # snout
        sn_y = y + ry * 0.45
        out.append(P.shape('M%.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (
            x - rx * 0.62, sn_y - 10, x - rx * 0.72, sn_y + 40, x, sn_y + 44,
            x + rx * 0.72, sn_y + 40, x + rx * 0.62, sn_y - 10, x, sn_y - 26, x - rx * 0.62, sn_y - 10), shade(c, 0.08)))
        for sx in (-1, 1):
            out.append(ellipse(x + sx * 15, sn_y + 2, 6, 4.5, '#1e0a0a'))
        if jaw > 0.05:
            out.append(P.shape('M%.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (x - rx * 0.5, sn_y + 26, x, sn_y + 26 + 56 * jaw, x + rx * 0.5, sn_y + 26), '#6a0e14', 4))
            if S.get('fire') and jaw > 0.6:
                out.append(P.glow(x, sn_y + 60, 60, '#ff7a2a', 0.85))
            for k in range(-2, 3):
                out.append(poly([(x + k * 12 - 4, sn_y + 27), (x + k * 12, sn_y + 38), (x + k * 12 + 4, sn_y + 27)], '#fffaf0'))
        else:
            out.append(path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x - rx * 0.5, sn_y + 28, x, sn_y + 36, x + rx * 0.5, sn_y + 28), 'none', ink(c), 4))
            for sx in (-1, 1):
                out.append(poly([(x + sx * 20, sn_y + 29), (x + sx * 16, sn_y + 40), (x + sx * 12, sn_y + 31)], '#fffaf0', ink('#fffaf0'), 2))
        out.append(cp.eyes(P, dict(B, hy=B['hy'] - 22), dict(S, eye_style=S.get('eye_style', 'fierce'), no_brows=True), expr))
        for sx in (-1, 1):
            out.append(path('M%.1f %.1f L%.1f %.1f' % (x + sx * 44, y - 30, x + sx * 12, y - 18), 'none', shade(c, -0.5), 7))
        return ''.join(out)
    return ''


def beast_torso(P, B, S):
    kind = S['head']
    x, ty, hipy = 200, B['ty'], B['hipy']
    c = S['skin']
    if kind == 'wolf':
        fur2 = S.get('fur2', '#e8e4dc')
        out = [P.shape('M%.1f %.1f Q%.1f %.1f %.1f %.1f L%.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (
            x - 64, ty + 10, x, ty - 30, x + 64, ty + 10, x + 50, hipy + 10, x, hipy + 26, x - 50, hipy + 10), c)]
        out.append(P.shape('M%.1f %.1f L%.1f %.1f L%.1f %.1f L%.1f %.1f L%.1f %.1f L%.1f %.1f L%.1f %.1f Z' % (
            x - 30, ty, x + 30, ty, x + 26, ty + 40, x + 14, ty + 34, x, ty + 56, x - 14, ty + 34, x - 26, ty + 40), fur2, 4))
        return ''.join(out)
    belly = S.get('belly', '#f2c27a')
    out = [P.shape(cp.rounded_poly([(x - B['wt'], ty + 2), (x + B['wt'], ty + 2), (x + B['wb'] + 6, hipy + 12), (x - B['wb'] - 6, hipy + 12)], 30), c)]
    out.append(P.shape('M%.1f %.1f Q%.1f %.1f %.1f %.1f Q%.1f %.1f %.1f %.1f Z' % (x - B['wt'] * 0.55, ty + 6, x - B['wb'] * 0.9, hipy + 16, x, hipy + 14, x + B['wb'] * 0.9, hipy + 16, x + B['wt'] * 0.55, ty + 6), belly, 4))
    for i in range(5):
        yy = ty + 22 + i * (hipy - ty) / 5.5
        w = B['wt'] * 0.5 + i * 4
        out.append(path('M%.1f %.1f Q%.1f %.1f %.1f %.1f' % (x - w, yy, x, yy + 7, x + w, yy), 'none', shade(belly, -0.3), 3))
    return ''.join(out)


# =========================================================================== compose


def _tf(*parts):
    return ' '.join(p for p in parts if p)


def compose(S, pose, defs, frame_seed=0):
    P = cp.Painter(defs)
    if 'skin' not in S:
        S = dict(S, skin=S.get('bone', '#ece4cf'))
    B = dict(cp.BODY[S.get('body', 'human')])
    for k, v in S.get('body_mod', {}).items():
        B[k] = B[k] * v if isinstance(v, float) and k in ('hrx', 'hry', 'wt', 'wb', 'limb', 'arm', 'sx') else v
    p = dict(pose)
    hip = (200, B['hipy'])
    feet = (200, B['footy'])
    sc = S.get('scale', 1.0)
    root = _tf('translate(%.2f %.2f)' % (p.get('rx', 0), p.get('ry', 0)),
               'translate(%.2f %.2f) rotate(%.2f) scale(%.4f %.4f) translate(%.2f %.2f)' % (
                   feet[0], feet[1], p.get('rr', 0), sc * p.get('sx', 1), sc * p.get('sy', 1), -feet[0], -feet[1]))
    torso_t = _tf(root, 'rotate(%.2f %.2f %.2f)' % (p.get('tr', 0), hip[0], hip[1]))
    neck = (B['hx'], B['ty'])
    hs = p.get('hs', 1.0)
    head_t = _tf(torso_t, 'translate(0 %.2f)' % p.get('hy', 0), 'rotate(%.2f %.2f %.2f)' % (p.get('hr', 0), neck[0], neck[1]),
                 'translate(%.2f %.2f) scale(%.3f) translate(%.2f %.2f)' % (neck[0], neck[1], hs, -neck[0], -neck[1]) if hs != 1 else '')
    shF = (200 - B['sx'], B['sy'])
    shB = (200 + B['sx'], B['sy'])
    armF_t = _tf(torso_t, 'translate(%.2f %.2f) rotate(%.2f)' % (shF[0], shF[1], p.get('aF', 12)))
    armB_t = _tf(torso_t, 'translate(%.2f %.2f) rotate(%.2f)' % (shB[0], shB[1], p.get('aB', -12)))
    face = p.get('face', 'normal')
    kind = S.get('head', 'human')
    beast = kind in ('wolf', 'dragon')
    L = B['arm']
    layers = []
    fx_back, fx_front = fx_layers(P, S, B, p, frame_seed, torso_t, armF_t)

    # ground shadow (follows only horizontal motion)
    layers.append(ellipse(200 + p.get('rx', 0), B['footy'] + 2, 74 * sc * (1 - min(0.4, abs(p.get('ry', 0)) / 60)), 13 * sc,
                          defs.rad([(0, '#000000', 0.38), (1, '#000000', 0)])))
    layers.append(fx_back)
    layers.append(g(cp.tail(P, B, S, p.get('tail', 0)), root))
    layers.append(g(cp.wings(P, B, S, p.get('flap', 0)), torso_t))
    layers.append(g(cp.back_items(P, B, S), torso_t))
    if not beast:
        layers.append(g(cp.headgear_back(P, B, S) + cp.hair_back(P, B, S), head_t))
    # off arm behind the body when it carries nothing in front
    off = S.get('offhand')
    for side, key in ((1, 'lB'), (-1, 'lF')):
        layers.append(g(cp.leg(P, B, S, side), _tf(root, 'rotate(%.2f %.2f %.2f)' % (p.get(key, 0), 200 + side * B['hipx'], hip[1]))))
    if beast:
        layers.append(g(beast_torso(P, B, S), torso_t))
        if S.get('torso', {}).get('kind') == 'robe':
            layers.append(g(cp.torso(P, B, S), torso_t))
    else:
        layers.append(g(cp.torso(P, B, S), torso_t))
    if S.get('arms', {}).get('kind') != 'none':
        arm_b = cp.arm(P, B, S, 1)
        if off:
            arm_b += g(cp.offhand(P, off, S), 'translate(0 %.2f) rotate(%.2f)' % (L, p.get('wB', 0)))
        if S.get('weapon2'):
            arm_b += g(cp.weapon(P, S['weapon2'], S), 'translate(0 %.2f) rotate(%.2f)' % (L, p.get('wB', 0) + 25))
        layers.append(g(arm_b, armB_t))
    if beast:
        layers.append(g(beast_head(P, B, S, face, p.get('jaw', 0)), head_t))
    elif kind == 'hidden':
        hidden_eyes = '' if S.get('no_eyes') else cp.eyes(P, dict(B, hy=B['hy'] + 4), dict(S, eye_style='glow'), face)
        layers.append(g(cp.headgear_front(P, B, S) + hidden_eyes, head_t))
    else:
        covered = S.get('headgear') in ('helm_knight', 'dragon_helm', 'horned_helm', 'winged_helm')
        layers.append(g(cp.face(P, B, S, face) + ('' if covered else cp.hair_front(P, B, S)) + cp.headgear_front(P, B, S), head_t))
    if S.get('arms', {}).get('kind') != 'none':
        wpn = S.get('weapon')
        arm_f = ''
        wg = cp.weapon(P, wpn, S) if wpn else ''
        if p.get('tipglow'):
            tx, ty = WEAPON_TIP.get(wpn, (0, 0))
            gc = S.get('aura', '#ffe08a')
            wg += P.glow(tx, ty, 40 * p['tipglow'], gc, 0.95) + poly(star_points(tx, ty, 24 * p['tipglow'], 6 * p['tipglow'], 4, (frame_seed * 23) % 90), '#ffffff', None, None, opacity=0.9)
        behind = S.get('weapon_behind_arm', False)
        weapon_g = g(wg, 'translate(0 %.2f) rotate(%.2f)' % (L, p.get('wF', -20)))
        arm_f = (weapon_g + cp.arm(P, B, S, -1)) if behind else (cp.arm(P, B, S, -1) + weapon_g)
        layers.append(g(arm_f, armF_t))
    layers.append(fx_front)
    body = ''.join(layers)
    if p.get('alpha', 1) < 1:
        body = g(body, None, opacity='%.3f' % p['alpha'])
    if p.get('tint'):
        body = g(body)  # tint is applied at runtime (hit flash); kept for future use
    return body


# =========================================================================== FX baked into frames


def fx_layers(P, S, B, p, seed, torso_t, armF_t):
    back, front = [], []
    aura = S.get('aura', '#8fd6ff')
    fx = p.get('fx', ())
    cx, cy = 200, (B['hy'] + B['footy']) / 2
    if 'aura' in fx:
        k = p.get('fxk', 1.0)
        back.append(P.glow(cx, cy + 10, 170 * k, aura, 0.55))
        back.append(ellipse(cx, B['footy'], 120 * k, 26 * k, 'none', aura, 6, opacity=0.8 * k))
        back.append(ellipse(cx, B['footy'], 90 * k, 18 * k, 'none', '#ffffff', 3, opacity=0.6 * k))
        for i in range(10):
            a = (i * 137.5 + seed * 40) % 360
            r = 60 + (i * 29 % 90)
            px = cx + math.cos(math.radians(a)) * r
            py = B['footy'] - ((seed * 34 + i * 47) % 260)
            s = 4 + (i % 3) * 2.5
            front.append(poly(star_points(px, py, s * 1.8 * k, s * 0.5 * k, 4, 0), '#ffffff' if i % 2 else aura, None, None, opacity=0.9))
    if 'charge' in fx:
        k = p.get('fxk', 1.0)
        for i in range(3):
            r = (140 - i * 40) * (1.2 - k * 0.6)
            front.append(circle(cx, cy - 10, max(10, r), 'none', aura, 5 - i, opacity=0.35 + 0.2 * i))
    if 'swoosh' in fx:
        k = p.get('fxk', 1.0)
        sw = S.get('swoosh', '#ffffff')
        d = 'M%.1f %.1f A 150 150 0 0 0 %.1f %.1f A 120 120 0 0 1 %.1f %.1f Z' % (60, 40, 260, 330, 90, 70)
        front.append(g(path(d, P.d.lin([(0, sw, 0), (0.5, sw, 0.85 * k), (1, aura, 0.5 * k)], 0, 0, 1, 1)), torso_t))
    if 'swoosh2' in fx:
        sw = S.get('swoosh', '#ffffff')
        d = 'M%.1f %.1f A 150 150 0 0 0 %.1f %.1f A 130 130 0 0 1 %.1f %.1f Z' % (120, 60, 300, 330, 140, 80)
        front.append(g(path(d, P.d.lin([(0, sw, 0), (0.6, sw, 0.45), (1, aura, 0.2)], 0, 0, 1, 1)), torso_t))
    if 'burst' in fx:
        k = p.get('fxk', 1.0)
        front.append(P.glow(cx, cy - 30, 200 * k, aura, 0.7))
        front.append(poly(star_points(cx, cy - 30, 190 * k, 50 * k, 8, seed * 11), '#ffffff', None, None, opacity=0.55))
    if 'zzz' in fx:
        for i in range(3):
            front.append(path('M%.1f %.1f l12 0 l-12 12 l12 0' % (250 + i * 18, 70 - i * 22 - seed * 4), 'none', '#ffffff', 3.5, opacity=0.8))
    return ''.join(back), ''.join(front)


# =========================================================================== animation clips


def _lerp(a, b, t):
    out = dict(a)
    for k, v in b.items():
        if isinstance(v, (int, float)) and isinstance(a.get(k, 0), (int, float)):
            out[k] = a.get(k, REST.get(k, 0)) + (v - a.get(k, REST.get(k, 0))) * t
        else:
            out[k] = v
    return out


REST = dict(rx=0, ry=0, rr=0, sx=1, sy=1, tr=0, hr=0, hy=0, hs=1, aF=12, wF=-20, aB=-12, wB=0, lF=0, lB=0, alpha=1, flap=0, tail=0, jaw=0, tipglow=0, fxk=1)


def P_(**kw):
    d = dict(REST)
    d.update(kw)
    return d


def clips(S):
    a = archetype(S)
    rest = dict(REST)
    rest.update(S.get('rest', {}))
    R = lambda **kw: dict(rest, **kw)
    beast = S.get('head') in ('wolf', 'dragon')
    winged = bool(S.get('wings'))
    C = {}
    # ---- idle (loop): breathing, weapon sway, blink on one frame
    idle = []
    n = 8
    for i in range(n):
        t = i / n * math.tau
        idle.append(R(ry=-2.5 * (1 - math.cos(t)) / 2 * 2, sy=1 + 0.018 * math.sin(t), sx=1 - 0.008 * math.sin(t), hy=1.5 * math.sin(t + 0.6), hr=1.2 * math.sin(t),
                      aF=rest['aF'] + 3 * math.sin(t), aB=rest['aB'] - 3 * math.sin(t), wF=rest['wF'] + 2 * math.sin(t + 1),
                      flap=0.6 * math.sin(t) if winged else 0, tail=math.sin(t),
                      face='blink' if i == 5 else S.get('idle_face', 'normal')))
    C['idle'] = dict(frames=idle, fps=8, loop=True)

    # ---- attack
    if beast:
        atk = [R(hy=-6, hr=-6, sy=1.03, face='angry', jaw=0.2, flap=-0.8 if winged else 0),
               R(hy=-12, hr=-8, ry=-6, sy=1.05, face='angry', jaw=0.6, flap=-1 if winged else 0),
               R(hy=18, hs=1.12, ry=6, sy=0.95, face='angry', jaw=1.0, fx=('swoosh2',), flap=0.8 if winged else 0),
               R(hy=14, hs=1.08, ry=4, face='angry', jaw=0.8, flap=1 if winged else 0),
               R(hy=4, face='angry', jaw=0.3),
               R()]
    elif a == 'ranged':
        atk = [R(aF=95, wF=-95, aB=-70, wB=0, tr=-3, face='angry'),
               R(aF=110, wF=-110, aB=-40, tr=-5, face='angry', sy=1.02),
               R(aF=110, wF=-110, aB=-30, tr=-6, face='angry', sy=1.03),
               R(aF=104, wF=-104, aB=-95, tr=3, face='angry', tipglow=0.8, ry=2),
               R(aF=80, wF=-70, aB=-50, tr=2, face='normal'),
               R()]
    elif a in ('caster', 'bard'):
        atk = [R(aF=60, wF=-50, aB=-40, tr=-3, face='cast', tipglow=0.3),
               R(aF=120, wF=-110, aB=-80, tr=-6, ry=-4, face='cast', tipglow=0.6),
               R(aF=140, wF=-130, aB=-100, tr=-8, ry=-8, face='cast', tipglow=1.0),
               R(aF=100, wF=-80, aB=-60, tr=5, ry=2, face='angry', tipglow=1.3, fx=('burst',), fxk=0.45),
               R(aF=50, wF=-40, aB=-30, tr=2, face='normal', tipglow=0.3),
               R()]
        if a == 'bard':
            atk = [R(aF=30, wF=-60, aB=-40, face='happy'), R(aF=40, wF=-70, aB=-70, face='happy', tipglow=0.5),
                   R(aF=35, wF=-65, aB=-30, face='happy', tipglow=1.0, fx=('burst',), fxk=0.35), R(aF=40, wF=-70, aB=-70, face='happy', tipglow=0.6),
                   R(aF=30, wF=-60, aB=-30), R()]
    elif a == 'heavy':
        atk = [R(aF=110, wF=-10, tr=-6, sy=1.02, face='angry'),
               R(aF=170, wF=15, tr=-10, ry=-8, sy=1.04, face='angry'),
               R(aF=175, wF=20, tr=-12, ry=-12, sy=1.05, face='angry'),
               R(aF=-30, wF=70, tr=10, ry=8, sy=0.94, face='angry', fx=('swoosh',)),
               R(aF=-45, wF=85, tr=12, ry=10, sy=0.93, face='angry', fx=('swoosh2',)),
               R(aF=-10, wF=40, tr=4, ry=3, face='angry'),
               R()]
    else:
        atk = [R(aF=100, wF=-15, tr=-4, face='angry', sy=1.02),
               R(aF=160, wF=10, tr=-8, ry=-6, face='angry', sy=1.04),
               R(aF=-25, wF=60, tr=8, ry=6, face='angry', sy=0.96, fx=('swoosh',)),
               R(aF=-40, wF=75, tr=10, ry=7, face='angry', fx=('swoosh2',)),
               R(aF=-10, wF=30, tr=3, ry=2),
               R()]
    C['attack'] = dict(frames=atk, fps=14, loop=False)

    # ---- skill: charge up with aura, then a big release
    up = dict(aF=150 if a not in ('ranged',) else 120, wF=-140 if a in ('caster', 'bard', 'ranged') else -20, aB=-150)
    if beast:
        sk = [R(sy=0.94, ry=4, face='angry', fx=('aura', 'charge'), fxk=0.4, flap=-1 if winged else 0),
              R(sy=0.92, ry=6, face='angry', fx=('aura', 'charge'), fxk=0.7, jaw=0.3, flap=-1 if winged else 0),
              R(sy=1.06, ry=-14, face='angry', fx=('aura', 'charge'), fxk=1.0, jaw=0.5, hy=-10, flap=1 if winged else 0),
              R(sy=1.05, ry=-18, face='angry', fx=('aura',), fxk=1.0, jaw=0.9, hy=-14, flap=-1 if winged else 0),
              R(sy=0.95, ry=6, face='angry', fx=('aura', 'burst'), fxk=1.0, jaw=1.0, hy=20, hs=1.15, flap=1 if winged else 0),
              R(sy=0.96, ry=4, face='angry', fx=('aura', 'burst'), fxk=0.7, jaw=1.0, hy=16, hs=1.1),
              R(face='angry', fx=('aura',), fxk=0.35, jaw=0.4, hy=6),
              R()]
    else:
        sk = [R(sy=0.94, ry=4, aF=40, aB=-40, face='cast', fx=('aura', 'charge'), fxk=0.4, tipglow=0.3),
              R(sy=0.96, ry=0, aF=90, aB=-90, face='cast', fx=('aura', 'charge'), fxk=0.7, tipglow=0.6, wF=up['wF'] * 0.6),
              R(sy=1.04, ry=-10, face='cast', fx=('aura', 'charge'), fxk=1.0, tipglow=1.0, **up),
              R(sy=1.05, ry=-16, face='cast', fx=('aura',), fxk=1.0, tipglow=1.4, **up),
              R(sy=0.94, ry=6, tr=8, aF=-30 if a in ('melee', 'heavy') else 100, wF=70 if a in ('melee', 'heavy') else -90, aB=-60,
                face='angry', fx=('aura', 'burst') + (('swoosh',) if a in ('melee', 'heavy') else ()), fxk=1.0, tipglow=1.6),
              R(sy=0.95, ry=5, tr=9, aF=-40 if a in ('melee', 'heavy') else 90, wF=80 if a in ('melee', 'heavy') else -80, aB=-50,
                face='angry', fx=('aura', 'burst'), fxk=0.7, tipglow=1.0),
              R(face='normal', fx=('aura',), fxk=0.35, tipglow=0.3),
              R()]
    C['skill'] = dict(frames=sk, fps=12, loop=False)

    # ---- hit
    C['hit'] = dict(frames=[R(tr=-10, hr=-9, ry=2, sx=1.05, sy=0.95, face='hurt', aF=rest['aF'] + 20, aB=rest['aB'] - 20, jaw=0.4),
                            R(tr=-6, hr=-5, face='hurt', aF=rest['aF'] + 10, aB=rest['aB'] - 10, jaw=0.2),
                            R(tr=-2, hr=-1, face='normal')], fps=12, loop=False)

    # ---- death: stagger, knees, fall over and fade
    C['death'] = dict(frames=[R(tr=-12, hr=-10, face='hurt', aF=40, aB=-40),
                              R(tr=-6, sy=0.9, ry=8, face='hurt', aF=30, aB=-30, hr=8),
                              R(rr=-22, ry=10, sy=0.92, face='hurt', aF=60, aB=-20, hr=12),
                              R(rr=-55, ry=14, face='dead', aF=80, aB=-10, hr=14, alpha=0.95),
                              R(rr=-82, ry=18, face='dead', aF=95, aB=0, hr=10, alpha=0.8),
                              R(rr=-88, ry=20, face='dead', aF=100, aB=0, hr=8, alpha=0.55)], fps=10, loop=False)

    # ---- victory (loop): hop with weapon raised
    vy = (0, -10, -20, -24, -20, -10)
    vic = []
    for i, yy in enumerate(vy):
        vic.append(R(ry=yy, sy=1.04 if yy else 0.96, aF=165 if not beast else rest['aF'], wF=(-150 if a in ('caster', 'bard', 'ranged') else 0) if not beast else rest['wF'],
                     aB=-120 + yy, face='happy', tipglow=0.6 if a in ('caster',) else 0, flap=(-1 if i % 2 else 1) if winged else 0, jaw=0.4 if beast else 0))
    C['victory'] = dict(frames=vic, fps=10, loop=True)
    return C


# =========================================================================== rendering


def frame_svg(S, pose, seed=0, vb=VIEW, size=360, background=''):
    d = Defs('f%d' % seed)
    body = compose(S, pose, d, seed)
    return document(background + body, size, size, vb, d)


def bounds_head(S):
    B = dict(cp.BODY[S.get('body', 'human')])
    for k, v in S.get('body_mod', {}).items():
        B[k] = B[k] * v if isinstance(v, float) and k in ('hrx', 'hry', 'wt', 'wb', 'limb', 'arm', 'sx') else v
    return B


def render_character(S, out_dir, frame_px=360, portrait_px=512, icon_px=256):
    os.makedirs(os.path.join(out_dir, 'anim'), exist_ok=True)
    C = clips(S)
    manifest = {'id': S['id'], 'frameSize': frame_px, 'viewBox': VIEW, 'feetY': (cp.BODY[S.get('body', 'human')]['footy'] - VIEW[1]) / VIEW[3],
                'heightRatio': 0.0, 'clips': {}}
    for name, clip in C.items():
        files = []
        for i, pose in enumerate(clip['frames']):
            img = rasterize(frame_svg(S, pose, i + 1, VIEW, frame_px))
            fn = '%s_%02d.png' % (name, i)
            save(img, os.path.join(out_dir, 'anim', fn))
            files.append(fn)
        manifest['clips'][name] = {'fps': clip['fps'], 'loop': clip['loop'], 'frames': files}
    # character height (for display scale) from the first idle frame's alpha bounds
    first = rasterize(frame_svg(S, C['idle']['frames'][0], 1, VIEW, frame_px))
    bb = first.getbbox()
    manifest['heightRatio'] = round((bb[3] - bb[1]) / frame_px, 4) if bb else 0.7
    manifest['bbox'] = [round(v / frame_px, 4) for v in bb] if bb else None
    with open(os.path.join(out_dir, 'anim.json'), 'w') as f:
        json.dump(manifest, f, indent=1)
    render_portraits(S, out_dir, portrait_px, icon_px)
    return manifest


def head_extents(S):
    """(head_top, head_center_y, head_bottom, feet) in canvas units after root scaling."""
    B = bounds_head(S)
    sc = S.get('scale', 1.0)
    feet = B['footy']
    f = lambda y: feet + (y - feet) * sc
    top = f(B['hy'] - B['hry'] * 1.15) - S.get('portrait_top', 0) * sc
    if S.get('head') == 'wolf':
        top = f(B['hy'] - B['hry'] * 1.6)
    return top, f(B['hy']), f(B['hy'] + B['hry']), feet


def portrait_box(S):
    top, cy, bottom, feet = head_extents(S)
    side = max(bottom - top + 70 * S.get('scale', 1.0), 170 * S.get('scale', 1.0)) + S.get('portrait_zoom', 0)
    return (200 - side / 2, top - 12, side, side)


def render_portraits(S, out_dir, portrait_px=512, icon_px=256):
    rest = dict(REST)
    rest.update(S.get('rest', {}))
    pose = dict(rest, face=S.get('portrait_face', 'normal'))
    x, y, w, h = portrait_box(S)
    side = max(w, h)
    vb = (x + w / 2 - side / 2, y, side, side)
    img = rasterize(frame_svg(S, pose, 0, vb, portrait_px))
    save(img, os.path.join(out_dir, 'portrait.png'))
    # round icon: rarity-agnostic dark backdrop + bust, ring drawn by the UI
    d = Defs('ic')
    body = compose(S, pose, d, 0)
    c = S.get('aura', '#6a8ac0')
    cx, cy = vb[0] + side / 2, vb[1] + side / 2
    r = side / 2
    bg = circle(cx, cy, r * 0.98, d.rad([(0, shade(c, 0.15)), (0.7, shade(c, -0.45)), (1, shade(c, -0.7))], 0.5, 0.35, 0.75))
    clip = d.clip('<circle cx="%.2f" cy="%.2f" r="%.2f"/>' % (cx, cy, r * 0.98))
    rim = circle(cx, cy, r * 0.96, 'none', '#ffffff', side * 0.012, opacity=0.35)
    svg = document(bg + g(body, None, clip_path=clip) + rim, icon_px, icon_px, vb, d)
    save(rasterize(svg), os.path.join(out_dir, 'icon.png'))
    # card bust: taller crop (3:4) for hero cards, head to knees
    top, cy, bottom, feet = head_extents(S)
    ch = (feet - top) * 0.92 + 30
    cw = ch * 3 / 4
    vb2 = (200 - cw / 2, top - 22, cw, ch)
    d2 = Defs('cb')
    body2 = compose(S, dict(rest, face=S.get('card_face', S.get('portrait_face', 'normal'))), d2, 0)
    save(rasterize(document(body2, 600, 800, vb2, d2)), os.path.join(out_dir, 'card_bust.png'))
