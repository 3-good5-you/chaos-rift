"""Frame Forge: our own icon frames (profile picture borders), in the families Master Duel uses (metal tiers, the six
attributes, themed "deluxe" frames) but round, to sit around a round profile picture. 512x512 RGBA PNGs; the picture
shows through the hole (radius HOLE). The frame may lap a little over the picture's edge, never over its middle.
Usage: python frameforge.py <out_dir> [names]    (also writes sheet.png: every frame around sample pictures if
<art_dir> is given as FRAME_ART env var)"""
import sys, os, math
import numpy as np
import cv2
from PIL import Image, ImageDraw, ImageFont

OUT = sys.argv[1]
ONLY = set(sys.argv[2:])
os.makedirs(OUT, exist_ok=True)

S = 512
C = S / 2
HOLE = 168                       # picture radius
yy, xx = np.mgrid[0:S, 0:S].astype(np.float32) + 0.5
R = np.hypot(xx - C, yy - C)
TH = np.arctan2(yy - C, xx - C)  # 0 = right, +pi/2 = down

def rng(seed): return np.random.default_rng(seed)
def hexc(s): s = s.lstrip('#'); return np.array([int(s[i:i + 2], 16) / 255 for i in (0, 2, 4)], np.float32)
def col(c): return hexc(c) if isinstance(c, str) else np.asarray(c, np.float32)
def band(r0, r1, soft=1.2): return np.clip((R - r0) / soft + 0.5, 0, 1) * np.clip((r1 - R) / soft + 0.5, 0, 1)
def glow(m, s): return cv2.GaussianBlur(m.astype(np.float32), (0, 0), s)

def fbm(seed, base=4, octaves=5):
    r = rng(seed); out = np.zeros((S, S), np.float32); amp, tot = 1.0, 0.0
    for o in range(octaves):
        g = base * 2 ** o + 1
        out += cv2.resize(r.random((g, g)).astype(np.float32), (S, S), interpolation=cv2.INTER_CUBIC) * amp
        tot += amp; amp *= 0.5
    out /= tot
    return (out - out.min()) / (out.max() - out.min() + 1e-6)

def polar_fbm(seed, ang=12, rad=4):
    """Noise wrapped round the ring (seamless in angle)."""
    r = rng(seed); out = np.zeros((S, S), np.float32); amp, tot = 1.0, 0.0
    for o in range(4):
        a, b = ang * 2 ** o, rad * 2 ** o + 1
        grid = r.random((b, a)).astype(np.float32)
        u = (TH / (2 * np.pi) + 0.5) * a; v = np.clip(R / (S / 2), 0, 1) * (b - 1)
        u0 = np.floor(u).astype(int) % a; u1 = (u0 + 1) % a; fu = u - np.floor(u)
        v0 = np.clip(np.floor(v).astype(int), 0, b - 1); v1 = np.clip(v0 + 1, 0, b - 1); fv = v - np.floor(v)
        fu = fu * fu * (3 - 2 * fu); fv = fv * fv * (3 - 2 * fv)
        val = (grid[v0, u0] * (1 - fu) + grid[v0, u1] * fu) * (1 - fv) + (grid[v1, u0] * (1 - fu) + grid[v1, u1] * fu) * fv
        out += val * amp; tot += amp; amp *= 0.5
    return out / tot

METAL = {
    "bronze": ['3a1e0e', '9a5a2a', 'ffd2a0', 'a8642e', '40220e'], "silver": ['3e444c', 'aeb6c0', 'ffffff', 'b9c1cb', '4a5058'],
    "gold": ['5a3a0e', 'c98f2a', 'fff0b0', 'd7a23c', '6b4510'], "platinum": ['3c3550', 'b8b0d8', 'ffffff', 'c8c0e8', '453c5c'],
    "diamond": ['0c2a48', '6ac0f0', 'ffffff', '8ad8ff', '0e3050'], "violet": ['2a0f40', '8a4ad0', 'f2dcff', '9a5ae0', '2e1048'],
    "crimson": ['3a0606', 'c0302a', 'ffd0b0', 'd04030', '400808'], "azure": ['061a3a', '2a7ad0', 'd8f2ff', '3a8ae0', '08203e'],
    "jade": ['062a18', '2aa060', 'd8ffe8', '3ab070', '08301c'], "earth": ['2a1a0a', '8a6a3a', 'f0dcb0', '9a7a48', '30200c'],
    "obsidian": ['08080c', '3a3a48', 'b0b0c8', '44445a', '0a0a10'],
}

def ramp(t, stops):
    t = np.clip(t, 0, 1); out = np.zeros(t.shape + (3,), np.float32)
    n = len(stops) - 1
    for i in range(n):
        a, b = i / n, (i + 1) / n
        k = np.clip((t - a) / (b - a), 0, 1)[..., None]
        sel = ((t >= a) & (t <= b))[..., None]
        out = np.where(sel, hexc(stops[i]) * (1 - k) + hexc(stops[i + 1]) * k, out)
    return out

def metal_ring(r0, r1, name, shine_seed=0):
    """A bevelled metal band: rounded profile across the band, light from the top-left."""
    w = r1 - r0
    across = np.clip((R - r0) / w, 0, 1)
    prof = np.sin(across * np.pi)                                  # 0 at the edges, 1 in the middle
    light = 0.5 + 0.5 * np.cos(TH + 2.35)                          # brightest top-left
    t = np.clip(0.2 + 0.55 * prof * (0.55 + 0.45 * light) + 0.08 * np.sin(TH * 7 + shine_seed), 0, 1)
    return ramp(t, METAL[name]), band(r0, r1)

class Frame:
    def __init__(self): self.rgb = np.zeros((S, S, 3), np.float32); self.a = np.zeros((S, S), np.float32)
    def over(self, rgb, a):
        a = np.clip(a, 0, 1); rgb = np.broadcast_to(col(rgb) if not isinstance(rgb, np.ndarray) or rgb.ndim == 1 else rgb, (S, S, 3))
        self.rgb = self.rgb * (1 - a[..., None]) + rgb * a[..., None]; self.a = self.a + a * (1 - self.a)
    def add(self, m, c, k=1.0):
        """Glow: adds light and the alpha to carry it."""
        m = np.clip(m * k, 0, None); self.rgb = self.rgb + m[..., None] * col(c); self.a = np.clip(self.a + np.clip(m, 0, 1) * 0.9, 0, 1)
    def ring(self, r0, r1, name, seed=0): rgb, a = metal_ring(r0, r1, name, seed); self.over(rgb, a)
    def line(self, r, w, c, k=1.0): self.add(band(r - w / 2, r + w / 2, 0.8), c, k)
    def shadow(self):
        """Soft dark lip just inside the hole, so the frame sits on the picture."""
        m = np.clip(1 - (HOLE + 2 - R) / 14, 0, 1) * (R < HOLE + 4)
        self.over(np.zeros(3), m * 0.55)
    def png(self):
        rgb = np.clip(self.rgb, 0, 1); a = np.clip(self.a, 0, 1)
        out = np.dstack([rgb[..., ::-1], a]) * 255
        return out.astype(np.uint8)

def studs(f, n, r, size, metal, phase=0.0, shape="diamond"):
    m = np.zeros((S, S), np.float32)
    for i in range(n):
        a = phase + 2 * np.pi * i / n
        x, y = C + r * math.cos(a), C + r * math.sin(a)
        if shape == "diamond":
            pts = np.array([[x + size * math.cos(a), y + size * math.sin(a)], [x + size * 0.6 * math.cos(a + 1.57), y + size * 0.6 * math.sin(a + 1.57)],
                            [x - size * math.cos(a), y - size * math.sin(a)], [x + size * 0.6 * math.cos(a - 1.57), y + size * 0.6 * math.sin(a - 1.57)]], np.int32)
            cv2.fillConvexPoly(m, pts, 1.0, cv2.LINE_AA)
        else:
            cv2.circle(m, (int(x), int(y)), int(size), 1.0, -1, cv2.LINE_AA)
    rgb, _ = metal_ring(0, S, metal)
    shade = 0.6 + 0.6 * (glow(m, 2) - glow(np.roll(np.roll(m, 3, 0), 3, 1), 2))
    f.over(np.clip(ramp(np.full((S, S), 0.55), METAL[metal]) * shade[..., None], 0, 1), m)

def blades(f, n, r0, r1, width, metal, phase=0.0):
    """Pointed rays standing out from the ring (crown / sun)."""
    m = np.zeros((S, S), np.float32)
    for i in range(n):
        a = phase + 2 * np.pi * i / n
        tip = (C + r1 * math.cos(a), C + r1 * math.sin(a))
        l = (C + r0 * math.cos(a - width), C + r0 * math.sin(a - width)); rr = (C + r0 * math.cos(a + width), C + r0 * math.sin(a + width))
        cv2.fillConvexPoly(m, np.array([tip, l, rr], np.int32), 1.0, cv2.LINE_AA)
    t = np.clip(0.35 + 0.4 * np.cos(TH + 2.35) + 0.25 * np.clip((R - r0) / (r1 - r0), 0, 1), 0, 1)
    f.over(ramp(t, METAL[metal]), glow(m, 0.6))

# ---- attribute badge: the orb with its kanji, lower right

FONT = next((p for p in ["C:/Windows/Fonts/YuGothB.ttc", "C:/Windows/Fonts/msgothic.ttc", "C:/Windows/Fonts/meiryob.ttc"] if os.path.exists(p)), None)

def orb(f, kanji, c0, c1, at=0.78):
    cx, cy, rad = C + 205 * math.cos(at), C + 205 * math.sin(at), 44
    d = np.hypot(xx - cx, yy - cy)
    disc = np.clip((rad - d) / 1.2 + 0.5, 0, 1)
    rim = np.clip((rad - d) / 1.2 + 0.5, 0, 1) - np.clip((rad - 5 - d) / 1.2 + 0.5, 0, 1)
    shade = np.clip(1 - np.hypot(xx - cx + 14, yy - cy + 16) / (rad * 1.7), 0, 1)
    body = col(c1) * (0.45 + 0.9 * shade[..., None]) + col(c0) * (1 - shade[..., None]) * 0.4
    f.over(np.zeros(3), glow(disc, 5) * 0.6)
    f.over(body, disc)
    f.over(ramp(np.clip(0.5 + 0.4 * np.cos(TH + 2.35), 0, 1), METAL["gold"]), rim)
    if FONT:
        img = Image.new("L", (S, S), 0); dr = ImageDraw.Draw(img)
        font = ImageFont.truetype(FONT, 50)
        dr.text((cx, cy + 2), kanji, fill=255, font=font, anchor="mm")
        k = np.asarray(img, np.float32) / 255
        f.over(np.zeros(3), glow(k, 2) * 0.7)
        f.over(hexc('fff8e8'), k)

# ---------------------------------------------------------------- the frames

def classic():
    f = Frame(); f.shadow(); f.ring(166, 184, "gold"); f.line(186, 1.5, 'ffe7a0', 0.6); return f

def tier(metal, level):
    """Bronze 1 .. Master 6: each step adds detail."""
    f = Frame(); f.shadow()
    if level >= 4: blades(f, 12 if level < 6 else 16, 196, 224 + level * 2, 0.11, metal, phase=np.pi / 12)
    f.ring(164, 194, metal, level)
    f.line(170, 1.4, 'ffffff', 0.25)
    if level >= 2: f.ring(198, 206, metal, level + 3)
    if level >= 3: studs(f, 4 if level == 3 else 8, 202, 13, metal, phase=-np.pi / 2)
    if level >= 5:                                  # facets: a light sparkle on each stud
        s = np.zeros((S, S), np.float32)
        for i in range(8):
            a = -np.pi / 2 + i * np.pi / 4
            cv2.circle(s, (int(C + 202 * math.cos(a)), int(C + 202 * math.sin(a))), 2, 1.0, -1, cv2.LINE_AA)
        f.add(glow(s, 3) * 6, 'ffffff')
    if level == 6:                                  # a crown gem on top
        studs(f, 1, 228, 20, "violet", phase=-np.pi / 2)
        f.add(glow((np.hypot(xx - C, yy - (C - 228)) < 8).astype(np.float32), 6) * 2.5, 'f0c0ff')
    return f

def light():
    f = Frame()
    rays = np.clip(np.cos(TH * 24) * 0.5 + 0.5, 0, 1) ** 6 * np.clip(1 - np.abs(R - 200) / 50, 0, 1)
    f.add(glow(rays, 1.5) * 0.9 + glow(band(150, 236), 10) * 0.35, 'ffe9a0')
    inner = np.clip(1 - np.abs(R - 160) / 16, 0, 1) * (0.5 + 0.5 * np.cos(TH * 48) ** 2)
    f.add(inner * 0.5, 'fff6d8')
    f.shadow(); f.ring(168, 190, "gold"); f.line(194, 2, 'fffbe0', 0.9)
    orb(f, "光", 'b08a20', 'ffe070'); return f

def dark():
    f = Frame()
    # shadow flames: dark tongues flaring out of the ring, lit purple at their edges
    n = polar_fbm(21, 12, 4)
    height = 26 + 46 * n
    tongue = np.clip((184 + height - R) / 8, 0, 1) * (R > 172)
    edge = np.clip((184 + height - R) / 3, 0, 1) - np.clip((184 + height - 6 - R) / 3, 0, 1)
    f.add(glow(tongue, 10) * 0.45, '8a20d0')
    f.over(ramp(np.clip((R - 172) / (height + 12), 0, 1), ['2a0640', '160424', '0a020f']), tongue * 0.95)
    f.add(glow(edge * (R > 180), 1.5) * 0.9, 'c060ff')
    f.shadow(); f.ring(168, 190, "violet"); f.line(194, 2, 'd080ff', 0.8)
    orb(f, "闇", '30083a', 'a040c0'); return f

def earth():
    f = Frame()
    # rock shards breaking out of the ring at uneven lengths
    r_ = rng(33); m = np.zeros((S, S), np.float32)
    for i in range(16):
        a = 2 * np.pi * i / 16 + r_.uniform(-0.08, 0.08); L = r_.uniform(206, 238); w = r_.uniform(0.09, 0.16)
        pts = [(C + L * math.cos(a + r_.uniform(-0.03, 0.03)), C + L * math.sin(a)), (C + 186 * math.cos(a - w), C + 186 * math.sin(a - w)),
               (C + (L - 14) * math.cos(a - w * 0.6), C + (L - 14) * math.sin(a - w * 0.6)), (C + 186 * math.cos(a + w), C + 186 * math.sin(a + w))]
        cv2.fillPoly(m, [np.array(pts, np.int32)], 1.0, cv2.LINE_AA)
    shade = np.clip(0.35 + 0.35 * np.cos(TH + 2.35) + 0.3 * fbm(34, 10), 0, 1)
    f.over(np.zeros(3), glow(m, 4) * 0.6); f.over(ramp(shade, METAL["earth"]), m)
    f.add(glow(m, 6) * 0.15, 'e0a040')
    f.shadow(); f.ring(166, 192, "earth")
    cracks = (np.abs(polar_fbm(35, 20, 6) - 0.5) < 0.012) * band(168, 190)
    f.over(hexc('1a1006'), cracks.astype(np.float32) * 0.8)
    f.line(194, 1.5, 'e0c080', 0.5)
    orb(f, "地", '4a3010', 'b08040'); return f

def water():
    f = Frame()
    for k, (r, amp, freq, c) in enumerate([(204, 10, 9, '3aa0ff'), (196, 8, 13, '8ad8ff')]):
        crest = r + amp * np.sin(TH * freq + k * 1.3)
        wave = np.clip((crest - R) / 1.5 + 0.5, 0, 1) * (R > 160)
        f.over(ramp(np.clip(0.3 + 0.4 * np.cos(TH + 2.35) + 0.3 * (crest - R) / 40, 0, 1), METAL["azure"]), wave)
        f.add(np.exp(-((R - crest) ** 2) / 4) * (R > 160) * 0.8, 'e0f6ff')
    bub = np.zeros((S, S), np.float32); r_ = rng(41)
    for _ in range(26):
        a = r_.uniform(0, 2 * np.pi); rr = r_.uniform(212, 240)
        cv2.circle(bub, (int(C + rr * math.cos(a)), int(C + rr * math.sin(a))), int(r_.uniform(2, 6)), 1.0, 1, cv2.LINE_AA)
    f.add(bub * 0.9, 'c8f0ff')
    f.shadow(); f.ring(168, 188, "azure"); f.line(171, 1.2, 'ffffff', 0.4)
    orb(f, "水", '0a3a7a', '40a0ff'); return f

def fire():
    f = Frame()
    n = polar_fbm(51, 14, 4)
    up = np.clip(-np.sin(TH), 0, 1)                                 # taller flames at the top
    height = 22 + 40 * n * (0.5 + 0.8 * up)
    flame = np.clip((186 + height - R) / 6, 0, 1) * (R > 176)
    heat = np.clip((R - 176) / (height + 10), 0, 1)
    rgb = ramp(1 - heat, ['3a0400', 'c02010', 'ff7a14', 'ffd060', 'fff4c0'])
    f.over(rgb, flame * np.clip(1.2 - heat, 0, 1))
    f.add(glow(flame, 6) * 0.35, 'ff5010')
    f.shadow(); f.ring(168, 190, "crimson")
    f.add(band(160, 168, 3) * 0.6, 'ff6020')
    orb(f, "炎", '5a0a04', 'ff4020'); return f

def wind():
    f = Frame()
    m = np.zeros((S, S), np.float32)
    for i in range(7):
        a0 = i * 2 * np.pi / 7
        pts = [(C + (196 + 34 * t) * math.cos(a0 + 1.6 * t), C + (196 + 34 * t) * math.sin(a0 + 1.6 * t)) for t in np.linspace(0, 1, 40)]
        for (x0, y0), (x1, y1), t in zip(pts, pts[1:], np.linspace(0, 1, 40)):
            cv2.line(m, (int(x0 * 4), int(y0 * 4)), (int(x1 * 4), int(y1 * 4)), float(1 - t * 0.8), max(1, int(16 * (1 - t))), cv2.LINE_AA, shift=2)
    f.over(ramp(np.clip(0.4 + 0.4 * np.cos(TH + 2.35), 0, 1), METAL["jade"]), np.clip(m, 0, 1))
    f.add(glow(m, 4) * 0.4, '60ffa0')
    f.shadow(); f.ring(168, 190, "jade"); f.line(194, 1.5, 'b0ffd0', 0.7)
    orb(f, "風", '0a4020', '30c070'); return f

def millennium():
    f = Frame(); f.shadow()
    f.ring(164, 196, "gold", 2)
    # a band of carved marks round the ring, like the items' engravings
    marks = (np.abs(np.sin(TH * 36)) > 0.93) * band(172, 188)
    f.over(hexc('6b4510'), marks.astype(np.float32) * 0.7)
    f.ring(200, 208, "gold", 5)
    # the puzzle's point hanging below, two smaller ones at the sides
    m = np.zeros((S, S), np.float32)
    for a, s in ((np.pi / 2, 1.0), (np.pi / 2 - 0.55, 0.55), (np.pi / 2 + 0.55, 0.55)):
        bx, by = C + 200 * math.cos(a), C + 200 * math.sin(a)
        tip = (C + (200 + 52 * s) * math.cos(a), C + (200 + 52 * s) * math.sin(a))
        cv2.fillConvexPoly(m, np.array([tip, (bx + 26 * s * math.cos(a + 1.57), by + 26 * s * math.sin(a + 1.57)), (bx - 26 * s * math.cos(a + 1.57), by - 26 * s * math.sin(a + 1.57))], np.int32), 1.0, cv2.LINE_AA)
    f.over(ramp(np.clip(0.35 + 0.45 * np.cos(TH + 2.35) + 0.2 * (xx - C) / S, 0, 1), METAL["gold"]), glow(m, 0.6))
    f.add(glow(band(150, 240, 20), 12) * 0.15, 'ffd070')
    return f

def fusion():
    f = Frame()
    # two forces spiralling into each other: three arms each, orange and violet, tapering as they sweep round
    for k, (c, metal) in enumerate((('ff8a30', "crimson"), ('a060ff', "violet"))):
        m = np.zeros((S, S), np.float32)
        for j in range(3):
            a0 = (k * 3 + j) * 2 * np.pi / 6
            pts = [(C + (178 + 58 * t) * math.cos(a0 + 2.2 * t), C + (178 + 58 * t) * math.sin(a0 + 2.2 * t)) for t in np.linspace(0, 1, 60)]
            for (x0, y0), (x1, y1), t in zip(pts, pts[1:], np.linspace(0, 1, 60)):
                cv2.line(m, (int(x0 * 4), int(y0 * 4)), (int(x1 * 4), int(y1 * 4)), 1.0, max(1, int(22 * (1 - t) ** 0.8)), cv2.LINE_AA, shift=2)
        f.add(glow(m, 7) * 0.4, c)
        f.over(ramp(np.clip(0.35 + 0.4 * np.cos(TH + 2.35) + 0.25 * glow(m, 2), 0, 1), METAL[metal]), np.clip(m, 0, 1))
    f.shadow(); f.ring(166, 184, "silver"); f.line(187, 1.4, 'ffffff', 0.6)
    return f

def rune():
    f = Frame()
    f.add(glow(band(190, 238, 3), 8) * 0.2, '9040ff')
    for r, w in ((236, 2), (226, 1.4), (196, 1.4)): f.line(r, w, 'd8a8ff', 1.0)
    glyph = (np.abs(np.sin(TH * 40)) > 0.75) * (np.abs(np.sin(TH * 5)) > 0.3) * band(201, 221)
    f.add(glyph.astype(np.float32) * 0.9, 'e0c0ff')
    star = np.zeros((S, S), np.float32)                                # a six-point star drawn round the hole
    for k in range(2):
        pts = [(C + 236 * math.cos(-np.pi / 2 + k * np.pi / 3 + i * 2 * np.pi / 3), C + 236 * math.sin(-np.pi / 2 + k * np.pi / 3 + i * 2 * np.pi / 3)) for i in range(3)]
        cv2.polylines(star, [np.array(pts, np.int32)], True, 1.0, 2, cv2.LINE_AA)
    f.add(star * (R > 184) * 0.9 + glow(star * (R > 184), 4) * 0.5, 'c080ff')
    f.shadow(); f.ring(166, 186, "violet")
    return f

def chain():
    f = Frame(); f.shadow()
    f.ring(166, 180, "obsidian")
    n = 18
    for i in range(2 * n):
        a = np.pi * i / n
        m = np.zeros((S, S), np.float32)
        cx, cy = C + 200 * math.cos(a), C + 200 * math.sin(a)
        if i % 2 == 0:   # a link lying flat along the ring
            cv2.ellipse(m, (int(cx), int(cy)), (21, 12), math.degrees(a) + 90, 0, 360, 1.0, 7, cv2.LINE_AA)
        else:            # the next one turned on its edge: a thick bar through both neighbours
            dx, dy = -math.sin(a) * 15, math.cos(a) * 15
            cv2.line(m, (int(cx - dx), int(cy - dy)), (int(cx + dx), int(cy + dy)), 1.0, 9, cv2.LINE_AA)
        t = np.clip(0.35 + 0.45 * np.cos(TH + 2.35) + 0.3 * glow(m, 1.5), 0, 1)
        f.over(np.zeros(3), glow(m, 3) * 0.55)
        f.over(ramp(t, METAL["silver"]), m)
    return f

def circuit():
    f = Frame()
    m = np.zeros((S, S), np.float32); nodes = np.zeros((S, S), np.float32); r_ = rng(77)
    for i in range(18):
        a = 2 * np.pi * i / 18 + r_.uniform(-0.05, 0.05)
        r1 = r_.uniform(206, 236)
        p0 = (C + 188 * math.cos(a), C + 188 * math.sin(a)); p1 = (C + r1 * math.cos(a), C + r1 * math.sin(a))
        da = r_.choice([-0.12, 0.12]); p2 = (C + r1 * math.cos(a + da), C + r1 * math.sin(a + da))
        cv2.line(m, tuple(map(int, p0)), tuple(map(int, p1)), 1.0, 2, cv2.LINE_AA); cv2.line(m, tuple(map(int, p1)), tuple(map(int, p2)), 1.0, 2, cv2.LINE_AA)
        cv2.circle(nodes, tuple(map(int, p2)), 4, 1.0, -1, cv2.LINE_AA)
    f.add(m * 0.9 + glow(m, 3) * 0.6, '3ae0ff'); f.add(nodes + glow(nodes, 4), 'b0f8ff')
    f.shadow(); f.ring(166, 188, "silver"); f.line(192, 1.5, '3ae0ff', 1.0); f.line(170, 1.2, '3ae0ff', 0.6)
    return f

FRAMES = [
    ("classic", classic),
    ("bronze", lambda: tier("bronze", 1)), ("silver", lambda: tier("silver", 2)), ("gold", lambda: tier("gold", 3)),
    ("platinum", lambda: tier("platinum", 4)), ("diamond", lambda: tier("diamond", 5)), ("master", lambda: tier("gold", 6)),
    ("light", light), ("dark", dark), ("earth", earth), ("water", water), ("fire", fire), ("wind", wind),
    ("millennium", millennium), ("fusion", fusion), ("rune", rune), ("chain", chain), ("circuit", circuit),
]

def sample(frame_png, pic_path):
    """The frame over a round card picture on a dark backdrop (preview only)."""
    bg = np.full((S, S, 3), 22, np.uint8)
    if pic_path and os.path.exists(pic_path):
        p = cv2.imread(pic_path); h, w = p.shape[:2]
        side = int(w * 0.845); x0, y0 = int(w * 0.08), int(h * 0.155) if w > 400 else int(h * 0.2)
        p = cv2.resize(p[y0:y0 + side, x0:x0 + side], (2 * HOLE + 8, 2 * HOLE + 8))
        mask = (np.hypot(*np.mgrid[-(HOLE + 4):HOLE + 4, -(HOLE + 4):HOLE + 4]) < HOLE + 3).astype(np.float32)[..., None]
        o = int(C - HOLE - 4)
        bg[o:o + p.shape[0], o:o + p.shape[1]] = (p * mask + bg[o:o + p.shape[0], o:o + p.shape[1]] * (1 - mask)).astype(np.uint8)
    a = frame_png[..., 3:4] / 255.0
    return (bg * (1 - a) + frame_png[..., :3] * a).astype(np.uint8)

if __name__ == "__main__":
    art = os.environ.get("FRAME_ART", "")
    pics = sorted(f for f in os.listdir(art) if f.startswith("hd-")) if art else []
    shots = []
    for i, (name, fn) in enumerate(FRAMES):
        if ONLY and name not in ONLY: continue
        png = fn().png()
        cv2.imwrite(os.path.join(OUT, name + ".png"), png); print("made", name, flush=True)
        tile = sample(png, os.path.join(art, pics[(i * 7) % len(pics)]) if pics else None)
        cv2.putText(tile, name.upper(), (16, S - 16), cv2.FONT_HERSHEY_SIMPLEX, 0.9, (230, 230, 230), 2, cv2.LINE_AA)
        shots.append(cv2.resize(tile, (300, 300), interpolation=cv2.INTER_AREA))
    while len(shots) % 6: shots.append(np.full_like(shots[0], 22))
    cv2.imwrite(os.path.join(OUT, "sheet.png"), np.vstack([np.hstack(shots[i:i + 6]) for i in range(0, len(shots), 6)]))
    print("sheet")
