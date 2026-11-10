"""CHAOS RIFT logo samples (2026-11-08): the Yu-Gi-Oh! logo over "CHAOS RIFT" in the logo's own recipe (chiselled
silver, gold inline, heavy black outline), torn by a rift: the halves pushed apart, a white-hot energy core in the gap,
chipped dark faces along the cut, metal shards breaking off, sparks and small bolts.
Usage: python game/tools/riftforge.py <yugioh_logo_png> <out_dir> [R1 R2 R3 R4]
Builds on logoforge.py (imported, none of its own samples run)."""
import os, sys
import numpy as np, cv2

LOGO, OUT = sys.argv[1], sys.argv[2]
PICK = sys.argv[3:] or ["R0", "R1", "R2", "R3", "R4"]
sys.argv = [sys.argv[0], LOGO, OUT, "-"]                 # logoforge: no sample letters -> generates nothing
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import logoforge as lf

GREY = (0.3, 0.3, 0.34)
_shadow = lf.shadow
def _solid_shadow(layer, blur, alpha=0.6):
    """Drop shadow from the solid metal only, so the soft energy glow doesn't cast a grey box."""
    solid = layer.copy(); solid[..., 3] = np.clip((layer[..., 3] - 0.6) * 2.5, 0, 1)
    return _shadow(solid, blur, alpha)
lf.shadow = _solid_shadow
VIOLET, GOLDGLOW, WHITE = (0.62, 0.38, 1.0), (1.0, 0.78, 0.4), (1.0, 1.0, 1.0)


def midpoint(n, rough, rng):
    """Jagged 1D profile in [-1, 1] by midpoint displacement (2^n + 1 points)."""
    pts = np.zeros(2 ** n + 1, np.float32)
    step, amp = len(pts) - 1, 1.0
    while step > 1:
        half = step // 2
        for i in range(half, len(pts), step):
            pts[i] = (pts[i - half] + pts[i + half]) / 2 + rng.uniform(-amp, amp)
        step, amp = half, amp * rough
    return pts / max(np.abs(pts).max(), 1e-6)


def rift(layer, seed, energy=VIOLET, slant=0.0, gap=0.13, chips=True, shards=10, through=0.52):
    """Tear an RGBA lettering layer along a jagged line. slant: crack tilt (dy per dx); through: crack height (0..1).
    Returns a bigger RGBA layer with the energy, shards and sparks composited."""
    rng = np.random.default_rng(seed)
    h0, w0 = layer.shape[:2]
    P = int(h0 * 0.45)
    L = lf.pad(layer, P)
    h, w = L.shape[:2]
    prof = midpoint(7, 0.55, rng)
    xs = np.linspace(0, w - 1, len(prof))
    cy = P + h0 * through + (xs - w / 2) * slant + prof * h0 * 0.17
    crack_y = np.interp(np.arange(w), xs, cy).astype(np.float32)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    upper = (yy < crack_y[None, :]).astype(np.float32)
    g = max(int(h0 * gap), 3)
    def shift(img, dx, dy):
        M = np.float32([[1, 0, dx], [0, 1, dy]])
        return cv2.warpAffine(img, M, (w, h), flags=cv2.INTER_LINEAR, borderValue=0)
    up = L * upper[..., None]; lo = L * (1 - upper[..., None])
    up = shift(up, -g * 0.35, -g * 0.55); lo = shift(lo, g * 0.35, g * 0.45)
    if chips:
        # dark cut faces: shade each piece toward the torn edge
        for piece, sgn, dy in ((up, -1, -g * 0.55), (lo, 1, g * 0.45)):
            d = np.abs(yy - (crack_y[None, :] + dy))
            k = np.exp(-d / (g * 0.5))[..., None] * 0.75
            piece[..., :3] *= 1 - k
    out = np.zeros_like(L)
    # energy in the gap: wide glow, violet body, white core; strongest where the letters were
    core = np.zeros((h, w), np.float32)
    pts = np.stack([np.arange(w), crack_y], 1).astype(np.int32)
    cv2.polylines(core, [pts], False, 1.0, max(int(g * 0.35), 2), cv2.LINE_AA)
    letters = cv2.GaussianBlur(L[..., 3], (0, 0), g * 2.5)
    span = np.clip(letters * 2.2, 0, 1)
    body = np.clip(cv2.GaussianBlur(core, (0, 0), g * 0.6) * 3.2, 0, 1) * span
    halo = np.clip(cv2.GaussianBlur(core, (0, 0), g * 2.2) * 3.0, 0, 1) * np.clip(cv2.GaussianBlur(L[..., 3], (0, 0), g * 5) * 2.5, 0, 1)
    for a, c in ((halo * 0.75, energy), (body, tuple(0.5 + 0.5 * np.array(energy))), (core * span, WHITE)):
        lay = np.dstack([np.zeros((h, w, 3), np.float32) + c, a])
        lf.over(out, lay, 0, 0)
    lf.over(out, lo, 0, 0)
    lf.over(out, up, 0, 0)
    # shards: chiselled splinters flying out of the cut
    for _ in range(shards):
        x = int(rng.uniform(0.08, 0.92) * w)
        if letters[int(crack_y[x]), x] < 0.15: continue
        s = int(rng.uniform(0.05, 0.13) * h0)
        m = np.zeros((s * 2 + 4, s * 2 + 4), np.float32)
        c0 = s + 2
        ang = rng.uniform(0, np.pi)
        tri = np.array([(c0 + np.cos(ang + k * 2.2) * s * rng.uniform(0.4, 1), c0 + np.sin(ang + k * 2.2) * s * rng.uniform(0.25, 0.7)) for k in range(3)], np.int32)
        cv2.fillPoly(m, [tri], 1.0, cv2.LINE_AA)
        ro = max(int(s * 0.12), 2); m = np.pad(m, ro + 2)
        piece = np.dstack([lf.chisel(m, max(s * 0.35, 2), "silver"), lf.dilate(m, ro)])
        side = rng.choice([-1, 1])
        y = int(crack_y[x] + side * rng.uniform(0.25, 0.75) * h0 * 0.5) - piece.shape[0] // 2
        lf.over(out, piece, x - piece.shape[1] // 2 + int(rng.uniform(-1, 1) * g), y)
    # sparks along the rift
    sp = np.zeros((h, w), np.float32)
    for _ in range(60):
        x = int(rng.uniform(0.05, 0.95) * w); y = int(crack_y[x] + rng.normal(0, h0 * 0.12))
        cv2.circle(sp, (x, y), int(rng.uniform(1, 3.5)), float(rng.uniform(0.5, 1)), -1, cv2.LINE_AA)
    sp = np.clip(sp + cv2.GaussianBlur(sp, (0, 0), 4) * 1.5, 0, 1) * np.clip(letters * 3, 0, 1)
    lf.over(out, np.dstack([np.zeros((h, w, 3), np.float32) + tuple(0.6 + 0.4 * np.array(energy)), sp]), 0, 0)
    # two small bolts leaking out of each end of the tear
    bolts = np.zeros((h, w), np.float32)
    for end in (0.06, 0.94):
        x0 = int(end * w); y0 = crack_y[x0]
        for _ in range(2):
            x, y, a = float(x0), float(y0), (np.pi if end < 0.5 else 0) + rng.uniform(-0.7, 0.7)
            pts = [(x, y)]
            for _ in range(7):
                a += rng.uniform(-0.7, 0.7); x += np.cos(a) * h0 * 0.07; y += np.sin(a) * h0 * 0.07
                pts.append((x, y))
            cv2.polylines(bolts, [np.array(pts, np.int32)], False, 1.0, 3, cv2.LINE_AA)
    bolts = np.clip(bolts + cv2.GaussianBlur(bolts, (0, 0), 7) * 1.6, 0, 1) * 0.8
    lf.over(out, np.dstack([np.zeros((h, w, 3), np.float32) + energy, bolts]), 0, 0)
    # faint haze far from the tear reads as a grey box on light backgrounds: keep soft alpha only near the crack/bolts
    near = np.clip(cv2.GaussianBlur(np.maximum(core, (bolts > 0.3).astype(np.float32)), (0, 0), g * 1.6) * 5, 0, 1)
    out[..., 3] = np.where(out[..., 3] < 0.35, out[..., 3] * near, out[..., 3])
    ys, xs2 = np.nonzero(out[..., 3] > 0.01)
    return out[ys.min():ys.max() + 1, xs2.min():xs2.max() + 1]


def diagonal_split(left, right, seed, gapx=0.5, energy=VIOLET):
    """CHAOS | RIFT with a slanted tear between the words, matching the screen's crack."""
    space = int(left.shape[0] * 0.35)
    word = lf.hcat(left, right, -space)
    pw = int(word.shape[0] * 0.4)
    gapx = (pw + left.shape[1] + space / 2) / (word.shape[1] + 2 * pw)
    word = lf.pad(word, pw)
    h, w = word.shape[:2]
    rng = np.random.default_rng(seed)
    prof = midpoint(6, 0.55, rng)
    ys = np.linspace(0, h - 1, len(prof))
    cx = w * gapx - (ys - h / 2) * 0.22 + prof * h * 0.06
    crack_x = np.interp(np.arange(h), ys, cx).astype(np.float32)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    leftm = (xx < crack_x[:, None]).astype(np.float32)
    g = int(h * 0.04)
    M = lambda dx, dy: np.float32([[1, 0, dx], [0, 1, dy]])
    lp = cv2.warpAffine(word * leftm[..., None], M(-g, g * 0.3), (w, h)); rp = cv2.warpAffine(word * (1 - leftm[..., None]), M(g, -g * 0.3), (w, h))
    out = np.zeros_like(word)
    core = np.zeros((h, w), np.float32)
    cv2.polylines(core, [np.stack([crack_x, np.arange(h)], 1).astype(np.int32)], False, 1.0, max(g, 2), cv2.LINE_AA)
    fade = np.clip(np.sin(np.linspace(0, np.pi, h)) * 1.6, 0, 1)[:, None]
    for a, c in ((np.clip(cv2.GaussianBlur(core, (0, 0), g * 5) * 2.5, 0, 1) * fade, energy),
                 (np.clip(cv2.GaussianBlur(core, (0, 0), g * 1.2) * 2.5, 0, 1) * fade, (0.85, 0.8, 1.0)), (core * fade, WHITE)):
        lf.over(out, np.dstack([np.zeros((h, w, 3), np.float32) + c, a]), 0, 0)
    lf.over(out, lp, 0, 0); lf.over(out, rp, 0, 0)
    ys2, xs2 = np.nonzero(out[..., 3] > 0.01)
    return out[ys2.min():ys2.max() + 1, xs2.min():xs2.max() + 1]


S = lf.S
logo = lf.logo_variant(kanji=GREY)
if "R0" in PICK:   # plain: the name in the logo's own gothic chrome, no effects (user pick 2026-11-08)
    lf.compose("R0-gothic-plain", lf.lettering("Chaos Rift", lf.G, S, metal="silver", inline="gold", **lf.HYB), 0.8, 0.1, logo=logo)
    lf.compose("R0b-castellar-plain", lf.lettering("CHAOS RIFT", "CASTELAR.TTF", S, metal="silver", inline="gold", track=0.06), 0.92, 0.1, logo=logo)
    # the name alone, for the loading intro (appears under the big logo, then the two morph together)
    name = lf.lettering("Chaos Rift", lf.G, S, metal="silver", inline="gold", **lf.HYB)
    lf.Image.fromarray((np.clip(name, 0, 1) * 255).astype(np.uint8), "RGBA").save(os.path.join(OUT, "R0-name.png"))
    lf.Image.fromarray((np.clip(logo, 0, 1) * 255).astype(np.uint8), "RGBA").save(os.path.join(OUT, "R0-logo.png"))
if "R1" in PICK:   # Castellar capitals, the logo's metal, violet rift
    lf.compose("R1-castellar-violet", rift(lf.lettering("CHAOS RIFT", "CASTELAR.TTF", S, metal="silver", inline="gold", track=0.06), 11), 0.98, 0.1, logo=logo)
if "R2" in PICK:   # gothic hybrid like the Yu-Gi-Oh! letters, gold-white rift
    lf.compose("R2-gothic-gold", rift(lf.lettering("Chaos Rift", lf.G, S, metal="silver", inline="gold", **lf.HYB), 5, energy=GOLDGLOW, through=0.58), 0.9, 0.1, logo=logo)
if "R3" in PICK:   # CHAOS silver | RIFT dark chaos metal, slanted tear between them
    a = lf.lettering("CHAOS", "CASTELAR.TTF", S, metal="silver", inline="gold", track=0.05)
    b = lf.lettering("RIFT", "CASTELAR.TTF", S, metal="chaos", inline="gold", track=0.05)
    lf.compose("R3-split-words", diagonal_split(a, b, 3), 0.98, 0.1, logo=logo)
if "R4" in PICK:   # Perpetua Titling bold, slanted violet tear through all of it
    lf.compose("R4-perpetua-slant", rift(lf.lettering("CHAOS RIFT", "PERTIBD.TTF", S, metal="silver", inline="gold", track=0.08), 21, slant=-0.06), 0.98, 0.1, logo=logo)
print("done ->", OUT)
