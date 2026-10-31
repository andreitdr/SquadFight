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
        if cid in roster.ORIGINAL_IDS:
            rig.render_portraits(S, os.path.join(CHAR_DIR, cid))
        else:
            rig.render_portraits(S, os.path.join(CHAR_DIR, cid), 384, 192, (450, 600))
        return cid, 0
    boss = cid in roster.BOSSES
    out = os.path.join(CHAR_DIR, cid)
    if cid in roster.ORIGINAL_IDS:
        m = rig.render_character(S, out, frame_px=512 if boss else 360)
    else:
        # The 172 characters added for "The Hundred": smaller frames and lighter slow clips to keep the build small.
        m = rig.render_character(S, out, frame_px=336 if boss else 240, portrait_px=384, icon_px=192, card_px=(450, 600), lite=True)
    return cid, sum(len(c['frames']) for c in m['clips'].values())


if __name__ == '__main__':
    ids = [a for a in sys.argv[1:] if not a.startswith('--')] or [s['id'] for s in roster.HEROES + roster.ENEMIES]
    with ProcessPoolExecutor() as ex:
        for cid, n in ex.map(one, ids):
            print('%-22s %d frames' % (cid, n), flush=True)
