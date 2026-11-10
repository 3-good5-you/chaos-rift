"""Chaos Rift app icon options (2026-11-09, round 2: must say Yu-Gi-Oh!). Writes Godot/assets/title/rift/icons/Y1..Y4.png (1024 px) plus
icons_sheet.png: each option at 256 / 64 / 32 / 16 px so the small sizes (taskbar, Finder list) can be judged.
Usage: python tools/iconforge.py"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RIFT = os.path.join(ROOT, "Godot", "assets", "title", "rift")
OUT = os.path.join(RIFT, "icons")
N = 1024
rng = np.random.default_rng(7)


def crack(top=0.56, bottom=0.44, amp=0.035, seed=3):
    """Jagged rift x positions per row (like RiftMenu.CrackFn: mix(top, bottom, y) plus a wobble)."""
    r = np.random.default_rng(seed)
    pts = np.cumsum(r.normal(0, 1, 24)); pts = (pts - pts.mean()) / (np.abs(pts).max() + 1e-6)
    ys = np.linspace(0, 1, N)
    wob = np.interp(ys, np.linspace(0, 1, 24), pts) * amp + np.sin(ys * 61) * 0.004
    return (top + (bottom - top) * ys + wob) * N


def rounded_mask(r=0.2):
    m = Image.new("L", (N, N), 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, N - 1, N - 1], int(N * r), fill=255)
    return m


def split_bg(cx, light=(246, 245, 241), dark=(10, 10, 13), glow=1.0):
    """Light left, dark right, a white-hot seam with a soft halo (stronger on the dark side)."""
    x = np.arange(N)[None, :].astype(float)
    d = x - cx[:, None]
    yy, xx = np.mgrid[0:N, 0:N] / N
    shade = 1 - 0.1 * np.hypot(xx + 0.05, yy + 0.1)
    img = np.where(d[..., None] < 0, np.array(light) * shade[..., None], np.array(dark) * (0.8 + 0.4 * (1 - shade))[..., None])
    core = np.exp(-np.abs(d) / 2.2) * 1.6
    halo = np.exp(-np.abs(d) / 26) * np.where(d > 0, 0.55, 0.2)
    img = img + (core + halo)[..., None] * np.array([255, 250, 255]) * glow
    return Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), "RGB")


def letter(x0, x1):
    """A glyph cut from the locked R0 'Chaos Rift' wordmark (chrome gothic, gold inline, black outline)."""
    im = Image.open(os.path.join(RIFT, "R0_name.png")).convert("RGBA").crop((x0, 0, x1, 290))
    a = np.array(im)
    a[115:185, 196:, 3] = 0   # the next letter's outline between the C's serifs
    im = Image.fromarray(a)
    return im.crop(im.getbbox())


def paste_centered(base, im, h, dx=0, dy=0, shadow=True):
    im = im.resize((int(im.width * h / im.height), h), Image.LANCZOS)
    x, y = (N - im.width) // 2 + dx, (N - im.height) // 2 + dy
    if shadow:
        s = Image.new("RGBA", base.size, 0)
        a = im.split()[3].point(lambda v: v * 0.7)
        s.paste(Image.new("RGBA", im.size, (0, 0, 0, 255)), (x + 10, y + 18), a)
        base.alpha_composite(s.filter(ImageFilter.GaussianBlur(22)))
    base.alpha_composite(im, (x, y))


def finish(img, name):
    img = img.convert("RGBA")
    img.putalpha(rounded_mask())
    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(OUT, name + ".png"))
    return img


ART = os.path.join(os.path.dirname(ROOT), "art-source")
PUZZLE = os.path.join(ART, "logos", "millennium_puzzle___render_by_alanmac95_daqj0nl-fullview-3689896331.png")
BACK = os.path.join(ROOT, "Godot", "assets", "backs", "01_vortex.png")


def rgba(path, h):
    im = Image.open(path).convert("RGBA")
    im = im.crop(im.getbbox())
    return im.resize((int(im.width * h / im.height), h), Image.LANCZOS)


def drop(base, im, x, y, blur=22, a=0.75, off=(10, 18)):
    s = Image.new("RGBA", base.size, 0)
    s.paste(Image.new("RGBA", im.size, (0, 0, 0, 255)), (x + off[0], y + off[1]), im.split()[3].point(lambda v: v * a))
    base.alpha_composite(s.filter(ImageFilter.GaussianBlur(blur)))
    base.alpha_composite(im, (x, y))


def rounded(im, r):
    m = Image.new("L", im.size, 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, im.width - 1, im.height - 1], r, fill=255)
    im.putalpha(m)
    return im


def y1_puzzle():
    """Y1 Millennium Puzzle: the gold puzzle on the rift (dark side mostly), the seam lighting its edge."""
    bg = split_bg(crack(0.34, 0.26, 0.03), glow=0.9).convert("RGBA")
    pz = rgba(PUZZLE, int(N * 0.66))
    drop(bg, pz, (N - pz.width) // 2, (N - pz.height) // 2 + 20, 26)
    return finish(bg, "Y1-puzzle")


def y2_back():
    """Y2 Card back: your swirl back with the Yu-Gi-Oh! logo, tilted, standing on the rift."""
    bg = split_bg(crack(0.6, 0.4)).convert("RGBA")
    card = rounded(Image.open(BACK).convert("RGBA").resize((int(N * 0.5), int(N * 0.5 * 1185 / 813)), Image.LANCZOS), 30)
    card = card.rotate(-8, Image.BICUBIC, expand=True)
    drop(bg, card, (N - card.width) // 2, (N - card.height) // 2, 30)
    return finish(bg, "Y2-card-back")


def y3_logo():
    """Y3 Logo: the Yu-Gi-Oh! chrome (R0's top piece, kanji and all) over 'Chaos Rift', on the rift."""
    bg = split_bg(crack(0.62, 0.38), light=(30, 30, 34), dark=(8, 8, 10), glow=0.7).convert("RGBA")
    logo = Image.open(os.path.join(RIFT, "R0.png")).convert("RGBA")
    logo = logo.crop(logo.getbbox())
    w = int(N * 0.94)
    logo = logo.resize((w, int(logo.height * w / logo.width)), Image.LANCZOS)
    drop(bg, logo, (N - w) // 2, (N - logo.height) // 2, 20)
    return finish(bg, "Y3-logo")


def y4_puzzle_back():
    """Y4 Puzzle + back: the swirl back behind, the Millennium Puzzle hanging in front of it."""
    bg = split_bg(crack(0.58, 0.42)).convert("RGBA")
    card = rounded(Image.open(BACK).convert("RGBA").resize((int(N * 0.5), int(N * 0.5 * 1185 / 813)), Image.LANCZOS), 30)
    card = card.rotate(8, Image.BICUBIC, expand=True)
    drop(bg, card, (N - card.width) // 2 + 90, (N - card.height) // 2 - 40, 28)
    pz = rgba(PUZZLE, int(N * 0.46))
    drop(bg, pz, (N - pz.width) // 2 - 90, (N - pz.height) // 2 + 120, 24, 0.85)
    return finish(bg, "Y4-puzzle-back")


REFS = os.path.join(ART, "icon-refs")   # the user's reference pictures (2026-11-09 round 3)


def pharaoh(size):
    """The pharaoh silhouette (white ink + gold puzzle on black), square around the seal circle."""
    im = Image.open(os.path.join(REFS, "pharaoh_silhouette.png")).convert("RGB").crop((-10, 183, 400, 593))
    return np.asarray(im.resize((size, size), Image.LANCZOS).filter(ImageFilter.UnsharpMask(2, 60, 2))).astype(float)


def z1_pharaoh():
    """Z1 Pharaoh: the silhouette as it is, on black."""
    a = pharaoh(int(N * 0.9))
    bg = Image.new("RGB", (N, N), tuple(int(v) for v in a[4, a.shape[1] // 2]))   # the art's own black
    bg.paste(Image.fromarray(a.astype(np.uint8)), (int(N * 0.05), int(N * 0.07)))
    return finish(bg, "Z1-pharaoh")


def z2_pharaoh_rift():
    """Z2 Pharaoh on the rift: white ink on the dark side, black ink on the light side, the puzzle gold on both."""
    a = np.zeros((N, N, 3)); k = int(N * 0.9); o = int(N * 0.05)
    a[o + 10:o + 10 + k, o:o + k] = pharaoh(k)
    lum = a.mean(2) / 255
    sat = (a.max(2) - a.min(2)) / 255
    gold = np.clip((sat - 0.12) * 4, 0, 1)[..., None]
    cx = crack(0.56, 0.44)
    light = (np.arange(N)[None, :] < cx[:, None])[..., None]
    bg = np.asarray(split_bg(cx)).astype(float)
    ink = np.where(light, np.array([18, 18, 22]), np.array([240, 240, 244]))
    out = bg * (1 - lum[..., None]) + ink * lum[..., None]
    out = out * (1 - gold) + a * gold
    return finish(Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)), "Z2-pharaoh-rift")


def ra(size):
    im = Image.open(os.path.join(REFS, "gods_poster.png")).convert("RGB").crop((60, 168, 300, 408))
    return im.resize((size, size), Image.LANCZOS).filter(ImageFilter.UnsharpMask(2, 60, 2))


def z3_ra():
    """Z3 Ra: the Winged Dragon emblem from the poster on its brown, with the poster's gold frame."""
    bg = Image.new("RGB", (N, N), (74, 45, 30))
    bg.paste(ra(int(N * 0.86)), (int(N * 0.07), int(N * 0.07)))
    ImageDraw.Draw(bg).rounded_rectangle([int(N * 0.05)] * 2 + [int(N * 0.95)] * 2, 40, outline=(214, 170, 92), width=14)
    return finish(bg, "Z3-ra")


def z4_ra_rift():
    """Z4 Ra on the rift: only the poster's lines (gold dragon, blue rays), the brown swapped for the light | dark rift."""
    k = int(N * 0.92); o = (N - k) // 2
    a = np.asarray(ra(k)).astype(float)
    alpha = np.clip((np.abs(a - np.array([74, 45, 30])).sum(2) - 60) / 90, 0, 1)
    lay = np.zeros((N, N, 4), np.uint8)
    lay[o:o + k, o:o + k, :3] = a.astype(np.uint8); lay[o:o + k, o:o + k, 3] = (alpha * 255).astype(np.uint8)
    bg = split_bg(crack(0.56, 0.44)).convert("RGBA")
    drop(bg, Image.fromarray(lay), 0, 0, 10, 0.6, (4, 8))
    return finish(bg, "Z4-ra-rift")


def emblem(box, size):
    im = Image.open(os.path.join(REFS, "gods_emblem.png")).convert("RGB").crop(box)
    return im.resize((size, size), Image.LANCZOS).filter(ImageFilter.UnsharpMask(2, 60, 2))


def w1_gods():
    """W1 Three gods: the emblem (Obelisk, Slifer, Ra, Exodia behind the X) on its black."""
    bg = Image.new("RGB", (N, N), (0, 0, 0))
    bg.paste(emblem((1, 165, 411, 575), int(N * 0.94)), (int(N * 0.03), int(N * 0.03)))
    return finish(bg, "W1-gods")


def w2_gods_rift():
    """W2 Three gods on the rift: the emblem cut out of its black (its purple glow kept) over the light | dark split."""
    k = int(N * 0.96); o = (N - k) // 2
    a = np.asarray(emblem((1, 165, 411, 575), k)).astype(float)
    # solid inside (dark outlines kept), only the outer glow fades: grow the bright mask, then soften its edge
    m = Image.fromarray((np.clip((a.max(2) - 14) / 50, 0, 1) * 255).astype(np.uint8))
    m = m.filter(ImageFilter.MaxFilter(21)).filter(ImageFilter.MinFilter(11)).filter(ImageFilter.GaussianBlur(6))
    lay = np.zeros((N, N, 4), np.uint8)
    lay[o:o + k, o:o + k, :3] = a.astype(np.uint8); lay[o:o + k, o:o + k, 3] = np.asarray(m)
    bg = split_bg(crack(0.58, 0.42)).convert("RGBA")
    drop(bg, Image.fromarray(lay), 0, 0, 24, 0.7)
    return finish(bg, "W2-gods-rift")


def w3_gods_close():
    """W3 Close-up: tighter on the X and the gods' heads, so it still reads at taskbar size."""
    bg = Image.new("RGB", (N, N), (0, 0, 0))
    bg.paste(emblem((66, 190, 346, 470), N), (0, 0))
    return finish(bg, "W3-gods-close")


def gods_black(box, fill, name):
    """The gods emblem on pure black, the crop square `box` (source px) filling `fill` of the tile. The art's own
    near-black (JPEG noise, faint glow edges) is pushed to true black so no square seam shows."""
    im = Image.open(os.path.join(REFS, "gods_emblem.png")).convert("RGB").crop(box)
    k = int(N * fill)
    a = np.asarray(im.resize((k, k), Image.LANCZOS).filter(ImageFilter.UnsharpMask(2, 70, 2))).astype(float)
    a = np.clip((a - 8) * 255 / 247, 0, 255)
    bg = np.zeros((N, N, 3)); o = (N - k) // 2
    bg[o:o + k, o:o + k] = a
    return finish(Image.fromarray(bg.astype(np.uint8)), name)


def v1_full():
    """V1 Full: nothing cut, the whole emblem as tall as the tile allows."""
    return gods_black((10, 170, 404, 570), 0.97, "V1-gods-full")


def v2_trim():
    """V2 Light trim: only the purple glow above and below goes; every god and the X ends stay."""
    return gods_black((36, 188, 378, 530), 1.0, "V2-gods-trim")


def v3_medium():
    """V3 Medium: tighter (the X tips and Exodia's lowest scroll go), the gods bigger for small sizes."""
    return gods_black((60, 198, 352, 490), 1.0, "V3-gods-medium")


def sheet(icons):
    sizes = (256, 64, 32, 16)
    W = 40 + sum(s + 30 for s in sizes)
    sh = Image.new("RGB", (W * 2, (256 + 60) * len(icons) + 20), (128, 128, 132))
    for k, (name, im) in enumerate(icons):
        for half, bgc in enumerate(((236, 236, 236), (30, 30, 34))):   # light and dark taskbars
            x = 20 + half * W
            sh.paste(bgc, (half * W, k * 316, (half + 1) * W, (k + 1) * 316))
            for s in sizes:
                t = im.resize((s, s), Image.LANCZOS)
                sh.paste(t, (x, k * 316 + 30 + (256 - s) // 2), t)
                x += s + 30
        ImageDraw.Draw(sh).text((8, k * 316 + 6), name, fill=(200, 60, 60))
    sh.save(os.path.join(OUT, "icons_sheet.png"))


if __name__ == "__main__":
    z = [("Z1 pharaoh", z1_pharaoh()), ("Z2 pharaoh rift", z2_pharaoh_rift()), ("Z3 ra", z3_ra()), ("Z4 ra rift", z4_ra_rift())]
    w = [("W1 gods", w1_gods()), ("W2 gods rift", w2_gods_rift()), ("W3 gods close", w3_gods_close())]
    icons = [("V1 full", v1_full()), ("V2 light trim", v2_trim()), ("V3 medium trim", v3_medium()), w[2]]
    sheet(icons)
    print("wrote", OUT)
