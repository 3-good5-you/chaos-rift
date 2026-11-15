"""Card names to translate (user 2026-11-14: names shown as text follow the game language; the card art keeps its
printed English name). Writes names/batch_NN.en.json ({key: English name}) from remake/data/cards.json and
Engine/pool_cards.json; merge_names.py builds Godot/i18n/names.<lang>.json ({English: translated})."""
import json, os, sys
here = os.path.dirname(os.path.abspath(__file__))
data = os.path.join(here, "..", "..", "..", "remake", "data")
engine = os.path.join(here, "..", "..", "Engine")
out = os.path.join(here, "names")
os.makedirs(out, exist_ok=True)
names = []
for f in (os.path.join(data, "cards.json"), os.path.join(engine, "pool_cards.json")):
    for c in json.load(open(f, encoding="utf-8")):
        n = (c.get("name") or "").strip()
        if n and n not in names: names.append(n)
size = int(sys.argv[1]) if len(sys.argv) > 1 else 300
items = [(f"n{k:04d}", n) for k, n in enumerate(names)]
for b in range(0, len(items), size):
    json.dump(dict(items[b:b + size]), open(os.path.join(out, f"batch_{b // size:02d}.en.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print(f"names {len(items)}, batches {(len(items) + size - 1) // size}")
