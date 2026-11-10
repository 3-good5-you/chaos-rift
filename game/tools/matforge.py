"""Mat Forge: playmats for the duel table, in the same style families as the card backs (backforge.py),
but composed for the table: 180-degree symmetric (each player sees their half upright), calm under the zones
(recessed inlays), the style's detail in the border band, the centre divider, the corners and the gaps between
zones, and one faint centrepiece behind each half. Output: 2048x1374 JPGs (the table is 7.3 x 4.9 units).
Usage: python matforge.py <art_dir> <out_dir> [names]"""
import sys, os, math
import numpy as np
import cv2

ART, OUT = sys.argv[1], sys.argv[2]
ONLY = set(sys.argv[3:])
os.makedirs(OUT, exist_ok=True)

W, H = 2048, 1374
U = W / 7.3                                   # pixels per table unit
CX, CY = W / 2, H / 2
yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)

# ---------------------------------------------------------------- basics

def rng(seed): return np.random.default_rng(seed)

def fbm(seed, base=4, octaves=6, aspect=(1, 1)):
    r = rng(seed); out = np.zeros((H, W), np.float32); amp, tot = 1.0, 0.0
    for o in range(octaves):
        gh, gw = max(2, int(base * aspect[0] * 2 ** o)), max(2, int(base * aspect[1] * 2 ** o) + 1)
        out += cv2.resize(r.random((gh, gw)).astype(np.float32), (W, H), interpolation=cv2.INTER_CUBIC) * amp
        tot += amp; amp *= 0.5
    out /= tot
    return (out - out.min()) / (out.max() - out.min() + 1e-6)

def hexc(s): s = s.lstrip('#'); return np.array([int(s[i:i + 2], 16) / 255 for i in (0, 2, 4)], np.float32)

def ramp(t, stops):
    t = np.clip(t, 0, 1)
    out = np.zeros(t.shape + (3,), np.float32)
    for (a, ca), (b, cb) in zip(stops, stops[1:]):
        k = np.clip((t - a) / max(b - a, 1e-6), 0, 1)[..., None]
        sel = ((t >= a) & (t <= b))[..., None]
        out = np.where(sel, hexc(ca) * (1 - k) + hexc(cb) * k, out)
    return out

def glow(m, s): return cv2.GaussianBlur(m.astype(np.float32), (0, 0), s)
def add(img, m, col, k=1.0): return img + np.clip(m, 0, None)[..., None] * (hexc(col) if isinstance(col, str) else np.array(col, np.float32)) * k
def over(img, m, col): m = np.clip(m, 0, 1)[..., None]; return img * (1 - m) + (hexc(col) if isinstance(col, str) else np.array(col, np.float32)) * m

def rbox_dist(cx, cy, hw, hh, r):
    """Signed distance (px) to a rounded box: < 0 inside."""
    qx, qy = np.abs(xx - cx) - hw + r, np.abs(yy - cy) - hh + r
    return np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - r

def sym(img):
    """180-degree symmetry: the rival half is your half turned round."""
    out = img.copy()
    out[:H // 2] = img[::-1, ::-1][:H // 2]
    return out

def symm(m):
    out = m.copy(); out[:H // 2] = m[::-1, ::-1][:H // 2]; return out

# zones (your side; the rival side is the 180-degree turn) in pixels
ZW, ZH = 0.72 * U, 0.99 * U
ZONES = [(CX + gx * 0.9 * U, CY + gz * U) for gz in (0.62, 1.62) for gx in range(-3, 4)]
ZONES += [(2 * CX - x, 2 * CY - y) for x, y in ZONES]
zone_d = np.min(np.stack([rbox_dist(x, y, ZW / 2, ZH / 2, 14) for x, y in ZONES]), axis=0)
ZMASK = np.clip(-zone_d / 2.0 + 0.5, 0, 1)                    # 1 inside a zone
NEAR_ZONE = np.clip(1 - np.clip(zone_d, 0, None) / 40, 0, 1)  # 1 at a zone, 0 further than 40 px away
CORNER = 0.47 * U                                             # rounded mat corners (DuelField cuts the mesh the same way)
EDGE_D = -rbox_dist(CX, CY, W / 2, H / 2, CORNER)             # px from the mat edge
BORDER = np.clip((80 - EDGE_D) / 2, 0, 1)                     # the outer band (~0.28 units)
DIV_D = np.abs(yy - CY)                                       # px from the centre line
FIELD = (1 - BORDER) * (1 - ZMASK)                            # where decoration may show
# centrepiece behind each half: between the two rows, at the board centre column
PIECE = (CX, CY + 1.12 * U)
pr = np.hypot(xx - PIECE[0], yy - PIECE[1])
pth = np.arctan2(yy - PIECE[1], xx - PIECE[0])

METAL = {
    "gold": ['5a3a0e', 'c98f2a', 'fff0b0', 'd7a23c', '6b4510'], "silver": ['4a5058', 'aeb6c0', 'ffffff', 'b9c1cb', '50565e'],
    "bronze": ['3a1e0e', '9a5a2a', 'ffd2a0', 'a8642e', '40220e'], "violet": ['2a0f40', '8a4ad0', 'f2dcff', '9a5ae0', '2e1048'],
    "crimson": ['3a0606', 'c0302a', 'ffd0b0', 'd04030', '400808'], "azure": ['061a3a', '2a7ad0', 'd8f2ff', '3a8ae0', '08203e'],
    "jade": ['06301a', '2aa060', 'd8ffe8', '3ab070', '083a20'], "obsidian": ['050405', '2a2228', '8a7a80', '30282e', '060506'],
}

def metal(name, t):
    c = METAL[name]
    return ramp(t, [(0, c[0]), (0.35, c[1]), (0.5, c[2]), (0.62, c[3]), (1, c[4])])

# ---------------------------------------------------------------- shared finishing

def finish(img, metal_name, accent, calm=0.55):
    """Inlays under the zones, the border rim, the centre seam glow; then save-ready."""
    # calm the zones: darker, softer, so the cards and the zone frames read
    soft = cv2.GaussianBlur(img, (0, 0), 6)
    z = ZMASK[..., None]
    img = img * (1 - z) + (soft * calm) * z
    # recessed inlay: shadow on the upper-left inner edge, a faint accent line on the lower edge
    inner = np.clip(ZMASK - glow(ZMASK, 5) * 0.0, 0, 1)
    shade = glow(np.clip(np.roll(ZMASK, 6, axis=0) - ZMASK, 0, 1), 3) + glow(np.clip(np.roll(ZMASK, 6, axis=1) - ZMASK, 0, 1), 3)
    img = img * (1 - np.clip(shade, 0, 0.6)[..., None])
    lip = np.clip(ZMASK - np.roll(ZMASK, -3, axis=0), 0, 1)
    img = add(img, glow(symm(lip), 1.2), accent, 0.35)
    # border rim: metal band with a bevel, the inner edge glowing in the accent
    t = 0.5 + 0.5 * np.sin((xx / W * 0.7 + yy / H * 1.3) * math.pi * 3) * 0.8 + fbm(901, base=2, octaves=4, aspect=(1, 30)) * 0.15
    rim = metal(metal_name, np.clip(t, 0, 1))
    band = np.clip((40 - EDGE_D) / 2, 0, 1)                   # solid metal: outer 40 px
    img = img * (1 - band[..., None]) + rim * band[..., None]
    bev = np.exp(-((EDGE_D - 40) ** 2) / 6)
    img = add(img, bev + glow(bev, 10) * 0.8, accent, 0.7)
    img = img * (1 - np.clip((90 - EDGE_D) / 50, 0, 1)[..., None] * 0.25 * (1 - band[..., None]))   # shade inside the rim
    return np.clip(img, 0, 1)

def save(name, img):
    cv2.imwrite(os.path.join(OUT, name + ".jpg"), (img[:, :, ::-1] * 255).astype(np.uint8), [cv2.IMWRITE_JPEG_QUALITY, 92])

def corners(img, accent, metal_name, kind="bracket"):
    """Corner ornaments in the free corners of the border."""
    m = np.zeros((H, W), np.float32)
    R = int(CORNER)
    for sx, sy in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
        ox, oy = (R if sx > 0 else W - 1 - R), (R if sy > 0 else H - 1 - R)       # centre of the corner's curve
        if kind == "bracket":
            # arc following the rounded corner, legs running on along the edges
            a0 = {(1, 1): 180, (-1, 1): 270, (1, -1): 90, (-1, -1): 0}[(sx, sy)]
            cv2.ellipse(m, (ox, oy), (R - 60, R - 60), 0, a0, a0 + 90, 1.0, 6, cv2.LINE_AA)
            cv2.line(m, (ox - sx * (R - 60), oy), (ox - sx * (R - 60), oy + sy * 80), 1.0, 6, cv2.LINE_AA)
            cv2.line(m, (ox, oy - sy * (R - 60)), (ox + sx * 80, oy - sy * (R - 60)), 1.0, 6, cv2.LINE_AA)
            d = int((R - 60) * 0.42)
            cv2.circle(m, (ox - sx * d, oy - sy * d), 9, 1.0, -1, cv2.LINE_AA)
        else:
            d = int(R * 0.33)
            cv2.circle(m, (ox - sx * d, oy - sy * d), 26, 1.0, 4, cv2.LINE_AA)
            cv2.circle(m, (ox - sx * d, oy - sy * d), 10, 1.0, -1, cv2.LINE_AA)
    return add(img, m + glow(m, 6) * 0.8, accent, 0.9)

def sparks(seed, n, area, size=(1, 3)):
    r = rng(seed); m = np.zeros((H, W), np.float32)
    ys, xs = np.nonzero(area > 0.5)
    for i in r.integers(0, len(xs), n):
        cv2.circle(m, (int(xs[i]), int(ys[i])), int(r.uniform(*size)), 1.0, -1, cv2.LINE_AA)
    return m

# ---------------------------------------------------------------- styles

def magma():
    rock = fbm(1, base=5) * 0.6 + fbm(2, base=40, octaves=3) * 0.4
    img = ramp(rock, [(0, '0a0606'), (0.6, '241614'), (1, '3e2a24')])
    v = fbm(3, base=7)
    crack = np.exp(-(np.abs(v - 0.5) * 70) ** 2)
    heat = np.clip(1 - EDGE_D / 380, 0, 1) ** 1.5 + np.exp(-DIV_D / 90) * 0.9         # hotter at the rim and the river
    img = add(img, crack * heat * 1.4 + glow(crack * heat, 6) * 0.9, 'ff6a14')
    # lava river along the centre line
    flow = fbm(4, base=3, aspect=(1, 6))
    river = np.clip(1 - DIV_D / (34 + flow * 26), 0, 1)
    lava = ramp(flow * 0.6 + river * 0.5, [(0, '5a0a02'), (0.4, 'c8300a'), (0.75, 'ff8a1a'), (1, 'ffe08a')])
    img = img * (1 - river[..., None]) + lava * river[..., None]
    img = add(img, glow(river, 30), 'ff4a10', 0.6)
    # centrepiece: a cooled-lava sigil ring
    ring = np.exp(-((pr - 1.25 * U) ** 2) / 40) + np.exp(-((pr - 1.12 * U) ** 2) / 12) * 0.6
    img = add(img, symm(ring) * FIELD, 'ff7a20', 0.35)
    img = add(img, sparks(5, 160, FIELD * (EDGE_D < 300)) * 1.2, 'ffb040')
    img = sym(img)
    img = corners(img, hexc('ff6a14'), "obsidian")
    return finish(img, "obsidian", hexc('ff6a14'))

def tide():
    img = ramp(np.clip(DIV_D / (H / 2), 0, 1) * 0.7 + fbm(11, base=3) * 0.3, [(0, '0a4a88'), (0.6, '05244a'), (1, '020c1e')])
    p = np.zeros((H, W), np.float32); warp = fbm(12, base=3) * 50
    for k, (fx, fy) in enumerate(((0.017, 0.011), (-0.013, 0.016), (0.009, -0.019), (0.021, 0.006))):
        p += np.sin(xx * fx + yy * fy + warp * (0.3 + k * 0.1))
    caust = np.exp(-(np.abs(p) * 3) ** 2)
    img = add(img, caust * (0.18 + 0.4 * (1 - NEAR_ZONE)), '8ae0ff')
    # wave crest along the centre: foam line
    crest = CY + np.sin(xx / 95) * 10 + np.sin(xx / 37 + 1) * 4
    foam = np.exp(-((yy - crest) ** 2) / 30) * (0.6 + 0.6 * fbm(13, base=20, octaves=3))
    img = add(img, foam + glow(foam, 12) * 0.6, 'e8fbff', 0.9)
    # centrepiece: ripple rings
    ripples = np.exp(-((np.sin(pr / 34 - 1.0) - 0.97) ** 2) * 600) * np.clip(1 - pr / (1.6 * U), 0, 1)
    img = add(img, symm(ripples) * FIELD, '9ae6ff', 0.35)
    bub = np.zeros((H, W), np.float32); r = rng(14)
    ys, xs = np.nonzero((FIELD > 0.5) & (EDGE_D < 260))
    for i in r.integers(0, len(xs), 70): cv2.circle(bub, (int(xs[i]), int(ys[i])), int(r.uniform(3, 11)), 1.0, 1, cv2.LINE_AA)
    img = add(img, bub, 'd8f6ff', 0.6)
    img = sym(img)
    img = corners(img, hexc('7ad8ff'), "azure", "ring")
    return finish(img, "azure", hexc('7ad8ff'))

def storm():
    img = ramp(fbm(21, base=3) * 0.8, [(0, '05020a'), (0.6, '1e0a34'), (1, '3a1660')])
    r = rng(22); m = np.zeros((H, W), np.float32)
    def bolt(p0, p1, d, w):
        if d == 0: cv2.line(m, tuple(map(int, p0)), tuple(map(int, p1)), 1.0, w, cv2.LINE_AA); return
        mid = (p0 + p1) / 2 + r.normal(0, np.linalg.norm(p1 - p0) * 0.12, 2) * np.array([0.4, 1.0])
        bolt(p0, mid, d - 1, w); bolt(mid, p1, d - 1, w)
        if r.random() < 0.25 and d > 3: bolt(mid, mid + r.normal(0, 90, 2), d - 3, max(1, w - 1))
    for _ in range(3):   # arcs running along the divider
        bolt(np.array([80, CY + r.uniform(-12, 12)], np.float32), np.array([W - 80, CY + r.uniform(-12, 12)], np.float32), 8, 2)
    for _ in range(6):   # small arcs crawling along the border
        x = r.uniform(120, W - 120)
        bolt(np.array([x, H - 70], np.float32), np.array([x + r.uniform(-200, 200), H - 70 - r.uniform(30, 90)], np.float32), 5, 1)
    img = add(img, m * 1.2 + glow(m, 4) * 1.3 + glow(m, 22) * 0.6, 'd070ff')
    # centrepiece: a ring of runes
    runes = np.zeros((H, W), np.float32)
    for k in range(24):
        a = k / 24 * math.tau; R = 1.3 * U
        x, y = PIECE[0] + math.cos(a) * R, PIECE[1] + math.sin(a) * R * 0.95
        g = r.integers(0, 3)
        if g == 0: cv2.line(runes, (int(x - 8), int(y - 10)), (int(x + 8), int(y + 10)), 1.0, 2, cv2.LINE_AA)
        elif g == 1: cv2.circle(runes, (int(x), int(y)), 7, 1.0, 2, cv2.LINE_AA)
        else: cv2.polylines(runes, [np.array([[x - 8, y + 8], [x, y - 9], [x + 8, y + 8]], np.int32)], True, 1.0, 2, cv2.LINE_AA)
    runes += np.exp(-((pr - 1.3 * U - 26) ** 2) / 6) + np.exp(-((pr - 1.3 * U + 26) ** 2) / 6)
    img = add(img, symm(runes) * FIELD + glow(symm(runes) * FIELD, 6) * 0.5, 'b060ff', 0.45)
    img = sym(img)
    img = corners(img, hexc('d070ff'), "violet")
    return finish(img, "violet", hexc('d070ff'))

def radiant():
    marble = fbm(31, base=3); vein = np.exp(-(np.abs(fbm(32, base=5) - 0.5) * 50) ** 2)
    img = ramp(marble * 0.5 + 0.35, [(0, '6a5030'), (0.5, 'b8955a'), (1, 'e8d2a0')]) * 0.7
    img = add(img, vein, 'fff0c0', 0.25)
    ang = (pth + math.pi) / math.tau
    rays = cv2.resize(rng(33).random((1, 64)).astype(np.float32), (2048, 1), interpolation=cv2.INTER_CUBIC)[0]
    ray = np.clip(rays, 0, 1)[np.clip(ang * 2047, 0, 2047).astype(int)] ** 3
    sun = ray * np.clip(1 - pr / (2.6 * U), 0, 1) * 0.6 + np.exp(-pr / (0.6 * U)) * 0.5
    img = add(img, symm(sun) * (1 - BORDER), 'fff4c8', 0.7)
    beam = np.exp(-DIV_D ** 2 / 60) + glow(np.exp(-DIV_D ** 2 / 60), 18) * 0.8
    img = add(img, beam, 'fffbe8', 0.9)
    img = add(img, sparks(34, 90, FIELD * (EDGE_D < 280), (1, 3)), 'fff6d0', 1.0)
    img = sym(img)
    img = corners(img, hexc('ffe08a'), "gold", "ring")
    return finish(img, "gold", hexc('ffd870'), calm=0.65)

def ruins():
    stone = fbm(41, base=4) * 0.6 + fbm(42, base=60, octaves=2) * 0.4
    img = ramp(stone, [(0, '1e1610'), (0.5, '4a3a2c'), (1, '7a6650')])
    # slabs: seams run through the gaps between the zones
    xs_ = [CX + (g + 0.5) * 0.9 * U for g in range(-4, 4)]
    seam = np.zeros((H, W), np.float32)
    for x in xs_: seam = np.maximum(seam, np.exp(-((xx - x) ** 2) / 4))
    for y in (CY, CY + 1.12 * U, CY - 1.12 * U): seam = np.maximum(seam, np.exp(-((yy - y) ** 2) / 4))
    img = over(img, seam * 0.8, '0e0a06')
    img = add(img, glow(np.roll(seam, 2, axis=0), 1) * 0.25, 'a08a6a')
    # carved circle and moss in the margins
    carve = np.exp(-((pr - 1.25 * U) ** 2) / 20) + np.exp(-((pr - 1.05 * U) ** 2) / 8)
    img = over(img, symm(carve) * FIELD * 0.5, '0e0a06')
    moss = np.clip((fbm(43, base=8) - 0.55) * 4, 0, 1) * np.clip(1 - EDGE_D / 220, 0, 1)
    img = img * (1 - moss[..., None] * 0.6) + hexc('2e4a22') * moss[..., None] * 0.6
    img = sym(img)
    img = corners(img, hexc('ffb050'), "bronze")
    return finish(img, "bronze", hexc('e0a050'))

def skyway():
    img = ramp(fbm(51, base=2) * 0.4 + np.clip(DIV_D / (H / 2), 0, 1) * 0.6, [(0, '3ac8a0'), (0.5, '0e5a4a'), (1, '04201c')])
    streams = np.zeros((H, W), np.float32)
    wob = fbm(52, base=3) * 60
    for k in range(18):
        y0 = CY + 30 + k * 34
        stream = np.exp(-((yy - y0 - np.sin(xx / (160 + k * 9) + k) * 22 - wob) ** 2) / 3)
        streams += stream * (0.5 + 0.5 * np.sin(xx / 70 + k * 2.1)) ** 4
    streams *= np.clip(1 - NEAR_ZONE, 0, 1)
    img = add(img, streams, 'd8fff0', 0.5)
    swirl = np.exp(-((np.sin(pth * 3 - pr / 70) - 0.98) ** 2) * 500) * np.clip(1 - pr / (1.7 * U), 0, 1)
    img = add(img, symm(swirl) * FIELD, 'b8ffd8', 0.4)
    current = np.exp(-DIV_D ** 2 / 200) * (0.6 + 0.4 * np.sin(xx / 30))
    img = add(img, current + glow(current, 10) * 0.5, 'e8fff4', 0.5)
    img = sym(img)
    img = corners(img, hexc('8affb0'), "jade", "ring")
    return finish(img, "jade", hexc('8affb0'))

def pharaoh():
    sand = fbm(61, base=4) * 0.6 + fbm(62, base=80, octaves=2) * 0.4
    img = ramp(sand, [(0, '3a2a14'), (0.5, '6e5428'), (1, '9a7a40')]) * 0.85
    # glyph frieze: a band inside the border and along the divider
    r = rng(63); g = np.zeros((H, W), np.float32)
    def frieze(y0, y1):
        for x in range(110, W - 110, 40):
            for _ in range(r.integers(1, 3)):
                k = r.integers(0, 5); x0, ym = x + r.integers(6, 30), (y0 + y1) // 2 + r.integers(-8, 8)
                if k == 0: cv2.circle(g, (int(x0), int(ym)), int(r.integers(5, 9)), 1.0, 2, cv2.LINE_AA)
                elif k == 1: cv2.line(g, (int(x0), int(ym - 10)), (int(x0), int(ym + 10)), 1.0, 2, cv2.LINE_AA)
                elif k == 2: cv2.ellipse(g, (int(x0), int(ym)), (11, 6), 0, 0, 180, 1.0, 2, cv2.LINE_AA)
                elif k == 3: cv2.polylines(g, [np.array([[x0 - 8, ym + 8], [x0, ym - 8], [x0 + 8, ym + 8]], np.int32)], False, 1.0, 2, cv2.LINE_AA)
                else: cv2.line(g, (int(x0 - 9), int(ym)), (int(x0 + 9), int(ym)), 1.0, 2, cv2.LINE_AA)
        cv2.line(g, (100, y0), (W - 100, y0), 1.0, 2, cv2.LINE_AA); cv2.line(g, (100, y1), (W - 100, y1), 1.0, 2, cv2.LINE_AA)
    frieze(H - 92, H - 52)
    img = over(img, g * 0.7, '2a1a08')
    img = add(img, glow(g, 2) * 0.3, 'ffcf70')
    # divider: a gold inlaid band
    inlay = np.clip(1 - DIV_D / 9, 0, 1)
    img = img * (1 - inlay[..., None]) + metal("gold", 0.3 + 0.4 * (np.sin(xx / 40) * 0.5 + 0.5)) * inlay[..., None]
    # centrepiece: puzzle pyramid inlay, faint, apex toward the divider
    tri = np.zeros((H, W), np.float32); s = 1.55 * U
    cx, base_y = PIECE[0], PIECE[1] + s * 0.45
    pts = np.array([[cx, base_y - s * 0.95], [cx - s * 0.6, base_y], [cx + s * 0.6, base_y]], np.int32)
    cv2.polylines(tri, [pts], True, 1.0, 5, cv2.LINE_AA)
    for k in (0.33, 0.66):
        y = int(base_y - s * 0.95 * (1 - k)); hw = s * 0.6 * k
        cv2.line(tri, (int(cx - hw), y), (int(cx + hw), y), 1.0, 3, cv2.LINE_AA)
    img = add(img, tri * FIELD + glow(tri * FIELD, 8) * 0.5, 'ffcf70', 0.5)
    img = sym(img)
    img = corners(img, hexc('ffcf70'), "gold", "ring")
    return finish(img, "gold", hexc('ffcf70'))

def datastream():
    img = ramp(fbm(71, base=2) * 0.5 + np.clip(DIV_D / (H / 2), 0, 1) * 0.5, [(0, '082a52'), (1, '020814')])
    # traces routed through the gaps between zones, nodes at zone corners
    r = rng(72); m = np.zeros((H, W), np.float32); dots = np.zeros((H, W), np.float32)
    gx = [CX + (g + 0.5) * 0.9 * U for g in range(-4, 4)]
    for x in gx:
        for side in (1,):
            y0, y1 = CY + 0.1 * U, H - 90
            cv2.line(m, (int(x), int(y0)), (int(x), int(y1)), 1.0, 2, cv2.LINE_AA)
            for _ in range(2):
                y = int(r.uniform(y0 + 40, y1 - 40)); d = r.choice([-1, 1])
                cv2.line(m, (int(x), y), (int(x + d * 30), y + 30), 1.0, 2, cv2.LINE_AA)
                cv2.circle(dots, (int(x + d * 30), y + 30), 5, 1.0, 2, cv2.LINE_AA)
    for y in (CY + 1.12 * U,):
        cv2.line(m, (90, int(y)), (W - 90, int(y)), 1.0, 2, cv2.LINE_AA)
    for x, y in ZONES[:14]:
        for sx in (-1, 1):
            for sy in (-1, 1): cv2.circle(dots, (int(x + sx * (ZW / 2 + 9)), int(y + sy * (ZH / 2 + 9))), 4, 1.0, -1, cv2.LINE_AA)
    bus = sum(np.exp(-((yy - CY - o) ** 2) / 2) for o in (-14, -7, 0, 7, 14))
    pulse = 0.5 + 0.5 * np.sin(xx / 22)
    light = (m + dots) * (1 - ZMASK)
    img = add(img, light * 0.9 + glow(light, 5) * 0.8, '3ae0ff')
    img = add(img, bus * pulse + glow(bus, 8) * 0.5, '7af0ff', 0.8)
    img = sym(img)
    img = corners(img, hexc('3ae0ff'), "silver")
    return finish(img, "silver", hexc('3ae0ff'))

def cup_arena():
    img = ramp(fbm(81, base=2) * 0.5, [(0, '08020f'), (1, '1e0836')])
    # neon hex floor
    s = 46.0
    q = (xx * math.sqrt(3) / 3 - yy / 3) / s; rr = yy * 2 / 3 / s
    cx_, cz_ = q, rr; cy_ = -cx_ - cz_
    rx, ry, rz = np.round(cx_), np.round(cy_), np.round(cz_)
    dx, dy, dz = np.abs(rx - cx_), np.abs(ry - cy_), np.abs(rz - cz_)
    edge = np.maximum(np.maximum(dx, dy), dz)
    hexl = np.exp(-((edge - 0.5) ** 2) * 900)
    img = add(img, hexl * (0.15 + 0.35 * (1 - NEAR_ZONE)), 'b05aff')
    ring = sum(np.exp(-((pr - R) ** 2) / w) for R, w in ((1.4 * U, 10), (1.5 * U, 4)))
    img = add(img, symm(ring) * FIELD + glow(symm(ring) * FIELD, 10) * 0.5, 'd08aff', 0.6)
    bar = np.exp(-DIV_D ** 2 / 20)
    img = add(img, bar + glow(bar, 14), 'e0a0ff', 0.9)
    img = sym(img)
    img = corners(img, hexc('c070ff'), "violet")
    return finish(img, "violet", hexc('c070ff'))

def twin_vortex():
    d = pr; a = pth
    sw = a * 2 + np.log(d + 10) * 4.5
    streak = 0.5 + 0.5 * np.sin(sw * 3 + fbm(91, base=3) * 5)
    t = np.clip(streak * 0.45 + np.clip(d / (2.2 * U), 0, 1) * 0.45, 0, 1)
    img = ramp(t, [(0, '050302'), (0.4, '2a1405'), (0.7, '7a3a10'), (1, 'c8782a')]) * 0.85
    img *= np.clip(d / (0.5 * U), 0.25, 1)[..., None]
    img = sym(img)
    seam = np.exp(-DIV_D ** 2 / 40) * (0.6 + 0.4 * np.sin(xx / 18))
    img = add(img, seam + glow(seam, 16) * 0.6, 'ffb050', 0.8)
    img = corners(img, hexc('ff9a30'), "bronze", "ring")
    return finish(img, "bronze", hexc('ff9a30'))

def steel():
    brush = fbm(101, base=2, octaves=5, aspect=(1, 40))
    img = ramp(0.35 + brush * 0.3, [(0, '2a2e34'), (0.5, '5a626c'), (1, '9aa4b0')])
    seams = np.zeros((H, W), np.float32)
    for g in range(-4, 4): seams = np.maximum(seams, np.exp(-((xx - (CX + (g + 0.5) * 0.9 * U)) ** 2) / 3))
    for y in (CY + 1.12 * U, CY - 1.12 * U): seams = np.maximum(seams, np.exp(-((yy - y) ** 2) / 3))
    img = over(img, seams * 0.7, '101317')
    img = add(img, np.roll(seams, 2, axis=1) * 0.3, 'c8d0d8')
    rivets = np.zeros((H, W), np.float32)
    for g in range(-4, 4):
        x = CX + (g + 0.5) * 0.9 * U
        for y in (CY + 1.12 * U, CY - 1.12 * U, 100, H - 100): cv2.circle(rivets, (int(x), int(y)), 6, 1.0, -1, cv2.LINE_AA)
    img = add(img, rivets, 'e8eef4', 0.6); img = over(img, glow(np.roll(rivets, 3, axis=0), 2) * 0.5, '0a0c0e')
    # hazard divider: gold and black chevrons
    stripe = ((np.floor((xx + (yy - CY) * 1.0) / 26) % 2) == 0).astype(np.float32)
    band = np.clip(1 - DIV_D / 12, 0, 1)
    img = img * (1 - band[..., None]) + np.where(stripe[..., None] > 0, hexc('d8a83a'), hexc('15130e')) * band[..., None]
    img = sym(img)
    img = corners(img, hexc('ffffff'), "silver")
    return finish(img, "silver", hexc('dfe8f0'), calm=0.7)

def starlane():
    img = ramp(fbm(111, base=2) * 0.5, [(0, '010208'), (0.6, '0a1440'), (1, '1e2a70')])
    lines = cv2.resize(rng(112).random((300, 1)).astype(np.float32), (1, H), interpolation=cv2.INTER_NEAREST)[:, 0]
    streak = (lines[yy.astype(int)] > 0.86).astype(np.float32) * (np.abs(xx - CX) / CX) ** 2
    img = add(img, streak * (1 - NEAR_ZONE * 0.7) * 0.6 + glow(streak, 2) * 0.4, 'a8c8ff')
    r = rng(113); st = np.zeros((H, W), np.float32)
    for _ in range(500): cv2.circle(st, (int(r.uniform(0, W)), int(r.uniform(0, H))), int(r.uniform(1, 2.5)), 1.0, -1, cv2.LINE_AA)
    img = add(img, glow(st, 0.8) * 1.4 * (1 - ZMASK), 'eef4ff')
    beam = np.exp(-DIV_D ** 2 / 10)
    img = add(img, beam + glow(beam, 20) * 0.8, 'b8d0ff', 0.9)
    neb = np.exp(-pr / (0.9 * U)) * fbm(114, base=4)
    img = add(img, symm(neb) * 0.6, '6a5aff', 0.5)
    img = sym(img)
    img = corners(img, hexc('9ab8ff'), "silver", "ring")
    return finish(img, "silver", hexc('9ab8ff'))

def art_mat(passcode, metal_name, accent, grade):
    """Each half shows the monster faintly, filling its half; the rival's half turned round."""
    art = cv2.imread(os.path.join(ART, f"art-{passcode}.jpg"))[:, :, ::-1].astype(np.float32) / 255
    hw, hh = W, H // 2
    s = max(hw / art.shape[1], hh / art.shape[0])
    big = cv2.resize(art, (int(art.shape[1] * s) + 1, int(art.shape[0] * s) + 1), interpolation=cv2.INTER_LANCZOS4)
    y0 = (big.shape[0] - hh) // 3; x0 = (big.shape[1] - hw) // 2
    half = big[y0:y0 + hh, x0:x0 + hw]
    img = np.zeros((H, W, 3), np.float32); img[H - hh:] = half
    lum = img.mean(axis=2, keepdims=True)
    img = (img * 0.55 + lum * hexc(grade) * 0.9) * 0.6                      # colour-graded, dimmed
    img = sym(img)
    img *= (0.55 + 0.45 * np.clip(DIV_D / 120, 0, 1))[..., None]              # darker divider
    seam = np.exp(-DIV_D ** 2 / 12)
    img = add(img, seam + glow(seam, 14) * 0.5, accent, 0.8)
    img = corners(img, accent, metal_name)
    return finish(img, metal_name, accent, calm=0.6)


# ---------------------------------------------------------------- Chaos Rift styles (2026-11-09)

def rift_crack(seed, y0, width, amp):
    """A jagged crack running the length of the mat along y0 (px): distance field in px."""
    r = rng(seed)
    pts = []
    x = 60.0
    y = y0
    while x < W - 60:
        pts.append((x, y)); x += r.uniform(30, 70); y = y0 + r.normal(0, amp)
    pts.append((W - 60, y0))
    m = np.zeros((H, W), np.float32)
    cv2.polylines(m, [np.array(pts, np.int32)], False, 1.0, width, cv2.LINE_AA)
    return m

def obsidian():
    """Black marble with gold veins; the centre divider is a hairline crack lit in gold."""
    base = fbm(201, base=3) * 0.6 + fbm(202, base=18, octaves=3) * 0.4
    img = ramp(base, [(0, '050506'), (0.55, '121115'), (1, '24212a')])
    v = fbm(203, base=4)
    vein = np.exp(-(np.abs(v - 0.5) * 90) ** 2) * (0.4 + 0.6 * fbm(204, base=6))
    img = add(img, vein * FIELD * 0.9 + glow(vein * FIELD, 3) * 0.5, 'd7a23c', 0.8)
    crack = rift_crack(205, CY, 3, 4)
    img = add(img, crack + glow(crack, 5) * 0.8 + glow(crack, 30) * 0.25, 'ffe2a0')
    ring = np.exp(-((pr - 1.25 * U) ** 2) / 8)
    img = add(img, symm(ring) * FIELD, 'c98f2a', 0.3)
    img = sym(img)
    img = corners(img, hexc('e8c070'), "gold")
    return finish(img, "gold", hexc('e8c070'), calm=0.6)

def ivory():
    """Pale ivory stone with graphite veining: the light side of the rift as a table."""
    base = fbm(211, base=3) * 0.5 + fbm(212, base=24, octaves=3) * 0.5
    img = ramp(base, [(0, 'b8b2a6'), (0.6, 'd8d2c6'), (1, 'eeeae2')])
    v = fbm(213, base=5)
    vein = np.exp(-(np.abs(v - 0.5) * 80) ** 2) * fbm(214, base=5)
    img = over(img, vein * 0.55, '3a3a40')
    line = np.exp(-DIV_D ** 2 / 4)
    img = over(img, line * 0.8, '9a7428')
    ring = np.exp(-((pr - 1.25 * U) ** 2) / 6) + np.exp(-((pr - 1.12 * U) ** 2) / 3) * 0.6
    img = over(img, symm(ring) * FIELD * 0.35, '8a6a2a')
    img = sym(img)
    img = corners(img, hexc('6a4a14'), "gold")
    return finish(img, "gold", hexc('c08a30'), calm=0.42)

def eclipse():
    """Deep indigo night; each half holds a dark sun with a silver corona."""
    img = ramp(fbm(221, base=2) * 0.6, [(0, '030308'), (0.6, '0c0a1c'), (1, '1a1630')])
    corona = np.exp(-np.clip(pr - 0.5 * U, 0, None) / (0.3 * U)) * (pr > 0.45 * U) * (0.6 + 0.4 * fbm(222, base=12))
    rays = (0.5 + 0.5 * np.cos(pth * 36)) ** 6 * np.exp(-np.clip(pr - 0.5 * U, 0, None) / (0.6 * U)) * (pr > 0.45 * U)
    disc = (pr < 0.45 * U).astype(np.float32)
    img = add(img, symm(corona + rays * 0.8) * FIELD + glow(symm(corona) * FIELD, 12) * 0.6, 'dfe6ff', 0.9)
    img = over(img, symm(glow(disc, 2)) * FIELD * 0.9, '020205')
    r = rng(223); st = np.zeros((H, W), np.float32)
    for _ in range(350): cv2.circle(st, (int(r.uniform(0, W)), int(r.uniform(0, H))), 1, 1.0, -1, cv2.LINE_AA)
    img = add(img, st * (1 - ZMASK) * 0.8, 'eef2ff')
    line = np.exp(-DIV_D ** 2 / 3)
    img = add(img, line + glow(line, 12) * 0.5, 'c8d2ff', 0.8)
    img = sym(img)
    img = corners(img, hexc('c8d2ff'), "silver", "ring")
    return finish(img, "silver", hexc('c8d2ff'))

def crimson():
    """Red-black lacquer with thin gold filigree in the gaps, a gold divider."""
    lac = fbm(231, base=3, aspect=(1, 3))
    img = ramp(lac * 0.7, [(0, '120203'), (0.6, '2e0608'), (1, '4a0c10')])
    sheen = np.exp(-((yy - CY * 0.55) ** 2) / (2 * (H * 0.12) ** 2)) * 0.12
    img = add(img, symm(sheen), 'ff9080')
    fil = np.zeros((H, W), np.float32)
    for k in range(-5, 6):
        cx = CX + k * 0.9 * U * 0.5
        cv2.ellipse(fil, (int(cx), int(CY + 1.12 * U)), (int(0.2 * U), int(0.14 * U)), 0, 0, 360, 1.0, 2, cv2.LINE_AA)
    img = add(img, symm(fil) * FIELD * 0.7 + glow(symm(fil) * FIELD, 3) * 0.3, 'e0b050')
    line = np.exp(-DIV_D ** 2 / 5)
    img = add(img, line * 1.2 + glow(line, 8) * 0.4, 'f0c060')
    img = sym(img)
    img = corners(img, hexc('f0c060'), "gold")
    return finish(img, "gold", hexc('f0c060'))

STYLES = [
    ("magma", magma), ("tide", tide), ("storm", storm), ("radiant", radiant), ("ruins", ruins), ("skyway", skyway),
    ("pharaoh", pharaoh), ("datastream", datastream), ("cup_arena", cup_arena), ("twin_vortex", twin_vortex),
    ("steel", steel), ("starlane", starlane),
    ("obsidian", obsidian), ("ivory", ivory), ("eclipse", eclipse), ("crimson", crimson),
    ("art_dark_magician", lambda: art_mat("46986414", "violet", hexc('c07aff'), 'c8a0ff')),
    ("art_blue_eyes", lambda: art_mat("89631139", "silver", hexc('8ad0ff'), 'a8d8ff')),
    ("art_red_eyes", lambda: art_mat("74677422", "crimson", hexc('ff5a3a'), 'ff9a80')),
]

if __name__ == "__main__":
    for name, fn in STYLES:
        if ONLY and name not in ONLY: continue
        save(name, fn()); print("made", name, flush=True)
    files = [os.path.join(OUT, n + ".jpg") for n, _ in STYLES if os.path.exists(os.path.join(OUT, n + ".jpg"))]
    th = [cv2.resize(cv2.imread(f), (480, 322), interpolation=cv2.INTER_AREA) for f in files]
    while len(th) % 4: th.append(np.full_like(th[0], 24))
    pad = lambda im: cv2.copyMakeBorder(im, 10, 10, 10, 10, cv2.BORDER_CONSTANT, value=(24, 24, 24))
    cv2.imwrite(os.path.join(OUT, "sheet.png"), np.vstack([np.hstack([pad(t) for t in th[i:i + 4]]) for i in range(0, len(th), 4)]))
    print("sheet")
