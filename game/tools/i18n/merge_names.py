"""Card names in the game language. Builds Godot/i18n/names.<lang>.json ({English name: translated}) from
names/batch_NN.<lang>.json, then swaps the English names inside the translated card texts (quoted: "Dark Magician")
and duel prompts (as written) for the translated ones. Run last: merge_cards.py, merge_prompts.py, abbrev.py, this."""
import json, os, re, glob, sys
here = os.path.dirname(os.path.abspath(__file__))
src, dest = os.path.join(here, "names"), os.path.join(here, "..", "..", "Godot", "i18n")
en = {}
for f in glob.glob(os.path.join(src, "batch_*.en.json")): en.update(json.load(open(f, encoding="utf-8")))
bad = 0
for lang in sys.argv[1:] or ["es", "ar"]:
    tr = {}
    for f in glob.glob(os.path.join(src, f"batch_*.{lang}.json")):
        try: tr.update(json.load(open(f, encoding="utf-8")))
        except json.JSONDecodeError as e: print(f"  skipped unfinished {os.path.basename(f)}: {e}")
    names = {en[k]: v.strip() for k, v in tr.items() if k in en and v.strip()}
    missing = len(en) - len(names); bad += missing
    json.dump(names, open(os.path.join(dest, f"names.{lang}.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=0)
    swap = {k: v for k, v in names.items() if k != v}
    if not swap: continue
    longest = sorted(swap, key=len, reverse=True)
    quoted = re.compile('"(' + "|".join(map(re.escape, longest)) + ')(?:s|es|\'s)?"')   # "Nimble Momongas" is the card too
    bare = re.compile(r"(?<![\w-])(" + "|".join(map(re.escape, longest)) + r")(?![\w-])")
    counts = {}
    for fname, pat, fmt in ((f"cards.{lang}.json", quoted, '"{}"'), (f"duel.{lang}.json", bare, "{}")):
        p = os.path.join(dest, fname)
        d = json.load(open(p, encoding="utf-8")); n = 0
        for k, v in d.items():
            nv = pat.sub(lambda m: fmt.format(swap[m.group(1)]), v)
            if nv != v: d[k] = nv; n += 1
        json.dump(d, open(p, "w", encoding="utf-8"), ensure_ascii=False, indent=0)
        counts[fname] = n
    print(f"{lang}: {len(names)} of {len(en)} names; names swapped in {counts}")
sys.exit(1 if bad else 0)
