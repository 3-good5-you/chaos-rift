"""Back Forge: our own card backs, procedurally, in the styles of the Master Duel protector families
(used only as a style reference). Each back carries the Yu-Gi-Oh! logo (repo root) recoloured to its style.
Usage: python backforge.py <art_dir> <logo_png> <out_dir> [names] [--mat]
--mat: plain landscape renders of the styles (no frame/logo). The real playmats are matforge.py."""
import sys, os, math
import numpy as np
import cv2

MAT = "--mat" in sys.argv
ARGS = [a for a in sys.argv[1:] if a != "--mat"]
ART, LOGO, OUT = ARGS[0], ARGS[1], ARGS[2]
os.makedirs(OUT, exist_ok=True)
W, H = (2048, 1374) if MAT else (813, 1185)
yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)

# ---------------------------------------------------------------- helpers

def rng(seed): return np.random.default_rng(seed)

def fbm(seed, base=4, octaves=6, h=H, w=W, aspect=(1, 1)):
    r = rng(seed); out = np.zeros((h, w), np.float32); amp, tot = 1.0, 0.0
    for o in range(octaves):
        gh, gw = max(2, int(base * aspect[0] * 2 ** o)), max(2, int(base * aspect[1] * 2 ** o))
        g = r.random((gh, gw)).astype(np.float32)
        out += cv2.resize(g, (w, h), interpolation=cv2.INTER_CUBIC) * amp
        tot += amp; amp *= 0.5
    out /= tot
    return (out - out.min()) / (out.max() - out.min() + 1e-6)

def ramp(t, stops):
    """t in 0..1 -> colour from [(pos, (r,g,b)), ...] (0..1 floats)."""
    t = np.clip(t, 0, 1)[..., None]
    pos = np.array([s[0] for s in stops], np.float32); cols = np.array([s[1] for s in stops], np.float32)
    out = np.zeros(t.shape[:-1] + (3,), np.float32)
    for i in range(len(stops) - 1):
        a, b = pos[i], pos[i + 1]
        k = np.clip((t - a) / max(b - a, 1e-6), 0, 1)
        sel = (t >= a) & (t <= b)
        out = np.where(sel, cols[i] * (1 - k) + cols[i + 1] * k, out)
    return out

def hexc(s): s = s.lstrip('#'); return tuple(int(s[i:i + 2], 16) / 255 for i in (0, 2, 4))

def glow(mask, sigma): return cv2.GaussianBlur(mask.astype(np.float32), (0, 0), sigma)

def add(img, mask, col, k=1.0): return img + mask[..., None] * np.array(col, np.float32) * k

def over(img, mask, col): m = np.clip(mask, 0, 1)[..., None]; return img * (1 - m) + np.array(col, np.float32) * m

def polar(cx=W / 2, cy=H / 2):
    dx, dy = xx - cx, yy - cy
    return np.hypot(dx, dy), np.arctan2(dy, dx)

def rrect_mask(x0, y0, x1, y1, r, h=H, w=W):
    m = np.zeros((h, w), np.uint8)
    cv2.rectangle(m, (x0 + r, y0), (x1 - r, y1), 255, -1); cv2.rectangle(m, (x0, y0 + r), (x1, y1 - r), 255, -1)
    for cx, cy in ((x0 + r, y0 + r), (x1 - r, y0 + r), (x0 + r, y1 - r), (x1 - r, y1 - r)): cv2.circle(m, (cx, cy), r, 255, -1, cv2.LINE_AA)
    return m.astype(np.float32) / 255

def stars(img, seed, n, col=(1, 1, 1), size=(1.0, 3.5), cross=True):
    r = rng(seed); m = np.zeros((H, W), np.float32)
    for _ in range(n):
        x, y, s = r.integers(30, W - 30), r.integers(30, H - 30), r.uniform(*size)
        cv2.circle(m, (int(x), int(y)), max(1, int(s)), 1.0, -1, cv2.LINE_AA)
        if cross and s > 2.5:
            L = int(s * 7)
            cv2.line(m, (int(x - L), int(y)), (int(x + L), int(y)), 0.6, 1, cv2.LINE_AA)
            cv2.line(m, (int(x), int(y - L)), (int(x), int(y + L)), 0.6, 1, cv2.LINE_AA)
    return add(img, glow(m, 1.2) * 1.6 + glow(m, 6) * 0.8, col)

# ---------------------------------------------------------------- metal, frame, emblem

METALS = {
    "gold": [(0, hexc('5a3a0e')), (0.35, hexc('c98f2a')), (0.5, hexc('fff0b0')), (0.62, hexc('d7a23c')), (1, hexc('6b4510'))],
    "silver": [(0, hexc('4a5058')), (0.35, hexc('aeb6c0')), (0.5, hexc('ffffff')), (0.62, hexc('b9c1cb')), (1, hexc('50565e'))],
    "bronze": [(0, hexc('3a1e0e')), (0.35, hexc('9a5a2a')), (0.5, hexc('ffd2a0')), (0.62, hexc('a8642e')), (1, hexc('40220e'))],
    "violet": [(0, hexc('2a0f40')), (0.35, hexc('8a4ad0')), (0.5, hexc('f2dcff')), (0.62, hexc('9a5ae0')), (1, hexc('2e1048'))],
    "crimson": [(0, hexc('3a0606')), (0.35, hexc('c0302a')), (0.5, hexc('ffd0b0')), (0.62, hexc('d04030')), (1, hexc('400808'))],
    "azure": [(0, hexc('061a3a')), (0.35, hexc('2a7ad0')), (0.5, hexc('d8f2ff')), (0.62, hexc('3a8ae0')), (1, hexc('08203e'))],
    "jade": [(0, hexc('06301a')), (0.35, hexc('2aa060')), (0.5, hexc('d8ffe8')), (0.62, hexc('3ab070')), (1, hexc('083a20'))],
}

def metal(name, t): return ramp(t, METALS[name])

def metal_fill(img, mask, name, tilt=0.0):
    """Brushed-metal fill: diagonal gradient + fine brush noise, bevel highlight from the mask's gradient."""
    t = ((yy / H) * 0.9 + (xx / W) * tilt) % 1.0
    t = 0.5 + 0.5 * np.sin(t * math.pi * 3.0 + 0.6)
    brush = fbm(7, base=2, octaves=4, aspect=(40, 1)) * 0.12
    col = metal(name, np.clip(t * 0.85 + brush, 0, 1))
    m = np.clip(mask, 0, 1)
    # bevel: light from the top-left
    gx = cv2.Sobel(glow(m, 2.0), cv2.CV_32F, 1, 0); gy = cv2.Sobel(glow(m, 2.0), cv2.CV_32F, 0, 1)
    shade = np.clip(-(gx + gy) * 0.35, -0.6, 0.6)
    col = np.clip(col + shade[..., None], 0, 1.4)
    return img * (1 - m[..., None]) + col * m[..., None]

def frame(img, metal_name, accent, inner_dark=0.0, ornate=True):
    if MAT: return img                       # the table draws its own edge
    outer = rrect_mask(0, 0, W - 1, H - 1, 40)
    inner = rrect_mask(30, 30, W - 31, H - 31, 26)
    ring = np.clip(outer - inner, 0, 1)
    if inner_dark: img = img * (1 - glow(1 - inner, 22)[..., None] * inner_dark)       # vignette under the frame
    img = add(img, glow(ring, 10) * 0.6, accent)                                        # frame glow into the art
    img = metal_fill(img, ring, metal_name)
    line = np.clip(rrect_mask(40, 40, W - 41, H - 41, 20) - rrect_mask(44, 44, W - 45, H - 45, 17), 0, 1)
    img = metal_fill(img, line, metal_name, tilt=0.3)
    if ornate:   # corner brackets
        m = np.zeros((H, W), np.float32)
        for sx, sy in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
            cx, cy = (60 if sx > 0 else W - 61), (60 if sy > 0 else H - 61)
            pts = np.array([[cx, cy + sy * 90], [cx, cy], [cx + sx * 90, cy]], np.int32)
            cv2.polylines(m, [pts], False, 1.0, 5, cv2.LINE_AA)
            cv2.circle(m, (cx + sx * 16, cy + sy * 16), 6, 1.0, -1, cv2.LINE_AA)
        img = add(img, glow(m, 6) * 0.7, accent)
        img = metal_fill(img, m, metal_name)
    return img

def _logo_layers():
    im = cv2.imread(LOGO, cv2.IMREAD_UNCHANGED)
    if im.ndim == 2 or im.shape[2] < 4:
        from PIL import Image as _I   # palette PNGs: let Pillow expand them to RGBA
        im = cv2.cvtColor(np.asarray(_I.open(LOGO).convert("RGBA")), cv2.COLOR_RGBA2BGRA)
    a = im[:, :, 3].astype(np.float32) / 255
    ys, xs = np.nonzero(a > 0.02)
    im, a = im[ys.min():ys.max() + 1, xs.min():xs.max() + 1], a[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    b, g, r = [im[:, :, i].astype(np.float32) / 255 for i in range(3)]
    h, w = a.shape
    kanji = ((r > 0.6) & (g < 0.45) & (b < 0.3)).astype(np.float32)
    # the (R) mark sits on a kanji stroke: paint it over as brush
    rm = np.zeros_like(a); cv2.circle(rm, (int(w * 0.8456), int(h * 0.7676)), int(w * 0.012), 1.0, -1)
    rm = rm > 0
    kanji[rm & (a > 0.5)] = 1; r[rm] = 0.88
    gold = ((~rm) & (r > 0.55) & (g > 0.35) & (b < 0.25) & (g < r * 0.85)).astype(np.float32)
    lum = 0.3 * r + 0.59 * g + 0.11 * b
    silver = ((~rm) & (np.abs(r - g) < 0.12) & (np.abs(g - b) < 0.12) & (lum > 0.25)).astype(np.float32)
    return a, kanji, gold, silver, lum, r

LOGO_L = None

def emblem(img, cx, cy, s, metal_name, accent, kanji_col=None, width=None):
    if MAT: return img                       # mats carry no logo
    """The Yu-Gi-Oh! logo recoloured: kanji in the style's colour, letters in the frame metal, edges bright metal.
    Width defaults to 3.5 * s."""
    global LOGO_L
    if LOGO_L is None: LOGO_L = _logo_layers()
    a, kanji, gold, silver, lum, red = LOGO_L
    lw = int(width or s * 3.5); lh = int(a.shape[0] * lw / a.shape[1])
    rs = lambda m: cv2.resize(m, (lw, lh), interpolation=cv2.INTER_AREA)
    a_, k_, g_, s_, l_, r_ = map(rs, (a, kanji, gold, silver, lum, red))
    x0, y0 = int(cx - lw / 2), int(cy - lh / 2)
    x0c, y0c = max(x0, 0), max(y0, 0); x1c, y1c = min(x0 + lw, W), min(y0 + lh, H)
    sl = (slice(y0c - y0, y1c - y0), slice(x0c - x0, x1c - x0))
    def place(m):
        out = np.zeros((H, W), np.float32); out[y0c:y1c, x0c:x1c] = m[sl]; return out
    A, K, G, S, L, R = map(place, (a_, k_, g_, s_, l_, r_))
    kc = np.array(kanji_col if kanji_col is not None else accent, np.float32)
    # shadow + glow under the whole logo
    img = over(img, glow(A, 6) * 0.65, (0, 0, 0))
    img = add(img, glow(K, 14) * 0.5, kc)
    # base: dark outline everywhere the logo is opaque
    base = np.zeros((H, W, 3), np.float32) + np.array((0.03, 0.02, 0.025), np.float32)
    # kanji: brush strokes in the style colour, keep the brush texture from the red channel
    kcol = kc[None, None, :] * (0.75 + 0.45 * (R - 0.6))[..., None]
    base = base * (1 - K[..., None]) + kcol * K[..., None]
    # letters: frame metal driven by the original silver shading
    met = metal(metal_name, np.clip(L * 1.05, 0, 1))
    base = base * (1 - S[..., None]) + met * S[..., None]
    # thin edge: bright metal
    edge = metal(metal_name, np.full((H, W), 0.5, np.float32))
    base = base * (1 - G[..., None]) + edge * G[..., None]
    return img * (1 - A[..., None]) + base * A[..., None]


def finish(name, img, grain=0.02, seed=0):
    img = img + (rng(seed + 99).random((H, W, 1)).astype(np.float32) - 0.5) * grain
    out = (np.clip(img, 0, 1) * 255).astype(np.uint8)[:, :, ::-1]
    cv2.imwrite(os.path.join(OUT, name + ".png"), out)

# ---------------------------------------------------------------- styles

def vortex():
    r, th = polar()
    sw = th * 3 + np.log(r + 8) * 5.5
    n = fbm(1, base=3)
    streak = 0.5 + 0.5 * np.sin(sw * 3 + n * 6)
    t = np.clip(streak * 0.55 + np.clip((r - 60) / 520, 0, 1) * 0.6 - 0.15, 0, 1)
    img = ramp(t, [(0, hexc('050302')), (0.35, hexc('2a1405')), (0.6, hexc('8a4210')), (0.82, hexc('e08a2a')), (1, hexc('ffd27a'))])
    img *= np.clip(r / 260, 0.0, 1)[..., None] ** 1.4                # dark eye of the vortex
    img = emblem(img, W / 2, H / 2, 150, "gold", hexc('ff9a30'), kanji_col=hexc('c8501a'), width=620)
    return frame(img, "bronze", hexc('ff8a20'), 0.4)

def grid():
    cell = 54; r_ = rng(3)
    cx_, cy_ = (xx // cell).astype(int), (yy // cell).astype(int)
    shades = r_.uniform(0.62, 0.95, (H // cell + 2, W // cell + 2)).astype(np.float32)
    base = shades[cy_, cx_]
    fx, fy = (xx % cell) / cell, (yy % cell) / cell
    edge = np.minimum(np.minimum(fx, 1 - fx), np.minimum(fy, 1 - fy))
    tile = base * (0.9 + 0.1 * np.clip(edge * 6, 0, 1))
    lines = np.exp(-(edge * cell) ** 2 / 3.0)
    img = np.stack([tile * 0.86, tile * 0.89, tile * 0.95], -1)
    img = add(img, glow(lines, 1.0) * 0.9 + glow(lines, 5) * 0.25, (1, 1, 1))
    r, _ = polar(); img *= (1.05 - 0.35 * np.clip(r / 800, 0, 1))[..., None]
    img = emblem(img, W / 2, H * 0.7, 150, "gold", hexc('ffffff'), kanji_col=hexc('5a6a80'), width=640)
    return frame(img, "gold", hexc('ffffff'), 0.2)

def burst(name, core, mid, edge, metal_name, seed):
    r, th = polar(cy=H / 2 - 30)
    ang = (th + math.pi) / (2 * math.pi)
    rays = cv2.resize(rng(seed).random((1, 180)).astype(np.float32), (2048, 1), interpolation=cv2.INTER_CUBIC)[0]
    ray = np.clip(rays, 0, 1)[np.clip(ang * 2047, 0, 2047).astype(int)] ** 2.2
    t = np.clip(1.1 - r / 620 + ray * 0.35 * np.clip(r / 300, 0, 1), 0, 1)
    img = ramp(t, [(0, edge), (0.45, mid), (0.8, core), (1, (1, 1, 1))])
    ring = np.exp(-((r - 330 - fbm(seed + 1, base=12) * 18) ** 2) / 30)
    img = add(img, ring * 0.9 + glow(ring, 8) * 0.6, (1, 1, 0.95))
    img = stars(img, seed + 2, 70, (1, 1, 0.9))
    img = emblem(img, W / 2, H / 2 - 30, 150, metal_name, (1, 0.9, 0.5), kanji_col=hexc('e07a10'), width=600)
    return frame(img, metal_name, core, 0.15)

def lightning(seed, col_bg, col_bolt, metal_name):
    n = fbm(seed, base=3)
    img = ramp(n * 0.8, [(0, hexc('05010a')), (0.5, col_bg), (1, tuple(c * 1.4 for c in col_bg))])
    r_ = rng(seed + 1); m = np.zeros((H, W), np.float32)
    def bolt(p0, p1, d, w):
        if d == 0: cv2.line(m, tuple(map(int, p0)), tuple(map(int, p1)), 1.0, w, cv2.LINE_AA); return
        mid = (p0 + p1) / 2 + r_.normal(0, np.linalg.norm(p1 - p0) * 0.18, 2)
        bolt(p0, mid, d - 1, w); bolt(mid, p1, d - 1, w)
        if r_.random() < 0.3 and d > 2: bolt(mid, mid + r_.normal(0, 120, 2), d - 2, max(1, w - 1))
    for _ in range(7):
        a = np.array([r_.uniform(60, W - 60), r_.uniform(40, 200)], np.float32)
        b = np.array([r_.uniform(60, W - 60), r_.uniform(H - 260, H - 60)], np.float32)
        bolt(a, b, 7, 2)
    img = add(img, m * 1.2 + glow(m, 4) * 1.2 + glow(m, 18) * 0.8, col_bolt)
    img = emblem(img, W / 2, H * 0.3, 150, metal_name, col_bolt, kanji_col=col_bolt, width=640)
    return frame(img, metal_name, col_bolt, 0.4)

def stone():
    n = fbm(11, base=4); v = fbm(12, base=10)
    crack = np.exp(-(np.abs(v - 0.5) * 90) ** 2)
    img = ramp(n * 0.7 + 0.15, [(0, hexc('1a0f08')), (0.5, hexc('5a3a22')), (1, hexc('a07850'))])
    img *= (0.75 + 0.35 * fbm(13, base=40, octaves=3))[..., None]           # grain
    lit = np.clip(cv2.Sobel(n, cv2.CV_32F, 1, 1, ksize=5) * 4, -0.25, 0.25)  # relief
    img = img + lit[..., None]
    img = over(img, crack * 0.85, hexc('0a0503'))
    img = add(img, glow(crack, 3) * 0.35 * (fbm(14, base=3) > 0.55), hexc('ff9a30'))
    img = emblem(img, W / 2, H * 0.72, 150, "gold", hexc('ffb040'), kanji_col=hexc('b0601a'), width=620)
    return frame(img, "bronze", hexc('ffb040'), 0.4)

def water():
    p = np.zeros((H, W), np.float32); warp = fbm(21, base=3) * 40
    for k, (fx, fy) in enumerate(((0.021, 0.013), (-0.017, 0.019), (0.011, -0.023), (0.026, 0.007))):
        p += np.sin(xx * fx + yy * fy + warp * (0.3 + k * 0.1))
    caust = np.exp(-(np.abs(p) * 3.0) ** 2)
    img = ramp(yy / H * 0.7 + fbm(22) * 0.3, [(0, hexc('0a5aa0')), (0.6, hexc('062a5a')), (1, hexc('020a1e'))])
    img = add(img, caust * 0.5 + glow(caust, 3) * 0.4, hexc('9ae6ff'))
    r_ = rng(23); bub = np.zeros((H, W), np.float32)
    for _ in range(40): cv2.circle(bub, (int(r_.uniform(60, W - 60)), int(r_.uniform(60, H - 60))), int(r_.uniform(3, 12)), 1.0, 1, cv2.LINE_AA)
    img = add(img, glow(bub, 1) * 0.8, hexc('d8f6ff'))
    img = emblem(img, W / 2, H * 0.32, 150, "silver", hexc('7ad8ff'), kanji_col=hexc('1a8ad8'), width=620)
    return frame(img, "azure", hexc('7ad8ff'), 0.3)

def fire():
    n = fbm(31, base=3, aspect=(1.0, 2.2)); n2 = fbm(32, base=6, aspect=(0.6, 1.5))
    h = np.clip((yy / H) ** 1.2 + (n - 0.5) * 0.7 + (n2 - 0.5) * 0.35, 0, 1)
    img = ramp(h, [(0, hexc('000000')), (0.35, hexc('3a0402')), (0.6, hexc('b8200a')), (0.8, hexc('ff7a10')), (0.93, hexc('ffd860')), (1, hexc('fff8d0'))])
    r_ = rng(33); em = np.zeros((H, W), np.float32)
    for _ in range(90): cv2.circle(em, (int(r_.uniform(50, W - 50)), int(r_.uniform(80, H - 80))), int(r_.uniform(1, 3)), 1.0, -1, cv2.LINE_AA)
    img = add(img, glow(em, 1.5) * 1.5 + glow(em, 6) * 0.6, hexc('ffb040'))
    img = emblem(img, W / 2, H * 0.27, 150, "gold", hexc('ff6a10'), kanji_col=hexc('e02a10'), width=640)
    return frame(img, "crimson", hexc('ff5a10'), 0.3)

def wind():
    r, th = polar()
    sw = th * 2 - r / 95 + fbm(41, base=3) * 2.5
    bands = 0.5 + 0.5 * np.sin(sw * 4)
    img = ramp(bands * 0.5 + np.clip(1 - r / 700, 0, 1) * 0.5, [(0, hexc('02140a')), (0.45, hexc('0a5a2e')), (0.75, hexc('3ac87a')), (1, hexc('e8ffe8'))])
    streak = np.exp(-((np.sin(sw * 12) - 0.98) ** 2) * 900) * np.clip(r / 300, 0, 1)
    img = add(img, streak * 0.6, hexc('d8ffe8'))
    img = emblem(img, W / 2, H / 2, 150, "silver", hexc('8affb0'), kanji_col=hexc('1ea860'), width=600)
    return frame(img, "jade", hexc('8affb0'), 0.3)

def circuit(seed, bg, glowc, metal_name):
    img = ramp(fbm(seed, base=2) * 0.6 + yy / H * 0.4, [(0, bg), (1, tuple(c * 0.3 for c in bg))])
    r_ = rng(seed + 1); m = np.zeros((H, W), np.float32); dots = np.zeros((H, W), np.float32)
    g = 27
    for _ in range(60):
        x, y = int(r_.integers(2, W // g - 2)) * g, int(r_.integers(2, H // g - 2)) * g
        d = r_.integers(0, 8)
        for _ in range(int(r_.integers(4, 14))):
            if r_.random() < 0.3: d = (d + r_.choice([-1, 1])) % 8
            dx, dy = [(1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1)][d]
            nx, ny = x + dx * g, y + dy * g
            if not (60 < nx < W - 60 and 60 < ny < H - 60): break
            cv2.line(m, (x, y), (nx, ny), 1.0, 2, cv2.LINE_AA); x, y = nx, ny
        cv2.circle(dots, (x, y), 6, 1.0, 2, cv2.LINE_AA)
    r, _ = polar(cy=H / 2 - 30)
    m *= np.clip((r - 210) / 60, 0, 1); dots *= np.clip((r - 210) / 60, 0, 1)
    img = add(img, (m + dots) * 0.9 + glow(m + dots, 5) * 0.8, glowc)
    hexring = np.exp(-((r - 225) ** 2) / 8) + np.exp(-((r - 245) ** 2) / 3) * 0.6
    img = add(img, hexring + glow(hexring, 8), glowc)
    img = emblem(img, W / 2, H / 2 - 30, 150, metal_name, glowc, kanji_col=tuple(c * 0.8 for c in glowc), width=430)
    return frame(img, metal_name, glowc, 0.3)

def hyperspace():
    r, th = polar(cy=H / 2 - 30)
    ang = (th + math.pi) / (2 * math.pi)
    lines = cv2.resize(rng(51).random((1, 900)).astype(np.float32), (4096, 1), interpolation=cv2.INTER_NEAREST)[0]
    s = (lines[(ang * 4095).astype(int)] > 0.82).astype(np.float32) * np.clip((r - 120) / 500, 0, 1) ** 1.5
    img = ramp(fbm(52, base=2) * 0.5 + np.clip(1 - r / 900, 0, 1) * 0.5, [(0, hexc('010208')), (0.6, hexc('0a1440')), (1, hexc('3a2a90'))])
    img = add(img, s * 0.7 + glow(s, 2) * 0.6, hexc('a8c8ff'))
    img = stars(img, 53, 160, (0.9, 0.95, 1), (0.6, 2.8))
    img = add(img, glow((r < 160).astype(np.float32), 60) * 0.9, hexc('6a8aff'))
    img = emblem(img, W / 2, H * 0.74, 150, "silver", hexc('9ab8ff'), kanji_col=hexc('4a6aff'), width=640)
    return frame(img, "silver", hexc('9ab8ff'), 0.3)

def millennium():
    img = ramp(fbm(61, base=3) * 0.5 + 0.2, [(0, hexc('040612')), (1, hexc('1a1a34'))])
    r_ = rng(62); glyph = np.zeros((H, W), np.float32)
    cw, ch = 46, 64
    for gy in range(70, H - 70, ch):          # columns of invented glyphs, carved in gold lines
        for gx in range(66, W - 66, cw):
            for _ in range(r_.integers(2, 5)):
                k = r_.integers(0, 5); x0, y0 = gx + r_.integers(6, 30), gy + r_.integers(8, 44)
                if k == 0: cv2.circle(glyph, (int(x0), int(y0)), int(r_.integers(4, 9)), 1.0, 2, cv2.LINE_AA)
                elif k == 1: cv2.line(glyph, (int(x0), int(y0)), (int(x0), int(y0 + r_.integers(10, 20))), 1.0, 2, cv2.LINE_AA)
                elif k == 2: cv2.ellipse(glyph, (int(x0), int(y0)), (10, 5), 0, 0, 180, 1.0, 2, cv2.LINE_AA)
                elif k == 3: cv2.line(glyph, (int(x0 - 8), int(y0)), (int(x0 + 8), int(y0)), 1.0, 2, cv2.LINE_AA)
                else: cv2.polylines(glyph, [np.array([[x0 - 7, y0 + 7], [x0, y0 - 7], [x0 + 7, y0 + 7]], np.int32)], False, 1.0, 2, cv2.LINE_AA)
            cv2.line(glyph, (gx - 4, gy), (gx - 4, gy + ch - 8), 0.6, 1, cv2.LINE_AA)
    img = add(img, glyph * 0.28, hexc('c89a40'))
    r, _ = polar(cy=H / 2 - 20)
    img = img * (1 - glow((r < 300).astype(np.float32), 40)[..., None] * 0.75)
    # big puzzle pyramid: bevelled triangles
    cx, cy, s = W / 2, H / 2 - 20, 300
    tri = np.zeros((H, W), np.float32)
    rows = 4
    for i in range(rows):
        for j in range(i + 1):
            for up in (True, False):
                if not up and j == i: continue
                yt = cy - s * 0.6 + i * s * 1.1 / rows; hgt = s * 1.1 / rows
                xl = cx - (i + 1) * s * 0.62 / rows + j * 2 * s * 0.62 / rows
                if up: pts = [(xl + s * 0.62 / rows, yt), (xl, yt + hgt), (xl + 2 * s * 0.62 / rows, yt + hgt)]
                else: pts = [(xl + s * 0.62 / rows, yt + hgt), (xl + 2 * s * 0.62 / rows, yt), (xl + 3 * s * 0.62 / rows, yt + hgt)]
                c = np.mean(pts, axis=0); pts = [(c + (np.array(p) - c) * 0.9) for p in pts]
                cv2.fillPoly(tri, [np.array(pts, np.int32)], 1.0, cv2.LINE_AA)
    img = add(img, glow(tri, 18) * 0.5, hexc('ffb030'))
    img = metal_fill(img, tri, "gold")
    img = emblem(img, W / 2, H - 230, 150, "gold", hexc('ffb030'), kanji_col=hexc('a0601a'), width=600)
    return frame(img, "gold", hexc('ffb030'), 0.5)

def hextunnel():
    img = ramp(fbm(71, base=2) * 0.4, [(0, hexc('0a0216')), (1, hexc('240a40'))])
    m = np.zeros((H, W), np.float32); cx, cy = W // 2, H // 2 - 30
    for k in range(14):
        rad = 30 * 1.28 ** k
        pts = np.array([[cx + rad * math.cos(math.radians(60 * i + 30)), cy + rad * math.sin(math.radians(60 * i + 30)) * 1.15] for i in range(6)], np.int32)
        cv2.polylines(m, [pts], True, 1.0 - k * 0.03, 2 + k // 4, cv2.LINE_AA)
    for i in range(6):
        a = math.radians(60 * i + 30); cv2.line(m, (cx, cy), (int(cx + 1500 * math.cos(a)), int(cy + 1500 * math.sin(a) * 1.15)), 0.5, 2, cv2.LINE_AA)
    img = add(img, m * 0.6 + glow(m, 6) * 0.9, hexc('b05aff'))
    img = add(img, glow(np.exp(-((yy - cy) ** 2) / 2), 2) * 0.0, (1, 1, 1))
    img = emblem(img, W / 2, H / 2 - 30, 150, "violet", hexc('d08aff'), kanji_col=hexc('9a3ae0'), width=560)
    return frame(img, "violet", hexc('c070ff'), 0.4)

def artback(passcode, metal_name, accent, seed):
    art = cv2.imread(os.path.join(ART, f"art-{passcode}.jpg"))[:, :, ::-1].astype(np.float32) / 255
    s = max(H / art.shape[0], W / art.shape[1])
    big = cv2.resize(art, (int(art.shape[1] * s) + 1, int(art.shape[0] * s) + 1), interpolation=cv2.INTER_LANCZOS4)
    x0, y0 = (big.shape[1] - W) // 2, (big.shape[0] - H) // 3
    img = big[y0:y0 + H, x0:x0 + W].copy()
    sharp = cv2.GaussianBlur(img, (0, 0), 2); img = np.clip(img * 1.6 - sharp * 0.6, 0, 1)
    # darken the bottom for the plaque, keep the art bright on top
    img *= (1 - np.clip((yy - H * 0.72) / (H * 0.3), 0, 1) * 0.65)[..., None]
    # small emblem + wordmark plaque in the bottom-right
    img = emblem(img, W / 2, H - 170, 58, metal_name, accent, kanji_col=accent, width=470)
    return frame(img, metal_name, accent, 0.25)

STYLES = [
    ("01_vortex", vortex),
    ("02_silver_grid", grid),
    ("03_light", lambda: burst("light", hexc('fff2a0'), hexc('e0a820'), hexc('5a3a06'), "gold", 81)),
    ("04_dark", lambda: lightning(91, hexc('2a0a46'), hexc('d070ff'), "violet")),
    ("05_earth", stone),
    ("06_water", water),
    ("07_fire", fire),
    ("08_wind", wind),
    ("09_circuit_blue", lambda: circuit(101, hexc('061a3a'), hexc('3ae0ff'), "silver")),
    ("10_circuit_black", lambda: circuit(111, hexc('0a0a0e'), hexc('7affc0'), "gold")),
    ("11_hyperspace", hyperspace),
    ("12_millennium", millennium),
    ("13_hex_cup", hextunnel),
    ("14_art_dark_magician", lambda: artback("46986414", "violet", hexc('c07aff'), 1)),
    ("15_art_blue_eyes", lambda: artback("89631139", "silver", hexc('8ad0ff'), 2)),
    ("16_art_red_eyes", lambda: artback("74677422", "crimson", hexc('ff5a3a'), 3)),
]

only = set(ARGS[3:])
for i, (name, fn) in enumerate(STYLES):
    if only and name not in only: continue
    finish(("mat_" if MAT else "") + name, fn(), seed=i); print("made", name, flush=True)

pre, per = ("mat_", 4) if MAT else ("", 8)
files = [os.path.join(OUT, pre + n + ".png") for n, _ in STYLES if os.path.exists(os.path.join(OUT, pre + n + ".png"))]
th = [cv2.resize(cv2.imread(f), (480, 322) if MAT else (271, 395), interpolation=cv2.INTER_AREA) for f in files]
while len(th) % per: th.append(np.full_like(th[0], 24))
pad = lambda im: cv2.copyMakeBorder(im, 10, 10, 10, 10, cv2.BORDER_CONSTANT, value=(24, 24, 24))
cv2.imwrite(os.path.join(OUT, pre + "sheet.png"), np.vstack([np.hstack([pad(t) for t in th[i:i + per]]) for i in range(0, len(th), per)]))
print("sheet")
