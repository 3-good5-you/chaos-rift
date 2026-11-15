"""Merge prompts/batch_NN.<lang>.json into game/Godot/i18n/duel.<lang>.json ({English template: translation}).
Drops (and lists) any translation whose {n} placeholders differ from its template. Exit 1 if anything is missing."""
import json, os, re, glob, sys
here = os.path.dirname(os.path.abspath(__file__))
src, dest = os.path.join(here, "prompts"), os.path.join(here, "..", "..", "Godot", "i18n")
en = {}
for f in glob.glob(os.path.join(src, "batch_*.en.json")): en.update(json.load(open(f, encoding="utf-8")))
holes = lambda s: sorted(re.findall(r"\{\d+\}", s))
bad = 0
for lang in sys.argv[1:] or ["es", "ar"]:
    tr = {}
    for f in glob.glob(os.path.join(src, f"batch_*.{lang}.json")):
        try: tr.update(json.load(open(f, encoding="utf-8")))
        except json.JSONDecodeError as e: print(f"  skipped unfinished {os.path.basename(f)}: {e}")
    out, broken = {}, []
    for k, t in en.items():
        v = tr.get(k, "").strip()
        if not v: continue
        if holes(t) != holes(v): broken.append(k); continue
        out[t] = v
    missing = len(en) - len(out)
    print(f"{lang}: {len(out)} of {len(en)} templates, {len(broken)} dropped for placeholders {broken[:10]}")
    bad += missing
    json.dump(out, open(os.path.join(dest, f"duel.{lang}.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=0)
sys.exit(1 if bad else 0)
