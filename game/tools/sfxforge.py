"""Renders the duel's own sound effects (mono 16-bit WAV, 44.1 kHz): the few kept from the VFX Lab, all non-tonal
textures; the rest of the duel's sounds are cut from Master Duel files by sfxcut.py. The files in assets/audio/gen
are the approved renders; re-running gives the same sounds with different random phases.

    python game/tools/sfxforge.py <out_dir>        (normally game/Godot/assets/audio/gen)

Every sound is built from a few pieces (filtered noise, sine partials, envelopes, a small room) and timed to the
animation it plays over: the comments give the moments it follows (seconds from the sound's cue).
"""
import math, os, sys, wave
import numpy as np

SR = 44100
rng = np.random.default_rng(7)


# ---------------------------------------------------------------- pieces

def T(d):
    return np.arange(int(d * SR)) / SR

def silence(d):
    return np.zeros(int(d * SR))

def noise(d, seed=None):
    r = np.random.default_rng(seed) if seed is not None else rng
    return r.uniform(-1, 1, int(d * SR))

def env_ad(d, a, decay, curve=1.0):
    """Attack a seconds, then exponential decay with time constant `decay`."""
    t = T(d)
    up = np.clip(t / max(a, 1e-4), 0, 1) ** curve
    return up * np.exp(-np.maximum(t - a, 0) / decay)

def env_swell(d, peak, fall):
    """Rises (eased) to `peak` seconds, then falls over `fall` seconds."""
    t = T(d)
    up = np.clip(t / peak, 0, 1)
    up = up * up * (3 - 2 * up)
    down = np.clip(1 - (t - peak) / fall, 0, 1) ** 1.6
    return np.where(t < peak, up, down)

def fade(x, fin=0.003, fout=0.02):
    n = len(x); a = min(int(fin * SR), n); b = min(int(fout * SR), n)
    x = x.copy()
    if a: x[:a] *= np.linspace(0, 1, a)
    if b: x[n - b:] *= np.linspace(1, 0, b)
    return x

def fit(a, n):
    """A number or array stretched to n samples (an array is held at its last value)."""
    a = np.asarray(a, dtype=float)
    if a.ndim == 0: return np.full(n, float(a))
    return a[:n] if len(a) >= n else np.concatenate([a, np.full(n - len(a), a[-1])])

def add(*xs):
    """Sum of signals of any lengths."""
    out = np.zeros(max(len(x) for x in xs))
    for x in xs: out[:len(x)] += x
    return out

def sine(freq, d, phase=0.0):
    """freq: number or array (a glide)."""
    f = fit(freq, int(d * SR))
    return np.sin(2 * np.pi * np.cumsum(f) / SR + phase)

def glide(f0, f1, d, curve=1.0):
    k = np.linspace(0, 1, int(d * SR)) ** curve
    return f0 * (f1 / f0) ** k

def svf(x, fc, q=0.7, mode="bp"):
    """Chamberlin state-variable filter; fc can be an array (a sweep)."""
    fc = fit(fc, len(x))
    f = 2 * np.sin(np.pi * np.clip(fc, 20, SR / 6) / SR)
    dmp = 1 / q
    lp = bp = 0.0
    out = np.empty_like(x)
    xs = x.tolist(); fs = f.tolist()
    for i in range(len(xs)):
        hp = xs[i] - lp - dmp * bp
        bp += fs[i] * hp
        lp += fs[i] * bp
        out[i] = bp if mode == "bp" else lp if mode == "lp" else hp
    return out

def band(x, lo, hi):
    """Static band-pass by FFT mask (soft edges)."""
    X = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / SR)
    m = 1 / (1 + (lo / np.maximum(f, 1)) ** 4) / (1 + (f / hi) ** 4)
    return np.fft.irfft(X * m, len(x))

def partials(f0, ratios, decays, amps, d, detune=0.0, a=0.002):
    out = silence(d)
    for r, dc, am in zip(ratios, decays, amps):
        fr = f0 * r * (1 + detune * rng.uniform(-1, 1))
        out += am * sine(fr, d, rng.uniform(0, 6.28)) * env_ad(d, a, dc)
    return out

def thump(f0, f1, d, decay, a=0.002):
    """A low body hit: a sine falling in pitch."""
    return sine(glide(f0, f1, d, 0.5), d) * env_ad(d, a, decay)

def click(d=0.012, lo=1500, hi=9000, seed=None):
    return band(noise(d, seed), lo, hi) * env_ad(d, 0.0005, d / 4)

def room(x, size=0.6, wet=0.18, bright=5000):
    """A small hall: convolution with a decaying noise tail."""
    n = int(size * SR)
    ir = band(noise(size, 3), 200, bright) * np.exp(-T(size) / (size / 5))
    ir /= np.sqrt(np.sum(ir ** 2)) + 1e-9
    L = len(x) + n
    y = np.fft.irfft(np.fft.rfft(x, L) * np.fft.rfft(ir, L), L)
    return np.concatenate([x, silence(size)]) * (1 - wet) + y * wet * 1.4

def mix(d, *parts):
    """parts: (start seconds, signal, gain)."""
    out = silence(d)
    for t0, s, g in parts:
        i = int(t0 * SR)
        if i >= len(out): continue
        n = min(len(s), len(out) - i)
        out[i:i + n] += s[:n] * g
    return out

def saw(freq, d):
    f = fit(freq, int(d * SR))
    ph = np.cumsum(f) / SR
    return 2 * (ph - np.floor(ph + 0.5))


# ---------------------------------------------------------------- recipes

def whoosh(d, f0, f1, peak, q=1.2, seed=None, air=0.0):
    """Air moving: band-passed noise sweeping f0 -> f1, loudest at `peak`."""
    n = noise(d, seed)
    s = svf(n, glide(f0, f1, d, 0.8), q)
    if air: s += air * band(noise(d, seed), 4000, 12000) * env_swell(d, peak, d - peak)
    return s * env_swell(d, peak, d - peak)

def crackle(d, rate, lo=1800, hi=9000, grow=0.0, seed=None):
    """Sparse random clicks (crack, fire)."""
    r = np.random.default_rng(seed)
    out = silence(d)
    t = 0.0
    while t < d:
        k = t / d
        c = click(r.uniform(0.004, 0.014), lo, hi, int(r.integers(1e6))) * r.uniform(0.3, 1) * (1 + grow * k)
        out = mix(d, (0, out, 1), (t, c, 1))
        t += r.exponential(1 / (rate * (1 + grow * k)))
    return out

def metal(f0, d, ring=0.5, bright=1.0, seed=None):
    """Struck metal (inharmonic partials)."""
    ratios = [1, 2.76, 5.40, 8.93, 13.34]
    return partials(f0, ratios, [ring, ring * 0.6, ring * 0.35, ring * 0.2, ring * 0.12], [1, 0.6 * bright, 0.4 * bright, 0.25 * bright, 0.15 * bright], d, detune=0.004)

def pad(freqs, d, peak, fall, vib=4.5, depth=0.004):
    t = T(d)
    out = silence(d)
    for f in freqs:
        for det in (-0.006, 0, 0.006):
            out += sine(f * (1 + det) * (1 + depth * np.sin(2 * np.pi * vib * t + rng.uniform(0, 6))), d)
    return out / (3 * len(freqs)) * env_swell(d, peak, fall)

def ticks(times, f0, f1, d, tick_d=0.03):
    """Counter ticks at given times, pitch moving f0 -> f1."""
    out = silence(d)
    for i, t in enumerate(times):
        k = i / max(len(times) - 1, 1)
        f = f0 * (f1 / f0) ** k
        tk = add(partials(f, [1, 2.0], [0.012, 0.006], [1, 0.3], tick_d), 0.25 * click(0.006, 3000, 9000, i))
        out = mix(d, (0, out, 1), (t, tk, 1))
    return out


# ---------------------------------------------------------------- the sounds (name -> function)

S = {}
def snd(f):
    S[f.__name__] = f
    return f

# Attack lunge: wind back 0.24 s, pause 0.08, dash 0.14 -> contact at 0.46.
@snd
def whoosh_repel():     # thrown back (0.3 s)
    return whoosh(0.4, 2200, 500, 0.08, 1.1, 6) * 0.8
# Effect destruction: glare, the card burns from the top down in 0.55 s, embers lift, smoke at 0.35.
@snd
def ember_tail():       # a soft sizzle fading (after a break)
    d = 1.0
    return add(band(noise(d, 81), 3000, 10000) * env_swell(d, 0.12, 0.85) * 0.35, crackle(0.9, 20, 3000, 9000, -0.8, 82) * 0.25)
# Landing / summons.
# Swords (swing 0.23 s, cross at 0.23) and shield (pops in 0.12).
# Attack blocked: contact at 0.46 (hit-stop 0.16 s), thrown back.
# Grind (locked blades): contact, 0.34 s of scraping sparks, a release flash, thrown back.
@snd
def grind_scrape():     # metal grinding against metal for 0.34 s
    d = 0.42
    n = noise(d, 161)
    sc = svf(n, 2600 + 900 * np.sin(2 * np.pi * 23 * T(d)), 6) * env_swell(d, 0.03, 0.39)
    tones = partials(1900, [1, 1.51, 2.3], [1, 1, 1], [0.5, 0.3, 0.2], d) * env_swell(d, 0.03, 0.39) * 0.25
    return mix(d, (0, sc, 0.9), (0, tones, 1), (0, crackle(0.38, 140, 3000, 12000, 0, 162), 0.35))
# Both destroyed: the two meet in the middle.
# Direct attack: the shockwave on their side.
# Aim line: draws in 0.25 s and stays.
# LP: the number counts for 0.7 s after 0.1 s.
# Duel won: fanfares.
# Special summon: the card rebuilds out of embers (0.75 s), lands.
@snd
def rebuild_violet():   # crackle running backwards into a rising violet tone
    d = 1.0
    cr = crackle(0.75, 70, 1800, 8000, -0.8, 231)[::-1] * 0.5
    rise = pad([392, 466.2, 587.3], 0.95, 0.72, 0.23, depth=0.006) * 0.7
    return room(mix(d, (0, cr, 1), (0, rise, 1), (0, svf(noise(0.8, 232), glide(300, 3000, 0.8), 1.2) * env_swell(0.8, 0.7, 0.1) * 0.3, 1)), 0.7, 0.2)
# Souls: the tribute dissolves (0.55 s), its soul arcs over (0.9 s); fusion swirl 1.2 s.
# Ritual: the circle (spins 1.8 s), pillars at 0.15, the drop at 0.7, the impact at 1.15.
@snd
def ritual_drone():     # a low mystic drone swelling into a minor chord
    d = 2.2
    dr = (saw(55, d) * 0.4 + saw(55.4, d) * 0.4)
    dr = svf(dr, glide(200, 900, d, 0.5), 1.0, "lp") * env_swell(d, 1.1, 1.1) * 0.5
    return room(mix(d, (0, dr, 1), (0.2, pad([220, 261.6, 329.6], 1.9, 0.9, 1.0), 0.6)), 1.2, 0.3)
@snd
def pillar_zing():      # four light pillars: glassy zings
    d = 1.0
    out = silence(d)
    for i, f in enumerate([1760, 2217, 2637, 3520]):
        z = svf(noise(0.5, 280 + i), glide(f * 0.6, f * 1.4, 0.5), 10) * env_ad(0.5, 0.01, 0.15)
        out = mix(d, (0, out, 1), (i * 0.02, z, 0.25), (i * 0.02, partials(f, [1, 2], [0.25, 0.1], [1, 0.2], 0.6), 0.15))
    return room(out, 0.6, 0.25)

# Flip summon: pops up 0.8 and turns over (0.5 s).
@snd
def card_flip_snap():   # a crisp card snap as it turns over
    d = 0.35
    return mix(d, (0, click(0.02, 1500, 9000, 294), 0.9), (0.015, click(0.012, 3000, 11000, 295), 0.5), (0, band(noise(0.08, 296), 1000, 5000) * env_ad(0.08, 0.001, 0.02), 0.4))

# Cards moving: set, draw, to the Graveyard, back to the hand.
# ATK/DEF change (glow 0.2, count 0.6) and equip (thread 0.3, flare at 0.4): quiet.
@snd
def stat_up_soft():     # two soft rising glints and quiet count ticks
    d = 0.8
    return mix(d, (0, partials(1046.5, [1, 2], [0.12, 0.05], [1, 0.2], 0.3), 0.18), (0.08, partials(1568, [1, 2], [0.2, 0.06], [1, 0.2], 0.4), 0.18), (0, ticks(list(np.arange(0.02, 0.6, 0.05)), 2000, 2600, 0.7, 0.015), 0.15))
@snd
def stat_down_soft():   # the same falling
    d = 0.8
    return mix(d, (0, partials(1568, [1, 2], [0.12, 0.05], [1, 0.2], 0.3), 0.18), (0.08, partials(1046.5, [1, 2], [0.2, 0.06], [1, 0.2], 0.4), 0.18), (0, ticks(list(np.arange(0.02, 0.6, 0.05)), 2600, 2000, 0.7, 0.015), 0.15))
@snd
def thread_glide():     # a thin tone gliding along the thread
    d = 0.5
    return sine(glide(800, 1600, 0.3, 0.6), 0.5) * env_swell(0.5, 0.25, 0.25) * 0.15
@snd
def equip_clasp():      # a small metal clasp clicking shut
    d = 0.4
    return mix(d, (0, click(0.01, 2500, 10000, 351), 0.4), (0.01, metal(3100, 0.3, 0.06, 0.6), 0.15))
@snd
def glitch_static():      # radio static bursts on each flicker, then a dull drop
    d = 1.0
    r = np.random.default_rng(453)
    out = silence(d)
    for i in range(15):
        if r.uniform() < 0.7:
            b = band(noise(0.035, 454 + i), 1000, 8000) * r.uniform(0.3, 0.8)
            out = mix(d, (0, out, 1), (i * 0.04, fade(b, 0.002, 0.005), 0.5))
    return mix(d, (0, out, 1), (0.6, thump(140, 50, 0.3, 0.08), 0.5))
# Quieter by design (subtle stat / equip sounds; the card snap sits under the rest).
QUIET = {"stat": 0.45, "thread": 0.45, "equip": 0.5, "card": 0.7}


# ---------------------------------------------------------------- Chaos Rift (2026-11-09)

# Intro: the rift is drawn top to bottom in 0.22 s (quadratic ease-in: it speeds up), lands at 0.22 with a flash,
# a burst and a hard shake (0.35 s); then the light floods in.
@snd
def rift_strike():
    # Round 2 (user: "missing the impact factor"): the rip is quieter and stops dead 30 ms before the hit (the
    # silence makes the hit land); the hit is a dry, saturated stack (snap + sub kick + body), the room only on the tail.
    d = 3.6
    fall = 0.22
    rip = add(crackle(fall - 0.03, 260, 2500, 11000, 3.0, 901) * 0.5,
              svf(noise(fall - 0.03, 902), glide(900, 5200, fall - 0.03, 0.5), 2.5) * env_swell(fall - 0.03, fall - 0.05, 0.02) * 0.6) * 0.35
    snap = add(band(noise(0.06, 921), 2000, 14000) * env_ad(0.06, 0.0003, 0.008) * 1.6, click(0.015, 500, 8000, 922) * 2.0)
    kick = sine(glide(150, 28, 0.9, 0.35), 0.9) * env_ad(0.9, 0.001, 0.28) * 1.6          # the chest hit
    body = sine(glide(260, 90, 0.25, 0.5), 0.25) * env_ad(0.25, 0.001, 0.05) * 0.9         # the punch
    grit = band(noise(0.35, 923), 120, 1800) * env_ad(0.35, 0.001, 0.07) * 0.9             # stone breaking
    hit = np.tanh(mix(1.0, (0, snap, 1), (0, kick, 1), (0, body, 1), (0, grit, 1)) * 2.2) * 0.9   # saturated: fat, loud
    rumble = band(noise(3.0, 905), 25, 180) * env_swell(3.0, 0.12, 2.85) * 0.5
    boom_tail = room(sine(glide(70, 35, 2.0, 0.5), 2.0) * env_ad(2.0, 0.01, 0.6) * 0.5, 1.8, 0.5, 2500)
    shimmer = band(noise(1.6, 906), 5000, 12000) * env_ad(1.6, 0.01, 0.4) * 0.08
    return fade(mix(d, (0, rip, 1), (fall, hit, 1), (fall + 0.01, rumble, 1), (fall + 0.02, boom_tail, 1), (fall + 0.06, shimmer, 1)), 0.002, 0.5)

# Screen change (tear): the old screen splits along a rift for 0.3 s and its halves slide apart in 0.38 s.
# It plays on every screen change, so it stays short and soft: a quick rip with a breath of air, no boom.
@snd
def rift_tear():
    d = 0.75
    rip = add(crackle(0.26, 170, 2000, 9000, 1.5, 911) * 0.45,
              svf(noise(0.3, 912), glide(1400, 4200, 0.3, 0.6), 1.8) * env_swell(0.3, 0.24, 0.06) * 0.6)
    part = whoosh(0.5, 1800, 380, 0.12, 1.0, 913, air=0.3) * 0.55   # the halves sliding apart
    low = thump(110, 55, 0.3, 0.07) * 0.35
    out = mix(d, (0, rip, 1), (0.26, part, 1), (0.28, low, 1))
    return room(fade(out, 0.002, 0.15), 0.5, 0.12, 6000)

def write(path, x, name=""):
    """Levels every sound by its loudest 50 ms (so a short click and a long swell sit together), soft-limits peaks."""
    x = np.asarray(x, dtype=float)
    w = int(0.05 * SR)
    loud = max(np.sqrt(np.mean(x[i:i + w] ** 2)) for i in range(0, max(len(x) - w, 1), w // 2)) + 1e-9
    x = x * (0.25 / loud) * next((g for k, g in QUIET.items() if name.startswith(k)), 1.0)
    x = np.tanh(x * 1.1) * 0.9
    x = fade(x, 0.001, 0.01)
    with wave.open(path, "wb") as w_:
        w_.setnchannels(1); w_.setsampwidth(2); w_.setframerate(SR)
        w_.writeframes((x * 32767).astype("<i2").tobytes())


if __name__ == "__main__":
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    only = set(sys.argv[2:])
    for name, f in S.items():
        if only and name not in only: continue
        rng = np.random.default_rng(sum(map(ord, name)))   # each sound seeded by its name: renders don't depend on the others
        write(os.path.join(out, f"G_{name}.wav"), f(), name)
        print(name)
