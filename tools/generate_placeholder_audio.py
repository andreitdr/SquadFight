#!/usr/bin/env python3
"""Generates placeholder SFX and music loops (pure synthesis, released as CC0 by this project).
Usage: tools/generate_placeholder_audio.py <output_dir>"""
import math, os, random, struct, sys, wave

SR = 22050
out = sys.argv[1] if len(sys.argv) > 1 else "Assets/_Game/Audio/Placeholder"
os.makedirs(out, exist_ok=True)
rng = random.Random(7)

def write(name, samples):
    path = os.path.join(out, name + ".wav")
    peak = max(1e-6, max(abs(s) for s in samples))
    k = 0.85 / peak if peak > 0.85 else 1.0
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s * k)) * 32767)) for s in samples))

def env(i, n, attack=0.01, release=0.3):
    t = i / SR; dur = n / SR
    a = min(1.0, t / attack) if attack > 0 else 1.0
    r = min(1.0, (dur - t) / max(1e-4, dur * release))
    return max(0.0, a * r)

def tone(freq, dur, wave_type="sine", vol=0.6, sweep=0.0, noise=0.0, attack=0.005, release=0.5):
    n = int(SR * dur); s = []; ph = 0.0
    for i in range(n):
        f = freq * (1 + sweep * i / n)
        ph += 2 * math.pi * f / SR
        if wave_type == "square": v = 1.0 if math.sin(ph) > 0 else -1.0
        elif wave_type == "tri": v = 2 / math.pi * math.asin(math.sin(ph))
        elif wave_type == "saw": v = ((ph / math.pi) % 2) - 1
        else: v = math.sin(ph)
        v = v * (1 - noise) + (rng.random() * 2 - 1) * noise
        s.append(v * vol * env(i, n, attack, release))
    return s

def mix(*tracks):
    n = max(len(t) for t in tracks); r = [0.0] * n
    for t in tracks:
        for i, v in enumerate(t): r[i] += v
    return r

def seq(*parts):
    r = []
    for p in parts: r += p
    return r

def silence(d): return [0.0] * int(SR * d)

N = lambda m: 440 * 2 ** ((m - 69) / 12)

write("ui_click", tone(1200, 0.05, "square", 0.3, release=0.8))
write("hit", tone(180, 0.12, "square", 0.5, sweep=-0.5, noise=0.5))
write("crit", mix(tone(140, 0.25, "saw", 0.5, sweep=-0.6, noise=0.4), tone(900, 0.12, "square", 0.25, sweep=-0.4)))
write("heal", seq(tone(N(76), 0.08, "tri", 0.4), tone(N(79), 0.08, "tri", 0.4), tone(N(84), 0.2, "tri", 0.4)))
write("skill", tone(300, 0.3, "saw", 0.4, sweep=1.5, noise=0.1))
write("death", tone(400, 0.45, "square", 0.4, sweep=-0.8, noise=0.2, release=0.7))
write("victory", seq(*[tone(N(m), d, "square", 0.35) for m, d in [(72, .12), (76, .12), (79, .12), (84, .45)]]))
write("defeat", seq(*[tone(N(m), d, "tri", 0.4) for m, d in [(67, .2), (63, .2), (60, .5)]]))
write("unlock", mix(seq(*[tone(N(m), 0.09, "square", 0.3) for m in (72, 76, 79, 84, 88)]), seq(silence(0.45), tone(N(96), 0.5, "tri", 0.3))))
write("starup", seq(tone(N(79), 0.08, "tri", 0.4), tone(N(86), 0.25, "tri", 0.4)))
write("reward", tone(N(88), 0.12, "tri", 0.35, release=0.7))
write("equip", tone(220, 0.1, "square", 0.35, noise=0.3))
write("sell", seq(tone(N(84), 0.06, "square", 0.3), tone(N(91), 0.12, "square", 0.3)))
write("boss", mix(tone(70, 0.9, "saw", 0.5, sweep=0.2, noise=0.15, release=0.6), tone(105, 0.9, "square", 0.25)))
write("battle_start", seq(tone(N(60), 0.1, "square", 0.35), tone(N(67), 0.25, "square", 0.35)))

def loop(chords, bpm, bars_per_chord, lead_scale, name, wave_lead="square", seed=1):
    r = random.Random(seed)
    beat = 60 / bpm
    track = []
    for chord in chords:
        for _ in range(bars_per_chord):
            for b in range(4):
                bass = tone(N(chord[0] - 12), beat * 0.9, "tri", 0.45, release=0.4) + silence(beat * 0.1)
                arp = []
                for k in range(2):
                    note = chord[(b * 2 + k) % len(chord)] + 12
                    arp += tone(N(note), beat * 0.45, wave_lead, 0.12, release=0.5) + silence(beat * 0.05)
                lead = silence(beat)
                if r.random() < 0.35:
                    m = r.choice(lead_scale)
                    lead = tone(N(m + 12), beat * 0.9, "tri", 0.14, release=0.6) + silence(beat * 0.1)
                track += mix(bass, arp, lead)
    write(name, track)

loop([[57, 60, 64], [53, 57, 60], [48, 52, 55], [55, 59, 62]], 96, 1, [69, 72, 74, 76, 79], "music_menu", "tri", 3)
loop([[50, 53, 57], [46, 50, 53], [48, 52, 55], [45, 49, 52]], 132, 1, [62, 65, 67, 69, 72], "music_battle", "square", 5)
loop([[45, 48, 52], [44, 48, 51], [41, 45, 48], [40, 44, 47]], 140, 1, [57, 60, 63, 64, 67], "music_boss", "saw", 9)
print("ok", sorted(os.listdir(out)))
