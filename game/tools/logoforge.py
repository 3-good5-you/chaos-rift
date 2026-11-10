"""Title logo samples: the Yu-Gi-Oh! logo (repo root) with "Chaos Ascendant" under it,
ink logo with the name in several letterings, dark versions, loading screens and app icons.
Usage adds: D (dark logos), L (loading screens, need D), I (icons), A (loading transition GIFs, need L), J (icons v2), F (loading shader ink field), M (main-menu textures).
Usage: python logoforge.py <logo_png> <out_dir> [01 02 ...]
Writes <name>.png (transparent) and <name>_preview.png (on a dark backdrop) per sample."""
import os, sys
import numpy as np, cv2
from PIL import Image, ImageDraw, ImageFont

LOGO, OUT = sys.argv[1], sys.argv[2]
FONTS = "C:/Windows/Fonts/"
KANJI = np.array((242, 65, 32), np.float32) / 255
os.makedirs(OUT, exist_ok=True)

def load_logo():
    im = np.asarray(Image.open(LOGO).convert("RGBA")).astype(np.float32) / 255
    ys, xs = np.nonzero(im[:, :, 3] > 0.02)
    return im[ys.min():ys.max() + 1, xs.min():xs.max() + 1]

def text_mask(text, font, size, track=0.0, caps_font=None, caps_scale=1.0, smallcaps=False):
    """White-on-black mask of the text, tight-cropped, baselines aligned. track = extra spacing per letter, in ems.
    caps_font / caps_scale: capitals in another font and/or size (readable initials on gothic lowercase)."""
    f = ImageFont.truetype(FONTS + font, size)
    fc = ImageFont.truetype(FONTS + (caps_font or font), int(size * caps_scale))
    fs = ImageFont.truetype(FONTS + font, int(size * 0.78))
    pick = lambda c: fc if c.isupper() else f
    if smallcaps:
        pick = lambda c: fs if c.islower() else fc
    w = int(sum(pick(c).getlength(c) for c in text) + len(text) * size * track + size * 2)
    img = Image.new("L", (w, int(size * 2.6)), 0); d = ImageDraw.Draw(img)
    x = size * 0.5
    for c in text:
        g = c.upper() if smallcaps else c
        d.text((x, size * 1.8), g, font=pick(c), fill=255, anchor="ls")
        x += pick(c).getlength(g) + size * track
    m = np.asarray(img).astype(np.float32) / 255
    ys, xs = np.nonzero(m > 0.05)
    return m[ys.min():ys.max() + 1, xs.min():xs.max() + 1]

def ramp(t, stops):
    t = np.clip(t, 0, 1); out = np.zeros(t.shape + (3,), np.float32)
    for (t0, c0), (t1, c1) in zip(stops, stops[1:]):
        k = np.clip((t - t0) / max(t1 - t0, 1e-6), 0, 1)[..., None]
        sel = ((t >= t0) & (t <= t1))[..., None]
        out = np.where(sel, np.array(c0) * (1 - k) + np.array(c1) * k, out)
    return out

METALS = {
    "silver": [(0, (0.12, 0.12, 0.13)), (0.35, (0.42, 0.42, 0.44)), (0.6, (0.78, 0.78, 0.8)), (0.85, (0.97, 0.97, 0.98)), (1, (1, 1, 1))],
    "gold":   [(0, (0.22, 0.11, 0.02)), (0.35, (0.6, 0.38, 0.08)), (0.6, (0.92, 0.7, 0.25)), (0.85, (1, 0.9, 0.55)), (1, (1, 0.98, 0.85))],
    "obsidian": [(0, (0.02, 0.02, 0.03)), (0.4, (0.08, 0.07, 0.1)), (0.7, (0.2, 0.18, 0.24)), (1, (0.45, 0.42, 0.5))],
    "blood":  [(0, (0.12, 0.01, 0.04)), (0.35, (0.45, 0.05, 0.2)), (0.6, (0.62, 0.2, 0.62)), (0.85, (0.92, 0.75, 1)), (1, (1, 1, 1))],
    "chaos":  [(0, (0.08, 0.03, 0.12)), (0.35, (0.32, 0.14, 0.45)), (0.6, (0.62, 0.42, 0.82)), (0.85, (0.9, 0.82, 1)), (1, (1, 1, 1))],
}

def chisel(m, bevel, metal):
    """Faceted bevel from the distance field: each facet faces away from the nearest edge, lit from top-left."""
    d = cv2.distanceTransform((m > 0.5).astype(np.uint8), cv2.DIST_L2, 5)
    d = np.minimum(d, bevel)
    gx, gy = cv2.Sobel(d, cv2.CV_32F, 1, 0, ksize=3), cv2.Sobel(d, cv2.CV_32F, 0, 1, ksize=3)
    n = np.sqrt(gx ** 2 + gy ** 2) + 1e-6
    flat = d >= bevel - 0.5
    shade = np.where(flat, 0.0, (-gx * 0.6 - gy * 0.8) / n)          # light from top-left
    h = m.shape[0]
    face = 0.62 + 0.25 * np.cos(np.linspace(0, 2.6, h))[:, None]       # chrome band down the flat top
    t = np.where(flat, face, 0.5 + 0.45 * shade)
    return ramp(t, METALS[metal])

def dilate(m, r):
    k = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * r + 1, 2 * r + 1))
    return cv2.dilate(m, k)

def lettering(text, font, size, metal="silver", track=0.0, inline="gold", outline=0.15, **kw):
    """RGBA layer: chiselled metal letters, thin inline, heavy black outline (logo recipe)."""
    m = text_mask(text, font, size, track, **kw)
    cap = m.shape[0]
    ro, ri = max(int(cap * outline), 3), max(int(cap * 0.03), 2)
    pad = ro + 4
    m = np.pad(m, pad)
    ring_in = np.clip(dilate(m, ri) - m, 0, 1)
    black = dilate(m, ro)
    rgb = np.zeros(m.shape + (3,), np.float32) + (0.07, 0.05, 0.06)
    if inline:
        gold = ramp(np.full(m.shape, 0.62, np.float32) + 0.2 * np.cos(np.linspace(0, 3, m.shape[0]))[:, None], METALS[inline])
        rgb = rgb * (1 - ring_in[..., None]) + gold * ring_in[..., None]
    rgb = rgb * (1 - m[..., None]) + chisel(m, max(cap * 0.09, 2), metal) * m[..., None]
    return np.dstack([rgb, black])

def brush(w, h, seed):
    """Dry-brush red stroke, like the kanji behind the logo."""
    r = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    u, v = xx / w, (yy - h / 2) / (h / 2)
    taper = np.clip(np.minimum(u / 0.06, (1 - u) / 0.03), 0, 1) ** 0.6
    wob = cv2.GaussianBlur(r.normal(0, 1, (1, w)).astype(np.float32), (0, 0), w * 0.02)[0] * 1.5
    edge = 0.8 * taper + 0.08 * wob[None, :] + 0.05 * cv2.resize(r.normal(0, 1, (8, w // 6)).astype(np.float32), (w, h))
    body = (np.abs(v + 0.1 * np.sin(u * 5)) < edge).astype(np.float32)
    streak = cv2.resize(r.random((h // 6, 1)).astype(np.float32), (w, h), interpolation=cv2.INTER_LINEAR)
    gaps = (streak > 0.12 + 0.7 * np.clip((u - 0.75) / 0.25, 0, 1)).astype(np.float32)  # dry tail
    a = cv2.GaussianBlur(body * gaps, (0, 0), 1.2)
    rgb = np.zeros((h, w, 3), np.float32) + KANJI
    rgb *= (0.9 + 0.1 * r.random((h, w, 1))).astype(np.float32)
    return np.dstack([rgb, a])

def over(dst, src, x, y):
    """Alpha-composite src onto dst at (x, y), clipped."""
    sx, sy = max(-x, 0), max(-y, 0); x, y = max(x, 0), max(y, 0)
    src = src[sy:sy + dst.shape[0] - y, sx:sx + dst.shape[1] - x]
    h, w = src.shape[:2]
    a = src[..., 3:4]; d = dst[y:y + h, x:x + w]
    out_a = a + d[..., 3:4] * (1 - a)
    d[..., :3] = (src[..., :3] * a + d[..., :3] * d[..., 3:4] * (1 - a)) / np.maximum(out_a, 1e-6)
    d[..., 3:4] = out_a

def shadow(layer, blur, alpha=0.6):
    s = np.zeros_like(layer); s[..., 3] = cv2.GaussianBlur(layer[..., 3], (0, 0), blur) * alpha
    return s

def compose(name, sub, sub_w, overlap, brush_seed=None, sub2=None, sub2_w=0.62, align=0.5, logo=None, bg=None):
    """bg: callable (h, w) -> RGBA laid under the finished logo (e.g. a paper sheet)."""
    logo = load_logo() if logo is None else logo; lh, lw = logo.shape[:2]
    sub = cv2.resize(sub, (int(sub_w * lw), int(sub.shape[0] * sub_w * lw / sub.shape[1])), interpolation=cv2.INTER_AREA)
    sh, sw = sub.shape[:2]
    sub2h = 0
    if sub2 is not None:
        sub2 = cv2.resize(sub2, (int(sw * sub2_w), int(sub2.shape[0] * sw * sub2_w / sub2.shape[1])), interpolation=cv2.INTER_AREA)
        sub2h = sub2.shape[0]
    M = 80
    H = lh + sh + sub2h - int(overlap * lh) + 2 * M; W = lw + 2 * M
    can = np.zeros((H, W, 4), np.float32)
    over(can, logo, M, M)
    y = M + lh - int(overlap * lh)
    if brush_seed is not None:
        b = brush(int(sw * 1.15), int(sh * 1.6), brush_seed)
        over(can, b, (W - b.shape[1]) // 2, y + (sh - b.shape[0]) // 2)
    x = int(M + (lw - sw) * align)
    over(can, shadow(sub, 10), x + 8, y + 12)
    over(can, sub, x, y)
    if sub2 is not None:
        over(can, shadow(sub2, 8), (W - sub2.shape[1]) // 2 + 6, y + sh - 10)
        over(can, sub2, (W - sub2.shape[1]) // 2, y + sh - 18)
    ys, xs = np.nonzero(can[..., 3] > 0.01)
    can = can[max(ys.min() - 20, 0):ys.max() + 20, max(xs.min() - 20, 0):xs.max() + 20]
    if bg is not None:
        can = pad(can, 160); can = under(can, bg(*can.shape[:2]))
    out = (np.clip(can, 0, 1) * 255).astype(np.uint8)
    Image.fromarray(out, "RGBA").save(os.path.join(OUT, name + ".png"))
    # previews: dark radial backdrop and a light one, 1600 wide
    h, w = can.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    r = np.sqrt(((xx - w / 2) / w) ** 2 + ((yy - h / 2) / h) ** 2)
    dark = ramp(np.clip(1 - r * 1.6, 0, 1), [(0, (0.02, 0.02, 0.03)), (1, (0.13, 0.1, 0.16))])
    light = ramp(np.clip(1 - r * 1.6, 0, 1), [(0, (0.8, 0.8, 0.82)), (1, (0.96, 0.96, 0.97))])
    for suffix, bgc in (("_preview", dark), ("_light", light)):
        prev = bgc * (1 - can[..., 3:4]) + can[..., :3] * can[..., 3:4]
        prev = cv2.resize(prev, (1600, int(h * 1600 / w)), interpolation=cv2.INTER_AREA)
        Image.fromarray((np.clip(prev, 0, 1) * 255).astype(np.uint8)).save(os.path.join(OUT, name + suffix + ".png"))

def hcat(a, b, gap, dy=0):
    """Two lettering layers side by side, bottoms (baselines) aligned, b raised by dy; outlines overlap into one."""
    h = max(a.shape[0], b.shape[0] + dy)
    a = np.pad(a, ((h - a.shape[0], 0), (0, 0), (0, 0))); b = np.pad(b, ((h - b.shape[0] - dy, dy), (0, 0), (0, 0)))
    out = np.zeros((h, a.shape[1] + b.shape[1] - gap, 4), np.float32)
    over(out, a, 0, 0); over(out, b, a.shape[1] - gap, 0)
    return out

def pad(layer, p):
    return np.pad(layer, ((p, p), (p, p), (0, 0)))

def under(layer, fx):
    """fx (same size RGBA) composited beneath layer."""
    out = fx.copy(); over(out, layer, 0, 0); return out

def glow(layer, r, col, strength=1.0):
    a = np.clip(cv2.GaussianBlur(layer[..., 3], (0, 0), r) * strength, 0, 1)
    return np.dstack([np.zeros(a.shape + (3,), np.float32) + col, a])

def sparks(layer, n, col, seed):
    """Rising embers around the letters."""
    r = np.random.default_rng(seed); h, w = layer.shape[:2]
    m = np.zeros((h, w), np.float32)
    for _ in range(n):
        x, y = int(r.uniform(0.05, 0.95) * w), int(r.uniform(0.02, 0.98) * h)
        cv2.line(m, (x, y), (x + int(r.uniform(-3, 3)), y - int(r.uniform(4, 22))), float(r.uniform(0.5, 1)), int(r.uniform(2, 5)), cv2.LINE_AA)
    a = np.clip(m + cv2.GaussianBlur(m, (0, 0), 6) * 1.5, 0, 1)
    return np.dstack([np.zeros((h, w, 3), np.float32) + col, a])

def lightning(layer, n, col, seed):
    """Forked chaos bolts crackling out from behind the letters."""
    r = np.random.default_rng(seed); h, w = layer.shape[:2]
    m = np.zeros((h, w), np.float32)
    for _ in range(n):
        x, y = r.uniform(0.1, 0.9) * w, h * 0.5
        ang = r.choice([-1, 1]) * r.uniform(0.6, 1.4) + (np.pi if r.random() < 0.5 else 0)
        pts = [(x, y)]
        for _ in range(9):
            ang += r.uniform(-0.6, 0.6); x += np.cos(ang) * h * 0.09; y += np.sin(ang) * h * 0.09
            pts.append((x, y))
        cv2.polylines(m, [np.array(pts, np.int32)], False, 1.0, 4, cv2.LINE_AA)
    a = np.clip(m + cv2.GaussianBlur(m, (0, 0), 10) * 2, 0, 1)
    rgb = np.zeros((h, w, 3), np.float32) + col
    rgb = rgb * (1 - m[..., None]) + m[..., None]                     # white-hot core
    return np.dstack([rgb, a])

def arc(layer, amount):
    """Bend the line upward in the middle (it ascends)."""
    layer = np.pad(layer, ((amount, 0), (0, 0), (0, 0)))
    h, w = layer.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    off = amount * (1 - ((xx - w / 2) / (w / 2)) ** 2)
    return cv2.remap(layer, xx, yy + off, cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT, borderValue=0)

def pip(size, metal="silver"):
    """Chiselled diamond, like the dots on the logo's i and !."""
    s = size; m = np.zeros((s, s), np.float32)
    cv2.fillPoly(m, [np.array([(s // 2, 0), (s - 1, s // 2), (s // 2, s - 1), (0, s // 2)], np.int32)], 1.0, cv2.LINE_AA)
    ro = int(s * 0.14); m = np.pad(m, ro + 2)
    ring = np.clip(dilate(m, max(int(s * 0.05), 2)) - m, 0, 1)
    gold = ramp(np.full(m.shape, 0.7, np.float32), METALS["gold"])
    rgb = np.zeros(m.shape + (3,), np.float32) + (0.07, 0.05, 0.06)
    rgb = rgb * (1 - ring[..., None]) + gold * ring[..., None]
    rgb = rgb * (1 - m[..., None]) + chisel(m, s, metal) * m[..., None]
    return np.dstack([rgb, dilate(m, ro)])

def plaque(layer, metal="obsidian", px=0.5, py=0.18):
    """Pointed banner plate behind the lettering: dark metal, gold rim, black outline."""
    h, w = layer.shape[:2]; ph, pw = int(h * (1 + 2 * py)), int(w + h * 2 * px)
    e = int(ph * 0.5); m = np.zeros((ph, pw), np.float32)
    cv2.fillPoly(m, [np.array([(0, ph // 2), (e, 0), (pw - e, 0), (pw - 1, ph // 2), (pw - e, ph - 1), (e, ph - 1)], np.int32)], 1.0, cv2.LINE_AA)
    ro = int(ph * 0.08); m = np.pad(m, ro + 2)
    inner = cv2.erode(m, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (int(ph * 0.12) | 1,) * 2))
    rgb = chisel(m, ph * 0.08, "gold")
    rgb = rgb * (1 - inner[..., None]) + chisel(inner, ph * 0.04, metal) * inner[..., None]
    out = np.dstack([rgb, dilate(m, ro)])
    over(out, layer, (out.shape[1] - w) // 2, (out.shape[0] - h) // 2)
    return out

def fx(layer, p, *makers):
    """Pad the lettering by p and put the effect layers (made from the padded lettering) beneath it."""
    layer = pad(layer, p)
    for mk in makers: layer = under(layer, mk(layer))
    return layer

S = 400   # render size; downscaled into place

def noise(h, w, seed, scale=64, octaves=4):
    """Smooth value noise in 0..1."""
    r = np.random.default_rng(seed); out = np.zeros((h, w), np.float32); amp, tot = 1.0, 0.0
    for _ in range(octaves):
        s = max(int(scale), 2)
        out += cv2.resize(r.random((h // s + 2, w // s + 2)).astype(np.float32), (w, h), interpolation=cv2.INTER_CUBIC) * amp
        tot += amp; amp *= 0.5; scale /= 2
    out /= tot
    return (out - out.min()) / (out.max() - out.min() + 1e-6)

def erode(m, r):
    return cv2.erode(m, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * r + 1, 2 * r + 1)))

shift = lambda m, dx, dy: np.roll(np.roll(m, dy, 0), dx, 1)
rgba = lambda rgb, a: np.dstack([rgb, a]).astype(np.float32)
col = lambda c, shape: np.zeros(shape + (3,), np.float32) + np.array(c, np.float32)
vgrad = lambda h, w, stops: ramp(np.repeat(np.linspace(0, 1, h, dtype=np.float32)[:, None], w, 1), stops)
mask = lambda text, font, size=S, p=80, **kw: np.pad(text_mask(text, font, size, **kw), p)

def rot(layer, deg):
    """Rotate (counter-clockwise for +deg), canvas grown to fit."""
    h, w = layer.shape[:2]; M = cv2.getRotationMatrix2D((w / 2, h / 2), deg, 1)
    c, s = abs(M[0, 0]), abs(M[0, 1]); nw, nh = int(h * s + w * c) + 2, int(h * c + w * s) + 2
    M[0, 2] += nw / 2 - w / 2; M[1, 2] += nh / 2 - h / 2
    return cv2.warpAffine(layer, M, (nw, nh), flags=cv2.INTER_LINEAR, borderValue=0)

def paste_max(dst, src, cx, cy):
    """Max-blend a 2D mask into dst centred at (cx, cy)."""
    h, w = src.shape; x0, y0 = int(cx - w / 2), int(cy - h / 2)
    dst[y0:y0 + h, x0:x0 + w] = np.maximum(dst[y0:y0 + h, x0:x0 + w], src)

METALS["ice"] = [(0, (0.02, 0.1, 0.25)), (0.4, (0.1, 0.45, 0.75)), (0.7, (0.55, 0.88, 1)), (1, (1, 1, 1))]
METALS["sand"] = [(0, (0.3, 0.2, 0.11)), (0.4, (0.6, 0.45, 0.28)), (0.7, (0.82, 0.68, 0.46)), (1, (0.95, 0.86, 0.66))]

# ---------------------------------------------------------------- ink-strip family + recoloured logos

BRUSH = "BRUSHSCI.TTF"

def logo_variant(kanji=None, letters=None, edge=None, halo=None):
    """The Yu-Gi-Oh! logo recoloured: kanji brush colour, letter metal, thin edge metal, optional halo behind."""
    im = load_logo(); r, g, b, a = [im[..., i] for i in range(4)]
    soft = lambda m: cv2.GaussianBlur(m.astype(np.float32), (0, 0), 0.7)
    kan = soft((r > 0.6) & (g < 0.45) & (b < 0.3))[..., None]
    gold = soft((r > 0.55) & (g > 0.35) & (b < 0.25) & (g < r * 0.85))[..., None]
    lum = 0.3 * r + 0.59 * g + 0.11 * b
    silver = soft((np.abs(r - g) < 0.12) & (np.abs(g - b) < 0.12) & (lum > 0.25))[..., None]
    rgb = im[..., :3].copy()
    if kanji is not None: rgb = rgb * (1 - kan) + col(kanji, r.shape) * (0.8 + 0.4 * (r - 0.6))[..., None] * kan
    if letters: rgb = rgb * (1 - silver) + ramp(np.clip(lum * 1.05, 0, 1), METALS[letters]) * silver
    if edge: rgb = rgb * (1 - gold) + ramp(np.full(r.shape, 0.65, np.float32), METALS[edge]) * gold
    out = rgba(rgb, a)
    if halo: out = under(pad(out, 90), glow(pad(out, 90), 45, halo, 1.4))
    return out

def seal(w, h, c, seed=54):
    """Vertical hanko: 混 over 沌 (chaos), cut out of a solid square of ink."""
    m = np.zeros((h, w), np.float32); cv2.rectangle(m, (4, 4), (w - 5, h - 5), 1.0, -1)
    gm = np.zeros_like(m)
    for ch, cy in (("混", 0.28), ("沌", 0.72)):
        k = text_mask(ch, "YuGothB.ttc", h)
        k = cv2.resize(k, (int(w * 0.74), int(k.shape[0] * w * 0.74 / k.shape[1])), interpolation=cv2.INTER_AREA)
        paste_max(gm, k, w / 2, h * cy)
    m = np.clip(m - gm, 0, 1) * (0.8 + 0.2 * noise(h, w, seed, 4))
    return rgba(col(c, m.shape), m)

def brush_mask(text="Chaos Ascendant", size=int(S * 1.3), bold=3):
    """Brush script, thickened so it reads at title size."""
    return dilate(text_mask(text, BRUSH, size), bold)

def strip(paper=(0.94, 0.9, 0.8), ink=(0.04, 0.03, 0.05), seal_c=tuple(KANJI), frame="gold", tilt=2, seed=51):
    """Torn paper strip, brush-script name, vertical hanko; framed in the logo's own gold inline + black outline."""
    t = brush_mask(); th, tw = t.shape
    px, py = int(th * 0.35), int(th * 0.22); sw, sh = int(th * 0.5), int(th * 0.92)
    w, h = tw + 2 * px + sw + px // 2 + 60, max(th, sh) + 2 * py + 60
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.minimum.reduce([xx - 30, w - 31 - xx, yy - 30, h - 31 - yy])
    a = cv2.GaussianBlur((d > noise(h, w, seed, 10) * 22).astype(np.float32), (0, 0), 0.8)
    rgb = col(paper, (h, w)) * (0.9 + 0.14 * noise(h, w, seed + 1, 5))[..., None]
    im = np.zeros((h, w), np.float32); y0 = (h - th) // 2; im[y0:y0 + th, 30 + px:30 + px + tw] = t
    im *= 0.85 + 0.15 * (noise(h, w, seed + 2, 3) > 0.3)
    rgb = rgb * (1 - im[..., None]) + col(ink, (h, w)) * im[..., None]
    sl = rot(seal(sw, sh, seal_c), -3); sx, sy = int(30 + px + tw + px * 0.5), (h - sl.shape[0]) // 2
    lay = rgba(rgb, a); s_ = np.zeros_like(lay); over(s_, sl, sx, sy)
    s_[..., 3] *= a; over(lay, s_, 0, 0)
    if frame:
        lay = pad(lay, 30); a = lay[..., 3]
        ring = np.clip(dilate(a, 7) - a, 0, 1)
        base = rgba(col((0.05, 0.03, 0.04), a.shape), dilate(a, 22))
        over(base, rgba(ramp(np.full(a.shape, 0.65, np.float32), METALS[frame]), ring), 0, 0)
        over(base, lay, 0, 0); lay = base
    return rot(lay, tilt)

def red_brush():
    """The name painted in the kanji's own red brush, with the logo letters' gold inline and black outline."""
    m = np.pad(brush_mask(bold=4), 40); h, w = m.shape
    streak = cv2.resize(noise(h, max(w // 10, 8), 61, 4), (w, h))
    rgb = col(tuple(KANJI), m.shape) * (0.82 + 0.3 * streak)[..., None]
    ring = np.clip(dilate(m, 6) - m, 0, 1)
    out = rgba(col((0.05, 0.03, 0.04), m.shape), dilate(m, 24))
    over(out, rgba(ramp(np.full(m.shape, 0.65, np.float32), METALS["gold"]), ring), 0, 0)
    over(out, rgba(np.clip(rgb, 0, 1), m), 0, 0)
    sl = seal(int(h * 0.36), int(h * 0.66), (0.95, 0.92, 0.85))
    sp = np.pad(sl, ((24, 24), (24, 24), (0, 0))); sp[..., 3] = np.maximum(sp[..., 3], dilate(np.pad(sl[..., 3] > 0, 24).astype(np.float32), 18))
    sp[..., :3] = np.where(np.pad(sl[..., 3:4] > 0.01, ((24, 24), (24, 24), (0, 0))), sp[..., :3], 0.05)
    return hcat(out, rot(sp, -4), -30)

def ink_words(ink=(0.04, 0.03, 0.05), seal_c=tuple(KANJI)):
    """Plain brush ink plus hanko, for sitting straight on a paper sheet."""
    m = np.pad(brush_mask(), 30)
    words = rgba(col(ink, m.shape), m * (0.85 + 0.15 * (noise(*m.shape, 62, 3) > 0.3)))
    return hcat(words, rot(np.pad(seal(int(m.shape[0] * 0.42), int(m.shape[0] * 0.78), seal_c), ((20, 20), (20, 20), (0, 0))), -3), -20)

def paper_sheet(h, w, seed=81):
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.minimum.reduce([xx, w - 1 - xx, yy, h - 1 - yy])
    a = cv2.GaussianBlur((d > 10 + noise(h, w, seed, 14) * 50).astype(np.float32), (0, 0), 1)
    stain = noise(h, w, seed + 1, 160)
    rgb = col((0.93, 0.88, 0.77), (h, w)) * (0.86 + 0.1 * noise(h, w, seed + 2, 5) + 0.08 * stain)[..., None]
    rgb *= (1 - 0.25 * np.clip(1 - d / (min(h, w) * 0.25), 0, 1) ** 2)[..., None]   # darker, aged edges
    return rgba(rgb, a)

VIOLET, CRIMSON = (0.56, 0.22, 0.88), (0.6, 0.04, 0.1)
INK = (0.05, 0.04, 0.05)
G = "OLDENGL.TTF"   # blackletter, echoes the gothic Yu-Gi-Oh! letterforms
HYB = dict(track=0.02, caps_font="CASTELAR.TTF", caps_scale=1.05)   # gothic lowercase, readable Castellar capitals

def sticker(layer, r=14, c=(0.93, 0.89, 0.8)):
    """Pale paper-cut edge round the whole logo so black ink survives on dark menus."""
    layer = pad(layer, r + 4)
    return under(layer, rgba(col(c, layer.shape[:2]), cv2.GaussianBlur(dilate(layer[..., 3], r), (0, 0), 1)))

def ink_stroke(w, h, thick, curve=0.0, seed=90):
    """Tapered dry-brush stroke, optionally bowed (curve = sag as a fraction of h)."""
    m = np.zeros((h, w), np.float32)
    xs = np.linspace(0, w - 1, 200)
    yc = h / 2 + curve * h * (1 - ((xs / (w - 1)) * 2 - 1) ** 2)
    th = thick * np.sin(np.pi * np.clip(xs / (w - 1), 0.02, 0.98)) ** 0.6 * np.where(xs < w * 0.5, 1, 0.75 + 0.25 * (1 - xs / w))
    pts = np.array(list(zip(xs, yc - th / 2)) + list(zip(xs[::-1], (yc + th / 2)[::-1])), np.int32)
    cv2.fillPoly(m, [pts], 1.0, cv2.LINE_AA)
    streak = cv2.resize(noise(max(h // 3, 4), 8, seed, 2), (w, h))
    return m * (streak > 0.18 + 0.5 * np.clip((np.arange(w) / w - 0.8) / 0.2, 0, 1)[None, :])

def ink_layer(m, c=INK):
    return rgba(col(c, m.shape), np.clip(m, 0, 1))

def seal_for(h, c=tuple(KANJI)):
    return rot(np.pad(seal(int(h * 0.42), int(h * 0.78), c), ((20, 20), (20, 20), (0, 0))), -3)

# --- the name, six different letterings ---

def s_script():
    """Brush script + hanko (the one picked)."""
    return ink_words()

def s_dry_caps():
    """Bold brush capitals: slanted, ragged edges, dry streaks."""
    m = np.pad(text_mask("CHAOS ASCENDANT", "FRAHV.TTF", S, track=0.04), 60)
    h, w = m.shape
    m = cv2.warpAffine(m, np.float32([[1, -0.22, h * 0.22], [0, 1, 0]]), (w + int(h * 0.1), h))
    h, w = m.shape; yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    m = cv2.remap(m, xx + (noise(h, w, 91, 6) - 0.5) * 10, yy + (noise(h, w, 92, 6) - 0.5) * 10, cv2.INTER_LINEAR)
    streak = cv2.resize(noise(max(h // 4, 4), max(w // 60, 4), 93, 2), (w, h))
    m = m * np.clip((streak - 0.2) * 4, 0.25, 1)
    return hcat(ink_layer(m), seal_for(h), -10)

def s_chrome_gothic():
    """Gothic letters in the logo's own silver chrome, gold inline, black outline."""
    return lettering("Chaos Ascendant", G, S, metal="silver", inline="gold", **HYB)

def s_red_swash():
    """A red brush swash (the kanji's red) with the name in pale paper capitals on it."""
    t = text_mask("CHAOS ASCENDANT", "COPRGTB.TTF", S, track=0.1)
    th, tw = t.shape
    sw = brush(int(tw * 1.25), int(th * 2.4), 12)
    h, w = sw.shape[:2]
    tm = np.zeros((h, w), np.float32); paste_max(tm, t, w / 2, h / 2)
    out = sw.copy()
    out[..., :3] = out[..., :3] * (1 - tm[..., None]) + np.array((0.96, 0.93, 0.86), np.float32) * tm[..., None]
    out[..., 3] = np.maximum(out[..., 3], tm)
    return out

def s_elegant():
    """Wide-tracked titling capitals in silver chrome, brush flourishes either side."""
    t = lettering("CHAOS ASCENDANT", "PERTIBD.TTF", S, metal="silver", inline="gold", track=0.16, outline=0.1)
    h = t.shape[0]; L = int(h * 2.4)
    left = ink_layer(ink_stroke(L, h, h * 0.16, seed=94)); right = ink_layer(ink_stroke(L, h, h * 0.16, seed=95)[:, ::-1])
    return hcat(hcat(left, t, -10), right, -10)

def s_calligraphy():
    """Chancery calligraphy with a long brush underline sweeping under the name."""
    t = np.pad(dilate(text_mask("Chaos Ascendant", "MTCORSVA.TTF", int(S * 1.5)), 2), 40)
    h, w = t.shape
    u = ink_stroke(int(w * 1.05), int(h * 0.6), h * 0.12, curve=-0.25, seed=96)
    m = np.zeros((h + int(h * 0.35), u.shape[1]), np.float32)
    m[:h, (u.shape[1] - w) // 2:(u.shape[1] - w) // 2 + w] = t
    m[-u.shape[0]:, :] = np.maximum(m[-u.shape[0]:, :], u)
    return hcat(ink_layer(m), seal_for(int(h * 0.9)), -10)

# ---------------------------------------------------------------- dark versions, loading screens, app icons

def save(name, img):
    arr = (np.clip(img, 0, 1) * 255).astype(np.uint8)
    Image.fromarray(arr, "RGBA" if arr.shape[2] == 4 else "RGB").save(os.path.join(OUT, name + ".png"))

def load_png(name):
    return np.asarray(Image.open(os.path.join(OUT, name + ".png")).convert("RGBA")).astype(np.float32) / 255

def place(bg, layer, width, cy=0.5):
    """Layer centred on an RGB backdrop at `width` of its width, with a soft drop shadow."""
    H, W = bg.shape[:2]; lw = int(W * width); lh = int(layer.shape[0] * lw / layer.shape[1])
    q = int(lw * 0.04)   # room for the shadow's blur, so it doesn't end in a hard box
    l = pad(cv2.resize(layer, (lw, lh), interpolation=cv2.INTER_AREA), q)
    out = rgba(bg, np.ones((H, W), np.float32)); x, y = (W - lw) // 2 - q, int(H * cy - lh / 2) - q
    over(out, shadow(l, lw * 0.01, 0.7), x + int(lw * 0.006), y + int(lw * 0.01)); over(out, l, x, y)
    return out[..., :3]

def radial(H, W):
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    return np.hypot((xx - W / 2) / W, (yy - H / 2) / H)

def bg_dark(H, W):
    return ramp(np.clip(1 - radial(H, W) * 1.7, 0, 1), [(0, (0.01, 0.01, 0.015)), (1, (0.12, 0.09, 0.15))]) * (0.94 + 0.06 * noise(H, W, 101, 3))[..., None]

def bg_inkwash(H, W):
    """Charcoal with drifting ink clouds and a huge faint 混沌 behind."""
    g = bg_dark(H, W) * 0.8
    g += (np.clip((noise(H, W, 102, 260) - 0.45) * 3, 0, 1) * 0.2)[..., None] * np.array((0.55, 0.52, 0.6), np.float32)
    k = text_mask("混沌", "YuGothB.ttc", int(H * 0.8))
    sc = min(H * 0.85 / k.shape[0], W * 0.92 / k.shape[1])
    k = cv2.resize(k, (int(k.shape[1] * sc), int(k.shape[0] * sc)), interpolation=cv2.INTER_AREA)
    km = np.zeros((H, W), np.float32); paste_max(km, cv2.GaussianBlur(k, (0, 0), 4), W / 2, H / 2)
    return g + km[..., None] * 0.05

def bg_vortex(H, W):
    """Violet chaos vortex, dark at the eye behind the logo."""
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32); dx, dy = xx - W / 2, yy - H / 2
    r = np.hypot(dx, dy) / H; th = np.arctan2(dy, dx)
    sw = th * 2 + np.log(r + 0.05) * 4 + noise(H, W, 103, 120) * 3
    t = np.clip((0.5 + 0.5 * np.sin(sw * 3)) * 0.5 + np.clip(r * 1.2, 0, 1) * 0.5 - 0.2, 0, 1) * np.clip(r * 3, 0, 1)
    g = ramp(t, [(0, (0.01, 0, 0.02)), (0.5, (0.18, 0.05, 0.3)), (0.8, (0.5, 0.2, 0.8)), (1, (0.9, 0.7, 1))])
    return g * np.clip(1.4 - r * 1.3, 0, 1)[..., None]

def bg_paper(H, W):
    g = col((0.93, 0.88, 0.77), (H, W)) * (0.86 + 0.1 * noise(H, W, 104, 5) + 0.08 * noise(H, W, 105, 200))[..., None]
    return g * (1 - 0.35 * np.clip(radial(H, W) * 1.6 - 0.3, 0, 1) ** 1.5)[..., None]

def rsq(n, rad=0.22):
    """Rounded-square icon mask."""
    m = np.zeros((n, n), np.float32); r = int(n * rad)
    cv2.rectangle(m, (r, 0), (n - 1 - r, n - 1), 1.0, -1); cv2.rectangle(m, (0, r), (n - 1, n - 1 - r), 1.0, -1)
    for cx, cy in ((r, r), (n - 1 - r, r), (r, n - 1 - r), (n - 1 - r, n - 1 - r)): cv2.circle(m, (cx, cy), r, 1.0, -1, cv2.LINE_AA)
    return m

def fit(layer, n, frac):
    """Resize a layer so its longer side is frac * n."""
    s = frac * n / max(layer.shape[:2])
    return cv2.resize(layer, (max(int(layer.shape[1] * s), 1), max(int(layer.shape[0] * s), 1)), interpolation=cv2.INTER_AREA)

def centre(dst, layer, dy=0):
    over(dst, layer, (dst.shape[1] - layer.shape[1]) // 2, (dst.shape[0] - layer.shape[0]) // 2 + dy)

def icon_monogram(n=1024):
    """Chrome gothic CA over a red brush slash on deep violet."""
    out = rgba(vgrad(n, n, [(0, (0.2, 0.08, 0.3)), (1, (0.05, 0.02, 0.08))]), rsq(n))
    centre(out, fit(rot(brush(int(n * 1.2), int(n * 0.36), 5), 32), n, 1.05))
    centre(out, fit(lettering("CA", G, int(n * 0.5), metal="silver", inline="gold", caps_font="CASTELAR.TTF"), n, 0.82))
    out[..., 3] = rsq(n); return out

def icon_seal(n=1024):
    """The red 混沌 hanko on cream paper."""
    out = rgba(bg_paper(n, n), rsq(n))
    centre(out, rot(seal(int(n * 0.5), int(n * 0.82), tuple(KANJI)), -4))
    return out

def icon_star(n=1024):
    """Eight-arrow chaos star in silver, glowing on a violet disc with a gold rim."""
    out = rgba(vgrad(n, n, [(0, (0.08, 0.06, 0.1)), (1, (0.02, 0.02, 0.03))]), rsq(n))
    c = n / 2; r = radial(n, n) * n
    disk = np.clip(n * 0.44 - r, 0, 1); core = np.clip(n * 0.38 - r, 0, 1)
    rgb = out[..., :3]
    rgb[:] = rgb * (1 - disk[..., None]) + chisel(disk, n * 0.03, "gold") * disk[..., None]
    rgb[:] = rgb * (1 - core[..., None]) + ramp(1 - r / (n * 0.38), [(0, (0.12, 0.03, 0.2)), (1, (0.55, 0.22, 0.8))]) * core[..., None]
    star = np.zeros((n, n), np.float32)
    for i in range(8):
        a = np.radians(i * 45 - 90); L = n * (0.3 if i % 2 == 0 else 0.24)
        ex, ey = c + np.cos(a) * L, c + np.sin(a) * L
        cv2.line(star, (int(c), int(c)), (int(ex), int(ey)), 1.0, int(n * 0.03), cv2.LINE_AA)
        hw, hl = n * 0.05, n * 0.07; px_, py_ = -np.sin(a), np.cos(a)
        cv2.fillPoly(star, [np.array([(ex + np.cos(a) * hl, ey + np.sin(a) * hl), (ex + px_ * hw, ey + py_ * hw), (ex - px_ * hw, ey - py_ * hw)], np.int32)], 1.0, cv2.LINE_AA)
    cv2.circle(star, (int(c), int(c)), int(n * 0.06), 1.0, -1, cv2.LINE_AA)
    rgb[:] = rgb + (cv2.GaussianBlur(star, (0, 0), n * 0.03) * 1.3 * core)[..., None] * np.array((0.6, 0.35, 1), np.float32)
    rgb[:] = rgb * (1 - star[..., None]) + chisel(star, n * 0.012, "silver") * star[..., None]
    out[..., :3] = np.clip(rgb, 0, 1); return out

def icon_enso(n=1024):
    """White brush ensō circle round a chrome gothic C, on charcoal."""
    out = rgba(vgrad(n, n, [(0, (0.12, 0.11, 0.13)), (1, (0.03, 0.03, 0.04))]), rsq(n))
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32); r = np.hypot(xx - n / 2, yy - n / 2)
    a = (np.degrees(np.arctan2(yy - n / 2, xx - n / 2)) + 360 - 110) % 360          # stroke starts lower left
    prog = a / 330
    th = n * 0.075 * np.clip(np.sin(np.pi * np.clip(prog, 0, 1)) ** 0.5, 0, 1) * (1.2 - 0.5 * prog)
    ring = (np.abs(r - n * 0.36) < th / 2) & (prog < 1)
    streak = cv2.resize(noise(64, 8, 106, 2), (n, n))
    m = cv2.GaussianBlur(ring.astype(np.float32), (0, 0), 1.2) * (0.75 + 0.25 * (streak > 0.3))
    over(out, rgba(col((0.94, 0.91, 0.84), (n, n)), m), 0, 0)
    centre(out, fit(lettering("C", "CASTELAR.TTF", int(n * 0.5), metal="silver", inline="gold"), n, 0.42))
    out[..., 3] = rsq(n); return out

def icon_sheet(names):
    """Each icon at 256, 64 and 32 px on dark and light, to judge readability small."""
    rows = []
    for nm in names:
        ic = load_png(nm); row = []
        for bgc in ((0.08, 0.08, 0.1), (0.9, 0.9, 0.92)):
            for s in (256, 64, 32):
                t = cv2.resize(ic, (s, s), interpolation=cv2.INTER_AREA); cell = col(bgc, (272, s + 16))
                cell[8:8 + s, 8:8 + s] = cell[8:8 + s, 8:8 + s] * (1 - t[..., 3:]) + t[..., :3] * t[..., 3:]
                row.append(cell)
        rows.append(np.concatenate(row, 1))
    save("_icons", np.concatenate(rows, 0))

T = sys.argv[3:] or ["D", "L", "I"]
INKLOGO = lambda: logo_variant(kanji=(0.07, 0.06, 0.07))
DARK = {"D1-white-ink": (0.9, 0.87, 0.8), "D2-red-ink": None, "D3-grey-ink": (0.34, 0.32, 0.37), "D4-violet-ink": VIOLET}
if "D" in T:
    for nm, k in DARK.items(): compose(nm, s_chrome_gothic(), 0.78, 0.12, logo=logo_variant(kanji=k))
    compose("D0-black-ink", s_chrome_gothic(), 0.78, 0.12, logo=INKLOGO())   # the light-background version
if "L" in T:
    H, W = 1080, 1920
    save("L1-splash-dark", place(bg_dark(H, W), load_png("D1-white-ink"), 0.62))
    save("L2-splash-inkwash", place(bg_inkwash(H, W), load_png("D1-white-ink"), 0.62))
    save("L3-splash-paper", place(bg_paper(H, W), load_png("D0-black-ink"), 0.62))
    save("L4-splash-vortex", place(bg_vortex(H, W), load_png("D4-violet-ink"), 0.62))
if "I" in T:
    for nm, fn in (("I1-icon-monogram", icon_monogram), ("I2-icon-seal", icon_seal), ("I3-icon-star", icon_star), ("I4-icon-enso", icon_enso)):
        save(nm, fn())
    icon_sheet(["I1-icon-monogram", "I2-icon-seal", "I3-icon-star", "I4-icon-enso"])
# ---------------------------------------------------------------- loading transitions (L1 dark <-> L3 paper) and icons v2

def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)

ease = lambda t: 1 - (1 - np.clip(t, 0, 1)) ** 3

def tr_ink(A, B, seed=1):
    """Ink bleeds outward from the middle, wet dark fringe on the front."""
    h, w = A.shape[:2]; f = 0.65 * noise(h, w, seed, 48) + 0.35 * np.clip(radial(h, w) * 1.6, 0, 1)
    def frame(t):
        thr = t * 1.25 - 0.1; m = smooth(thr + 0.015, thr - 0.015, f)[..., None]
        fringe = np.exp(-((f - thr) / 0.018) ** 2)[..., None]
        return (A * (1 - m) + B * m) * (1 - 0.55 * fringe)
    return frame

def tr_brush(A, B, seed=2):
    """One giant dry brush sweeps diagonally across, bristle streaks on its edge."""
    h, w = A.shape[:2]; yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    u = (xx + 0.35 * yy) / (w + 0.35 * h); s = cv2.resize(noise(h, 8, seed, 2), (w, h))
    def frame(t):
        edge = t * 1.3 - 0.15 + (s - 0.5) * 0.12; m = smooth(edge + 0.01, edge - 0.01, u)[..., None]
        wet = np.exp(-((u - edge) / 0.012) ** 2)[..., None]
        return (A * (1 - m) + B * m) * (1 - 0.4 * wet)
    return frame

def tr_burn(A, B, seed=3):
    """The top screen burns away: charred edge, glowing ember line."""
    h, w = A.shape[:2]; f = noise(h, w, seed, 30)
    def frame(t):
        thr = t * 1.15 - 0.05; m = smooth(thr + 0.004, thr - 0.004, f)[..., None]
        char = (smooth(thr + 0.07, thr + 0.01, f)[..., None]) * (1 - m)
        ember = np.exp(-((f - thr - 0.008) / 0.01) ** 2)[..., None]
        return A * (1 - m) * (1 - 0.75 * char) + B * m + ember * np.array((1, 0.45, 0.1), np.float32) * 1.2
    return frame

def tr_stamp(A, B, seed=4):
    """A red hanko slams down; the new screen bursts out from under it."""
    h, w = A.shape[:2]; r = radial(h, w) * np.hypot(w, h) * 0.7; fn = (noise(h, w, seed, 10) - 0.5) * h * 0.08
    sl = seal(int(h * 0.3), int(h * 0.5), tuple(KANJI))
    def frame(t):
        out = A.copy()
        if t >= 0.35:
            R = ease((t - 0.35) / 0.55) * np.hypot(w, h) * 0.6
            m = smooth(R + 6, R - 6, r + fn)[..., None]; out = A * (1 - m) + B * m
        a = np.clip(t / 0.35, 0, 1) * (1 - np.clip((t - 0.5) / 0.35, 0, 1))
        sc = 1.8 - 0.8 * ease(t / 0.35)
        s_ = cv2.resize(sl, (int(sl.shape[1] * sc), int(sl.shape[0] * sc))); s_[..., 3] *= a
        o = rgba(out, np.ones((h, w), np.float32)); centre(o, s_); out = o[..., :3]
        if 0.35 <= t < 0.45: out = np.roll(out, int(6 * np.sin(t * 200)), 1)    # impact shake
        return out
    return frame

def gif(name, A, B, maker, size=(640, 360), hold=12, steps=30):
    """Loop A -> B -> A, the transition played forward then with the screens swapped."""
    A, B = [cv2.resize(x, size, interpolation=cv2.INTER_AREA) for x in (A, B)]
    ab, ba = maker(A, B), maker(B, A)
    frames = [A] * hold + [ab(t) for t in np.linspace(0, 1, steps)] + [B] * hold + [ba(t) for t in np.linspace(0, 1, steps)]
    ims = [Image.fromarray((np.clip(f, 0, 1) * 255).astype(np.uint8)) for f in frames]
    ims[0].save(os.path.join(OUT, name + ".gif"), save_all=True, append_images=ims[1:], duration=42, loop=0)

# --- icons v2: from the D1 look (dark, cream ink, chrome, red seal)

CREAM = (0.9, 0.87, 0.8)

def dark_tile(n):
    return rgba(ramp(np.clip(1 - radial(n, n) * 1.6, 0, 1), [(0, (0.03, 0.03, 0.04)), (1, (0.16, 0.14, 0.18))]), rsq(n))

def splash_ink(n, R, c, seed):
    """Cream ink splat with droplets."""
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32); r = np.hypot(xx - n / 2, yy - n / 2) / n
    m = smooth(R + 0.01, R - 0.01, r + (noise(n, n, seed, 60) - 0.5) * 0.3)
    rg = np.random.default_rng(seed)
    for _ in range(14):
        a, d = rg.uniform(0, 2 * np.pi), rg.uniform(R * 1.05, R * 1.5) * n
        cv2.circle(m, (int(n / 2 + np.cos(a) * d), int(n / 2 + np.sin(a) * d)), int(rg.uniform(0.006, 0.022) * n), 1.0, -1, cv2.LINE_AA)
    return rgba(col(c, (n, n)), m)

def brush_ring(n, R, thick, c, seed, start=110, sweep=330):
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32); r = np.hypot(xx - n / 2, yy - n / 2)
    prog = ((np.degrees(np.arctan2(yy - n / 2, xx - n / 2)) + 360 - start) % 360) / sweep
    th = thick * np.sin(np.pi * np.clip(prog, 0.001, 0.999)) ** 0.5 * (1.2 - 0.5 * prog)
    m = cv2.GaussianBlur(((np.abs(r - R) < th / 2) & (prog < 1)).astype(np.float32), (0, 0), 1.2)
    return rgba(col(c, (n, n)), m * (0.75 + 0.25 * (cv2.resize(noise(64, 8, seed, 2), (n, n)) > 0.3)))

def diamond(s):
    """Chiselled silver diamond with gold inline and black outline, like the logo's dots."""
    m = np.zeros((s, s), np.float32)
    cv2.fillPoly(m, [np.array([(s // 2, 0), (s - 1, s // 2), (s // 2, s - 1), (0, s // 2)], np.int32)], 1.0, cv2.LINE_AA)
    ro = int(s * 0.1); m = np.pad(m, ro + 2)
    ring = np.clip(dilate(m, max(int(s * 0.035), 2)) - m, 0, 1)
    rgb = col((0.06, 0.04, 0.05), m.shape)
    rgb = rgb * (1 - ring[..., None]) + ramp(np.full(m.shape, 0.7, np.float32), METALS["gold"]) * ring[..., None]
    rgb = rgb * (1 - m[..., None]) + chisel(m, s, "silver") * m[..., None]
    return rgba(rgb, dilate(m, ro))

def icon_seal_splash(n=1024):
    out = dark_tile(n); over(out, splash_ink(n, 0.3, CREAM, 7), 0, 0)
    centre(out, rot(seal(int(n * 0.36), int(n * 0.6), tuple(KANJI)), -5)); out[..., 3] = rsq(n); return out

def icon_round_seal(n=1024):
    out = dark_tile(n); yy, xx = np.mgrid[0:n, 0:n].astype(np.float32); r = np.hypot(xx - n / 2, yy - n / 2) / n
    disc = smooth(0.37, 0.355, r + (noise(n, n, 8, 30) - 0.5) * 0.03)
    k = text_mask("混", "YuGothB.ttc", int(n * 0.5)); km = np.zeros((n, n), np.float32); paste_max(km, fit(k, n, 0.42), n / 2, n / 2)
    m = np.clip(disc - km, 0, 1) * (0.82 + 0.18 * noise(n, n, 9, 4))
    over(out, brush_ring(n, n * 0.43, n * 0.05, CREAM, 10, start=200, sweep=300), 0, 0)
    over(out, rgba(col(tuple(KANJI), (n, n)), m), 0, 0); out[..., 3] = rsq(n); return out

def icon_brush_c(n=1024):
    out = dark_tile(n)
    c = fit(dilate(text_mask("C", BRUSH, int(n * 0.9)), 6), n, 0.7)
    cm = np.zeros((n, n), np.float32); paste_max(cm, c, n * 0.47, n * 0.5)
    over(out, rgba(col(CREAM, (n, n)), cm * (0.85 + 0.15 * (noise(n, n, 11, 3) > 0.3))), 0, 0)
    over(out, rot(seal(int(n * 0.16), int(n * 0.28), tuple(KANJI)), -6), int(n * 0.68), int(n * 0.62)); out[..., 3] = rsq(n); return out

def icon_diamond(n=1024):
    out = dark_tile(n); over(out, brush_ring(n, n * 0.36, n * 0.08, CREAM, 12), 0, 0)
    centre(out, diamond(int(n * 0.42))); out[..., 3] = rsq(n); return out

def icon_logo_crop(n, box):
    """A square corner of the D1 logo itself, bled off the tile edges."""
    lg = logo_variant(kanji=CREAM); x0, y0, x1, y1 = box
    crop = lg[max(y0, 0):y1, x0:x1]
    out = dark_tile(n); centre(out, fit(crop, n, 1.08)); out[..., 3] = rsq(n); return out

def icon_sheet2(names):
    rows = []
    for nm in names:
        ic = load_png(nm); row = []
        for bgc in ((0.08, 0.08, 0.1), (0.9, 0.9, 0.92)):
            for s in (256, 64, 32):
                t = cv2.resize(ic, (s, s), interpolation=cv2.INTER_AREA); cell = col(bgc, (272, s + 16))
                cell[8:8 + s, 8:8 + s] = cell[8:8 + s, 8:8 + s] * (1 - t[..., 3:]) + t[..., :3] * t[..., 3:]
                row.append(cell)
        rows.append(np.concatenate(row, 1))
    save("_icons2", np.concatenate(rows, 0))

if "A" in T:
    L1, L3 = load_png("L1-splash-dark")[..., :3], load_png("L3-splash-paper")[..., :3]
    for nm, mk in (("T1-ink-bleed", tr_ink), ("T2-brush-wipe", tr_brush), ("T3-burn", tr_burn), ("T4-stamp", tr_stamp)):
        gif(nm, L1, L3, mk)
if "J" in T:
    icons = (("J1-seal-splash", icon_seal_splash), ("J2-round-seal", icon_round_seal), ("J3-brush-c", icon_brush_c),
             ("J4-diamond", icon_diamond), ("J5-logo-yu", lambda: icon_logo_crop(1024, (229, 0, 1419, 1190))),
             ("J6-logo-oh", lambda: icon_logo_crop(1024, (2391, 146, 3351, 1106))))
    for nm, fn in icons: save(nm, fn())
    icon_sheet2([nm for nm, _ in icons])
if "F" in T:   # the ink-bleed field the in-game loading shader (ui/Loading.cs) thresholds, same recipe as tr_ink
    h, w = 540, 960
    f = 0.65 * noise(h, w, 1, 72) + 0.35 * np.clip(radial(h, w) * 1.6, 0, 1)
    Image.fromarray((np.clip(f, 0, 1) * 255).astype(np.uint8), "L").save(os.path.join(OUT, "ink_field.png"))
if "M" in T:   # main-menu textures: brush highlight stroke, plain paper, ink splat
    save("brush_ink", rgba(col(INK, (140, 1000)), ink_stroke(1000, 140, 110, seed=97)))
    save("paper_bg", bg_paper(1080, 1920))
    save("ink_splash", splash_ink(900, 0.3, INK, 98))
print("ok")
