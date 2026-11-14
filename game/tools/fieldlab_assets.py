"""Download the CC0 Poly Haven assets the Field Lab uses into game/Godot/assets/fieldlab/ (not in git).

  python tools/fieldlab_assets.py            # everything in WANT
  python tools/fieldlab_assets.py id[:res]   # one asset

Textures: diffuse, GL normal, ARM (ao/rough/metal) and displacement as jpg. Models: glTF + its textures.
HDRIs: .hdr. Files already on disk are skipped. All assets are CC0 (polyhaven.com/license).
"""
import json, os, sys, urllib.request

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Godot', 'assets', 'fieldlab')
UA = {'User-Agent': 'asset-fetch/1.0'}

# id: resolution
WANT = {
    'monastery_stone_floor': '2k',
    'snow_02': '2k',
    'mossy_cobblestone': '2k',
    'marble_tiles': '2k',
    'volcanic_rock_tiles': '2k',
    'red_sandstone_pavement': '2k',
    'metal_plate': '1k',
    'slate_floor_02': '1k',
    'mossy_rock': '1k',
    'marble_01': '1k',
    'rusty_metal_02': '1k',
    'sandstone_blocks_05': '1k',
    'gothic_statue': '1k',
    'stone_fire_pit': '1k',
    'brass_candleholders': '1k',
    'boulder_01': '1k',
    'rock_moss_set_01': '1k',
    'tree_stump_01': '1k',
    'root_cluster_01': '1k',
    'fern_02': '1k',
    'marble_bust_01': '1k',
    'horse_statue_01': '1k',
    'brass_vase_01': '1k',
    'namaqualand_boulder_02': '1k',
    'coast_rocks_01': '1k',
    'ceramic_vase_01': '1k',
    'antique_ceramic_vase_01': '1k',
    'brass_pot_01': '1k',
    'castle_zavelstein_cellar': '1k',
    'snowy_forest': '1k',
    'mossy_forest': '1k',
    'small_cathedral': '1k',
    'qwantani_sunset': '1k',
    'smelting_tower_interior': '1k',
}


def get(url):
    return urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=120).read()


def save(url, path):
    if os.path.exists(path) and os.path.getsize(path) > 0:
        return 0
    os.makedirs(os.path.dirname(path), exist_ok=True)
    data = get(url)
    with open(path, 'wb') as f:
        f.write(data)
    return len(data)


def fetch(aid, res):
    files = json.loads(get(f'https://api.polyhaven.com/files/{aid}'))
    out = os.path.join(ROOT, aid)
    n = 0
    if 'hdri' in files:
        n += save(files['hdri'][res]['hdr']['url'], os.path.join(out, aid + '.hdr'))
    elif json.loads(get(f'https://api.polyhaven.com/info/{aid}'))['type'] == 2:      # model
        g = files['gltf'][res]['gltf']
        n += save(g['url'], os.path.join(out, aid + '.gltf'))
        for rel, inc in g.get('include', {}).items():
            n += save(inc['url'], os.path.join(out, rel))
    else:                                                  # texture set
        for key, name in (('Diffuse', 'diff'), ('nor_gl', 'nor'), ('arm', 'arm'), ('Displacement', 'disp')):
            if key in files and res in files[key] and 'jpg' in files[key][res]:
                n += save(files[key][res]['jpg']['url'], os.path.join(out, name + '.jpg'))
    print(f'{aid:32s} {res}  {n / 1e6:6.1f} MB new')
    return n


def icons():
    """Zone icons engraved in the tray beds: white on transparent, 256 px. Drawn here, not downloaded."""
    from PIL import Image, ImageDraw
    out = os.path.join(ROOT, 'icons')
    os.makedirs(out, exist_ok=True)
    S, W = 1024, 44          # drawn 4x, saved 256

    def card(d, cx, cy, w, h, rot=0, fill=None, width=W, dash=False):
        layer = Image.new('RGBA', (S, S), (0, 0, 0, 0))
        ld = ImageDraw.Draw(layer)
        box = (S / 2 - w / 2, S / 2 - h / 2, S / 2 + w / 2, S / 2 + h / 2)
        ld.rounded_rectangle(box, radius=w * 0.12, outline='white', width=width, fill=fill)
        if dash:                                  # break the outline into dashes: a card that is no longer there
            for i in range(0, S, 150):
                ld.rectangle((i + 55, 0, i + 115, S), fill=(0, 0, 0, 0))
        layer = layer.rotate(rot, resample=Image.BICUBIC)
        shifted = Image.new('RGBA', (S, S), (0, 0, 0, 0))
        shifted.paste(layer, (int(cx - S / 2), int(cy - S / 2)), layer)
        d.alpha_composite(shifted)

    def make(name, draw):
        im = Image.new('RGBA', (S, S), (0, 0, 0, 0))
        draw(im, ImageDraw.Draw(im))
        im.resize((256, 256), Image.LANCZOS).save(os.path.join(out, name + '.png'))

    def monster(im, d):       # a sword: monsters fight
        d.polygon([(512, 90), (572, 200), (572, 640), (452, 640), (452, 200)], fill='white')
        d.rounded_rectangle((330, 640, 694, 704), radius=30, fill='white')
        d.rectangle((482, 704, 542, 860), fill='white')
        d.ellipse((462, 850, 562, 950), fill='white')

    def spell(im, d):         # a four-point spark: spells and traps
        d.polygon([(512, 80), (590, 434), (944, 512), (590, 590), (512, 944), (434, 590), (80, 512), (434, 434)], fill='white')

    def field(im, d):         # a horizon with two peaks: the field spell changes the land
        d.polygon([(90, 820), (400, 300), (560, 560), (680, 400), (934, 820)], fill='white')
        d.ellipse((690, 150, 850, 310), fill='white')

    def deck(im, d):          # a stack of cards
        for i in range(3):
            card(im, 512 - 60 + i * 60, 512 + 60 - i * 60, 430, 620, fill=(0, 0, 0, 0) if i < 2 else None)

    def fusion(im, d):        # two cards crossing into one
        card(im, 512, 512, 400, 580, rot=22)
        card(im, 512, 512, 400, 580, rot=-22)

    def gy(im, d):            # a headstone
        d.rounded_rectangle((270, 150, 754, 900), radius=240, outline='white', width=W)
        d.rectangle((270, 600, 754, 900), outline='white', width=W)
        d.rectangle((150, 856, 874, 900), fill='white')
        d.rectangle((490, 330, 534, 620), fill='white')
        d.rectangle((400, 420, 624, 464), fill='white')

    def banish(im, d):        # a card with a broken outline: removed from play
        card(im, 512, 512, 460, 680, dash=True)

    def side(im, d):          # two cards side by side with a swap mark
        card(im, 330, 512, 300, 440)
        card(im, 694, 512, 300, 440)
        d.polygon([(470, 512), (554, 460), (554, 564)], fill='white')

    for name, fn in (('monster', monster), ('spell', spell), ('field', field), ('deck', deck), ('fusion', fusion), ('gy', gy), ('banish', banish), ('side', side)):
        make(name, fn)
    print('icons drawn')


if __name__ == '__main__':
    os.makedirs(ROOT, exist_ok=True)
    open(os.path.join(ROOT, '.gdignore'), 'a').close()    # loaded at runtime, the Godot importer must skip it
    want = dict(a.split(':') if ':' in a else (a, '1k') for a in sys.argv[1:]) or WANT
    total = sum(fetch(a, r) for a, r in want.items())
    print(f'total {total / 1e6:.1f} MB')
    icons()
