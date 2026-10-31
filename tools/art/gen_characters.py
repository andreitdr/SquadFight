"""Renders every hero and enemy: animation frames, anim.json, portrait, round icon and card bust.

Usage: python gen_characters.py [id ...]   (no ids = everything)
"""
import os
import sys
from concurrent.futures import ProcessPoolExecutor

import char_rig as rig
import roster
from paths import CHAR_DIR


PORTRAITS_ONLY = '--portraits' in sys.argv


def one(cid):
    S = roster.by_id(cid)
    if PORTRAITS_ONLY:
        rig.render_portraits(S, os.path.join(CHAR_DIR, cid))
        return cid, 0
    boss = cid in roster.BOSSES
    out = os.path.join(CHAR_DIR, cid)
    # The 172 characters added for "The Hundred" use smaller frames to keep the build size down.
    px = (512 if boss else 360) if cid in roster.ORIGINAL_IDS else (400 if boss else 288)
    m = rig.render_character(S, out, frame_px=px)
    return cid, sum(len(c['frames']) for c in m['clips'].values())


if __name__ == '__main__':
    ids = [a for a in sys.argv[1:] if not a.startswith('--')] or [s['id'] for s in roster.HEROES + roster.ENEMIES]
    with ProcessPoolExecutor() as ex:
        for cid, n in ex.map(one, ids):
            print('%-22s %d frames' % (cid, n), flush=True)
