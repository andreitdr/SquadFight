"""Contact sheets for quick visual review: python preview.py out.png id [id ...] [--clips idle,attack]"""
import sys

from PIL import Image, ImageDraw

import char_rig as rig
import roster
from svgkit import rasterize


def sheet(ids, clip_names=('idle', 'attack', 'skill', 'hit', 'death', 'victory'), px=180, first_only=False):
    rows = []
    for cid in ids:
        S = roster.by_id(cid)
        C = rig.clips(S)
        frames = []
        for name in clip_names:
            fr = C[name]['frames']
            for i, pose in enumerate(fr[:1] if first_only else fr):
                frames.append(rasterize(rig.frame_svg(S, pose, i + 1, rig.VIEW, px)))
        rows.append(frames)
    cols = max(len(r) for r in rows)
    out = Image.new('RGBA', (cols * px, len(rows) * px), (60, 66, 84, 255))
    d = ImageDraw.Draw(out)
    for j in range(len(rows)):
        for i in range(cols):
            if (i + j) % 2:
                d.rectangle([i * px, j * px, (i + 1) * px, (j + 1) * px], fill=(70, 76, 96, 255))
    for j, r in enumerate(rows):
        for i, im in enumerate(r):
            out.alpha_composite(im, (i * px, j * px))
    return out


if __name__ == '__main__':
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    opts = [a for a in sys.argv[1:] if a.startswith('--')]
    clips = ('idle', 'attack', 'skill', 'hit', 'death', 'victory')
    first = False
    px = 180
    for o in opts:
        if o.startswith('--clips='):
            clips = tuple(o.split('=')[1].split(','))
        if o == '--first':
            first = True
        if o.startswith('--px='):
            px = int(o.split('=')[1])
    sheet(args[1:], clips, px, first).save(args[0])
