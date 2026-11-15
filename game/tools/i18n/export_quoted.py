"""Quoted phrases still in English inside translated card text and duel prompts ("Elemental HERO", "Sheep Token",
cards outside the game's pool): the user wants every "..." translated (2026-11-15). Writes quoted/batch_NN.en.json
for translation; merge_quoted.py swaps them in. Run after merge_names.py."""
import json, os, re, sys, collections
here = os.path.dirname(os.path.abspath(__file__))
dest = os.path.join(here, "..", "..", "Godot", "i18n")
out = os.path.join(here, "quoted")
os.makedirs(out, exist_ok=True)
# the English originals' quoted phrases: a Spanish text still holding one of these exactly was left untranslated
import glob
english = set()
for f in glob.glob(os.path.join(here, "cards", "batch_*.en.json")) + glob.glob(os.path.join(here, "prompts", "batch_*.en.json")):
    for v in json.load(open(f, encoding="utf-8")).values(): english.update(re.findall(r'"([^"]+)"', v))
seen = collections.Counter()
for lang in ("es", "ar"):
    for f in (f"cards.{lang}.json", f"duel.{lang}.json"):
        for v in json.load(open(os.path.join(dest, f), encoding="utf-8")).values():
            for q in re.findall(r'"([^"]+)"', v):
                left = q in english if lang == "es" else re.search(r"[A-Za-z]{2}", q) and not re.search(r"[؀-ۿ]", q)
                if left: seen[q] += 1
items = [(f"q{k:04d}", q) for k, (q, _) in enumerate(seen.most_common())]
size = int(sys.argv[1]) if len(sys.argv) > 1 else 300
for b in range(0, len(items), size):
    json.dump(dict(items[b:b + size]), open(os.path.join(out, f"batch_{b // size:02d}.en.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print(f"phrases {len(items)}, batches {(len(items) + size - 1) // size}")
print([q for q, _ in seen.most_common(25)])
