"""How many live duel prompts the templates catch. Feed it a card-sweep log made with LABSWEEP_LOG=1
(`--labsweep`): every logged prompt text is matched the way ui/Lang.cs Duel does (exact, then templates with holes,
longest first). Prints coverage and the most common prompts nothing matches."""
import json, os, re, glob, sys, collections
here = os.path.dirname(os.path.abspath(__file__))
en = {}
for f in glob.glob(os.path.join(here, "prompts", "batch_*.en.json")): en.update(json.load(open(f, encoding="utf-8")))
ui = set(json.load(open(os.path.join(here, "..", "..", "Godot", "i18n", "es.json"), encoding="utf-8")))
exact = {t for t in en.values() if "{0}" not in t} | ui
holes = [re.compile("^" + re.sub(r"\\\{(\d+)\\\}", "(.+?)", re.escape(t)) + "$", re.S)
         for t in sorted((t for t in en.values() if "{0}" in t), key=len, reverse=True)]
texts = collections.Counter(m.group(1) for line in open(sys.argv[1], encoding="utf-8", errors="replace")
                            if (m := re.search(r"^\s+t\d+ \S+ p\d \w+ '(.*)' win=", line)))
miss = collections.Counter({t: n for t, n in texts.items() if t and t not in exact and not any(h.match(t) for h in holes)})
total, missed = sum(texts.values()), sum(miss.values())
print(f"prompts seen {total} ({len(texts)} distinct); matched {total - missed} = {100 * (total - missed) / max(total, 1):.1f}%")
for t, n in miss.most_common(40): print(f"  {n:4d}  {t}")
