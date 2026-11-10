"""Bring staged anime-pool cards into the game, one batch at a time.

    python game/tools/pool_import.py

Reads remake/data/anime_pool/import.txt (card names, one per line, '#' comments), takes each card's official data,
text and rulings from remake/data/anime_pool/spec.json + the scan manifest, and writes game/Engine/pool_cards.json
(embedded by Engine.csproj). Each card's idx is 2000 + its position in spec.json, so it never changes between runs
(saves store idx). Copies the HD scan to Godot/cards/hd/<idx>.jpg and a 400px copy to cards/small/<idx>.jpg.
The cards are not obtainable (no pack bits): rewards, shop and Card List only see them once a pool gives them out.
"""
import json, os, shutil, sys
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
POOL = os.path.join(ROOT, 'remake', 'data', 'anime_pool')
CARDS = os.path.join(ROOT, 'game', 'Godot', 'cards')
OUT = os.path.join(ROOT, 'game', 'Engine', 'pool_cards.json')

spec = json.load(open(os.path.join(POOL, 'spec.json'), encoding='utf-8'))
spec = list(spec.values()) if isinstance(spec, dict) else spec
manifest = {m['passcode']: m for m in json.load(open(os.path.join(CARDS, 'anime_pending', 'manifest.json'), encoding='utf-8'))}
game = json.load(open(os.path.join(ROOT, 'remake', 'data', 'cards.json'), encoding='utf-8'))
in_game = {c['name'].lower() for c in game}
# The PoC already has these under older names (same card): keep its printing, skip the pool's.
in_game_pass = {c.get('passcode') for c in game if c['name']}
RENAMED = {'black skull dragon'}   # = B. Skull Dragon (different passcode in the pool)

wanted = [l.split('#')[0].strip() for l in open(os.path.join(POOL, 'import.txt'), encoding='utf-8')]
wanted = [w for w in wanted if w]
by_name = {c['name'].lower(): (i, c) for i, c in enumerate(spec) if c['source'] == 'real'}

FRAME = {'Effect Monster': 'Effect', 'Normal Monster': 'Normal', 'Fusion': 'Fusion', 'Ritual': 'Ritual'}
out, bad = [], []
for name in wanted:
    if name.lower() in in_game or name.lower() in RENAMED: continue
    if name.lower() not in by_name: bad.append(name); continue
    i, c = by_name[name.lower()]
    if c['passcode'] in in_game_pass: continue
    m = manifest[c['passcode']]
    idx = 2000 + i
    kind = 'spell' if c['kind'] == 'Spell' else 'trap' if c['kind'] == 'Trap' else 'monster'
    out.append({
        'idx': idx, 'konami_id': 1_000_000 + idx, 'db_id': m['konami_id'],   # own id: some pool cards share or lack a Konami database id
        'name': c['name'], 'passcode': c['passcode'], 'kind': kind,
        'frame': FRAME.get(c['kind']) or c['property'] or 'Normal',
        'attribute': c.get('attribute') or '', 'race': c.get('race') or '', 'level': c.get('level') or 0,
        'atk': c.get('atk') or 0, 'def': c.get('def') or 0, 'desc': c['text'], 'archetype': c.get('archetype') or '',
        'rulings': c.get('rulings') or [], 'series': c.get('series') or [],
    })
    hd = os.path.join(CARDS, 'hd', f'{idx}.jpg')
    if not os.path.exists(hd): shutil.copyfile(os.path.join(CARDS, 'anime_pending', 'hd', f'{c["passcode"]}.jpg'), hd)
    small = os.path.join(CARDS, 'small', f'{idx}.jpg')
    if not os.path.exists(small):
        im = Image.open(hd).convert('RGB')
        im.resize((400, round(im.height * 400 / im.width)), Image.LANCZOS).save(small, quality=90)

if bad: sys.exit('not in the pool (anime-only or misspelled): ' + ', '.join(bad))
json.dump(sorted(out, key=lambda c: c['idx']), open(OUT, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print(f'{len(out)} cards -> {OUT}')
