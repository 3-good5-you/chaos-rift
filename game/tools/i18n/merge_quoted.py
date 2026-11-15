"""Swap the English phrases left in translated card text and duel prompts for the quoted/translations.py ones:
quoted ("Elemental HERO") and, since 2026-11-15, bare too (Which Elemental HERO? / flavor text), plus "<name> Token".
Run after merge_names.py (export_quoted.py lists what is left)."""
import json, os, re, sys
here = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(here, "quoted"))
from translations import T   # noqa: E402
dest = os.path.join(here, "..", "..", "Godot", "i18n")
code = re.compile(r"[\\\[\]|^$]|\(\?|^[a-z]")   # regexes / ids / English rule patterns the code matches, never shown
TOKEN = {"es": ("Ficha {}", "Fichas {}", "Ficha", "Fichas"), "ar": ("رمز {}", "رموز {}", "رمز", "رموز")}
for i, lang in enumerate(("es", "ar")):
    swap = {k: v[i] for k, v in T.items() if v and v[i] != k}
    bare_keys = [k for k in swap if len(k) > 1 and k not in ("roid", "LV", "NEX", "{0}")]
    names = [v for v in json.load(open(os.path.join(dest, f"names.{lang}.json"), encoding="utf-8")).values() if len(v) > 2]
    keep = set(names) - set(bare_keys)   # translated card names that hold an English word ("Buster Blader") stay whole
    alt = "|".join(map(re.escape, sorted(keep | set(bare_keys), key=len, reverse=True)))
    bare = re.compile(rf"(?<![A-Za-z\-])({alt})(?![A-Za-z])")
    known = sorted(set(names) | set(swap.values()), key=len, reverse=True)
    tok = re.compile(r"(?<![A-Za-z])(" + "|".join(map(re.escape, known)) + r") (Tokens?)(?![A-Za-z])")
    t1, tn, t1b, tnb = TOKEN[lang]
    for fname in (f"cards.{lang}.json", f"duel.{lang}.json"):
        p = os.path.join(dest, fname)
        d = json.load(open(p, encoding="utf-8")); n = 0
        for k, v in d.items():
            nv = re.sub(r'"([^"]+)"', lambda m: f'"{swap[m.group(1)]}"' if m.group(1) in swap else m.group(0), v)
            # left as the key = engine text (deck file header, rule patterns), unless the key is itself a known term
            if not code.search(k) and not k.startswith("#") and (v != k or k in swap):
                nv = bare.sub(lambda m: swap.get(m.group(1), m.group(1)), nv)
                nv = tok.sub(lambda m: (tn if m.group(2) == "Tokens" else t1).format(m.group(1)), nv)
                nv = re.sub(r"(?<![A-Za-z])Tokens(?![A-Za-z])", tnb, re.sub(r"(?<![A-Za-z])Token(?![A-Za-z])", t1b, nv))
            if lang == "ar":   # Arabic wording around the swapped-in archetype ("بطل Elemental HERO" = hero hero)
                for a, b in (("بطل البطل العنصري", "بطل عنصري"), ("أي البطل العنصري", "أي بطل عنصري"), ("لرمزين رمز ", "لرمزي ")):
                    nv = nv.replace(a, b)
            if nv != v: d[k] = nv; n += 1
        json.dump(d, open(p, "w", encoding="utf-8"), ensure_ascii=False, indent=0)
        print(f"{fname}: {n} values changed")
