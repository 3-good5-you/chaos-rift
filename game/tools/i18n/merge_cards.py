"""Merge translated batches into game/Godot/i18n/cards.<lang>.json ({idx: text}) and check them against the English:
every text present, quoted card names kept verbatim, the same numbers, Western digits only. Exit 1 on any problem."""
import json, os, re, glob, sys
here = os.path.join(os.path.dirname(os.path.abspath(__file__)), "cards")
dest = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Godot", "i18n")
idx_to_uid = json.load(open(os.path.join(here, "idx_to_uid.json"), encoding="utf-8"))
en = {}
for f in glob.glob(os.path.join(here, "batch_*.en.json")): en.update(json.load(open(f, encoding="utf-8")))
quoted = re.compile(r'"([^"]+)"')
nums = lambda s: sorted(re.findall(r"\d+", s.replace(",", "").replace(".", "")))
bad = 0
for lang in sys.argv[1:] or ["es", "ar"]:
    tr = {}
    for f in glob.glob(os.path.join(here, f"batch_*.{lang}.json")):
        try: tr.update(json.load(open(f, encoding="utf-8")))
        except json.JSONDecodeError as e: print(f"  skipped unfinished {os.path.basename(f)}: {e}")
    missing = [u for u in en if u not in tr or not tr[u].strip()]
    issues = []
    for u, t in tr.items():
        if u not in en: continue
        if sorted(quoted.findall(en[u])) != sorted(quoted.findall(t)): issues.append((u, "quoted names"))
        if nums(en[u]) != nums(t): issues.append((u, "numbers"))
        if re.search("[\u0660-\u0669\u06F0-\u06F9]", t): issues.append((u, "arabic-indic digits"))
        if lang == "ar" and not re.search("[\u0600-\u06FF]", t): issues.append((u, "not arabic"))
    print(f"{lang}: {len(tr)} translated, {len(missing)} missing, {len(issues)} flagged")
    for u, why in issues[:40]: print(f"  {u} {why}")
    if missing: print("  missing:", missing[:20])
    bad += len(missing)
    out = {i: tr[u] for i, u in sorted(idx_to_uid.items(), key=lambda kv: int(kv[0])) if u in tr}
    json.dump(out, open(os.path.join(dest, f"cards.{lang}.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=0)
    json.dump(issues, open(os.path.join(here, f"issues.{lang}.json"), "w", encoding="utf-8"), ensure_ascii=False)
sys.exit(1 if bad else 0)
