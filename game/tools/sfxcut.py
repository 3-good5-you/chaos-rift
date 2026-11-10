"""Cuts duel sounds out of Master Duel's own sound files (trim, fade, pitch, re-time) for the VFX Lab's sound round.

    python game/tools/sfxcut.py <md_sound_dir> <out_dir>

Writes C_<name>.wav (mono 16-bit 44.1 kHz), the ones the duel uses (DuelField.Sounds.cs). Real game material only: the long story-mode effects are trimmed to the
moment we need, and short ticks are placed on an animation's own rhythm (each turn of the coin, each shake of the cup).
"""
import glob, math, os, sys, wave
import numpy as np

SR = 44100
SRC = {}


def load(name):
    with wave.open(SRC[name]) as w:
        sr, ch, sw, n = w.getframerate(), w.getnchannels(), w.getsampwidth(), w.getnframes()
        x = np.frombuffer(w.readframes(n), dtype="<i2" if sw == 2 else "<i4").astype(float)
    if ch > 1: x = x.reshape(-1, ch).mean(1)
    x /= 32768.0 if sw == 2 else 2147483648.0
    if sr != SR: x = np.interp(np.arange(0, len(x), sr / SR), np.arange(len(x)), x)
    return x


def pitch(x, k):
    """Resample: k > 1 higher and shorter."""
    return np.interp(np.arange(0, len(x) - 1, k), np.arange(len(x)), x)


def cut(name, start=0.0, end=None, fade_out=0.15, gain=1.0, k=1.0):
    x = load(name)
    x = x[int(start * SR): int(end * SR) if end else None]
    if k != 1: x = pitch(x, k)
    n = min(int(fade_out * SR), len(x))
    if n: x[-n:] *= np.linspace(1, 0, n) ** 1.5
    m = min(int(0.003 * SR), len(x))
    if start > 0 and m: x[:m] *= np.linspace(0, 1, m)
    return x * gain


def place(d, hits):
    """hits: (time, signal, gain)."""
    out = np.zeros(int(d * SR))
    for t, s, g in hits:
        i = int(t * SR)
        if i >= len(out): continue
        n = min(len(s), len(out) - i)
        out[i:i + n] += s[:n] * g
    return out


def coin_turns():
    # The coin turns 5 times over 1.3 s with a smooth start and stop: its face meets the camera at theta = n*pi.
    k = np.linspace(0, 1, 4000)
    th = 2 * np.pi * 5 * (k * k * (3 - 2 * k))
    return [float(k[np.argmax(th >= n * np.pi)] * 1.3) for n in range(1, 10)]


def rattle_turns():
    # The cup turns back at each peak of sin(47 k) over its 0.9 s shake.
    return [0.9 * (math.pi / 2 + n * math.pi) / 47 for n in range(15)]


def recipes():
    tick = load("SE_CARD_COUNTER")[: int(0.06 * SR)]
    move = load("SE_DR_MOVE")
    yield "coin_toss_ticks", place(1.5, [(0, cut("SE_COIN_THROW", 0, 0.3, 0.12), 1.0)] + [(t, pitch(tick, 1.6), 0.45) for t in coin_turns()])
    r = np.random.default_rng(3)
    hits = []
    for t in rattle_turns():
        g = 0.45 + 0.55 * t / 0.9
        hits += [(t, pitch(tick, r.uniform(0.62, 0.75)), g), (t + r.uniform(0.012, 0.025), move, 0.35 * g)]
    yield "dice_rattle_roll", place(1.0, hits + [(0, cut("SE_DICE_ROLL", 0, 0.95, 0.2), 0.45)])
    # Story-mode effects trimmed to the moment.
    yield "smoke_blast", cut("SE_SOLO_GATE078_SMOKE_EXP", 0, 1.6, 0.7)
    yield "whoosh_gate", cut("SE_SOLO_GATE078_WHOOSH", 0, 0.7, 0.25)
    yield "change", cut("SE_SOLO_3S_CHANGE_2", 0, 1.6, 0.6)
    yield "absorb_soul_short", cut("SE_SOLO_S_ABSORPTION", 0, 1.25, 0.45)   # the pull ends as the souls arrive (1.15 s)
    # Field mat reactions (rumbles, cracks, gusts) trimmed.
    yield "mat_hit_light", cut("SE_FIELD_MAT001_PHASE1_P", 0, 1.0, 0.4)
    yield "mat_rumble", cut("SE_FIELD_MAT005_PHASE1_P", 0, 1.3, 0.5)


def write(path, x):
    x = np.asarray(x, dtype=float)
    pk = np.max(np.abs(x)) + 1e-9
    x = x * (0.89 / pk) if pk > 0.89 else x
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype("<i2").tobytes())


if __name__ == "__main__":
    src, out = sys.argv[1], sys.argv[2]
    for f in glob.glob(os.path.join(src, "**", "*.wav"), recursive=True):
        SRC.setdefault(os.path.basename(f)[:-4], f)
    os.makedirs(out, exist_ok=True)
    for name, x in recipes():
        if x is None: continue
        write(os.path.join(out, f"C_{name}.wav"), x)
        print(name, f"{len(x) / SR:.2f}s")
