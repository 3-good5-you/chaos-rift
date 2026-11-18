"""Sounds for the Unlock Lab (2026-11-16, round 2): a new card is revealed and marked NEW. Round 1 was rejected as
too musical ("not unlocking a Xmas song"): these have no notes at all, only air, snaps, hits and noise textures,
like a modern card game's reveal. Built from the sfxforge pieces.

    python game/tools/unlockforge.py game/Godot/assets/audio/gen      -> U_<name>.wav

HIT gives each sound's main hit (seconds from its start); UnlockLab.cs lines that up with the animation's hit.
"""
import os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(__file__))
import sfxforge as f
from sfxforge import *

S = {}
def snd(fn):
    S[fn.__name__] = fn
    return fn

def hit(d=0.6, f0=150, f1=38, punch=1.0, seed=0):
    """A tight, dry, saturated hit: snap + sub kick + body."""
    snap = add(band(noise(0.05, seed), 2500, 14000) * env_ad(0.05, 0.0003, 0.007) * 1.4, click(0.012, 600, 8000, seed + 1) * 1.6)
    kick = sine(glide(f0, f1, d, 0.35), d) * env_ad(d, 0.001, 0.14) * 1.4
    body = band(noise(0.2, seed + 2), 150, 1500) * env_ad(0.2, 0.001, 0.035) * 0.8
    return np.tanh(mix(d, (0, snap, 1), (0, kick, punch), (0, body, 1)) * 2.0) * 0.9

def air(d, lo=5000, hi=15000, decay=0.25, seed=0):
    """A breath of high air after a hit (the 'sheen', no pitch)."""
    return band(noise(d, seed), lo, hi) * env_ad(d, 0.004, decay)

# 1: a fast whoosh into a crisp card snap with a little sub weight. Hit 0.22.
@snd
def snap_air():
    d = 1.0
    w = whoosh(0.24, 700, 5000, 0.2, 1.3, 11, air=0.4) * 0.7
    snap = add(card_flip_snap() * 1.2, hit(0.35, 120, 45, 0.5, 12) * 0.6)
    return mix(d, (0, w, 1), (0.22, snap, 1), (0.23, air(0.6, 6000, 15000, 0.12, 13), 0.25))

# 2: a tense noise riser that stops dead, then a heavy hit. Hit 0.5.
@snd
def riser_hit():
    d = 1.6
    rise = svf(noise(0.47, 21), glide(300, 9000, 0.47, 2.2), 2.2) * env_swell(0.47, 0.46, 0.01) * 0.55
    rise = add(rise, crackle(0.47, 60, 4000, 12000, 4.0, 22) * 0.25)
    tail = room(band(noise(0.9, 23), 60, 400) * env_ad(0.9, 0.002, 0.25) * 0.4, 0.8, 0.35, 3000)
    return mix(d, (0, rise, 1), (0.5, hit(0.8, 160, 32, 1.0, 24), 1), (0.5, tail, 1), (0.51, air(0.8, 5000, 14000, 0.2, 25), 0.3))

# 3: a reversed breath swelling into a soft, low thud (lift and drop). Hit 0.5.
@snd
def reverse_swell():
    d = 1.4
    rev = (band(noise(0.5, 31), 1500, 12000) * env_ad(0.5, 0.002, 0.12))[::-1] * 0.8
    low = (band(noise(0.5, 32), 80, 600) * env_ad(0.5, 0.002, 0.15))[::-1] * 0.5
    thud = sine(glide(90, 40, 0.5, 0.4), 0.5) * env_ad(0.5, 0.003, 0.12) * 1.0
    return mix(d, (0, rev, 1), (0, low, 1), (0.5, thud, 1), (0.5, click(0.015, 800, 5000, 33), 0.6), (0.52, air(0.7, 4000, 12000, 0.18, 34), 0.25))

# 4: the card back shatters: a sharp crack, shards scattering, a low thump. Hit 0.0 (+ 10 ms).
@snd
def shatter():
    d = 1.4
    crack = add(band(noise(0.08, 41), 2000, 14000) * env_ad(0.08, 0.0003, 0.015) * 1.5, click(0.01, 1000, 10000, 42) * 1.5)
    shards = crackle(1.0, 90, 3500, 14000, -0.9, 43) * 0.6
    tink = crackle(0.8, 25, 7000, 16000, -0.6, 44) * 0.5
    thump = sine(glide(110, 40, 0.5, 0.4), 0.5) * env_ad(0.5, 0.001, 0.1)
    return mix(d, (0.01, crack, 1), (0.01, thump, 0.9), (0.03, shards, 1), (0.1, tink, 1), (0.02, air(0.8, 5000, 15000, 0.15, 45), 0.3))

# 5: a scanner sweep (filtered noise climbing with fine grain), then a dry lock-in click + thump. Hit 0.6.
@snd
def scan_sweep():
    d = 1.2
    n = noise(0.6, 51) * (0.6 + 0.4 * np.sign(np.sin(2 * np.pi * 38 * T(0.6))))   # grainy
    sweep = svf(n, glide(500, 7000, 0.6, 1.2), 5) * env_swell(0.6, 0.55, 0.05) * 0.5
    lock = add(click(0.012, 1500, 9000, 52) * 1.4, sine(glide(140, 60, 0.25, 0.4), 0.25) * env_ad(0.25, 0.001, 0.05) * 0.8)
    return mix(d, (0, sweep, 1), (0.6, lock, 1), (0.61, air(0.4, 6000, 15000, 0.08, 53), 0.25))

# Hovering a card that still has its NEW tag: a tiny dry tick (seen).
@snd
def seen():
    return add(click(0.008, 2500, 9000, 61), band(noise(0.04, 62), 3000, 9000) * env_ad(0.04, 0.001, 0.008) * 0.3)


if __name__ == "__main__":
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    for name, fn in S.items():
        f.rng = np.random.default_rng(sum(map(ord, name)))
        write(os.path.join(out, f"U_{name}.wav"), fn(), name)
        print(name)
