"""Duel prompt templates from the engine: every sentence-like C# string literal in game/Engine (the effect scripts and
the duel), with each interpolation hole ({x.Name}, {n}) turned into {0}, {1}... Writes prompts/batch_NN.en.json
({key: template}) for translation; the game matches live prompt text against these (ui/Lang.cs Duel)."""
import os, re, json, glob, sys
here = os.path.dirname(os.path.abspath(__file__))
engine = os.path.join(here, "..", "..", "Engine")
out = os.path.join(here, "prompts")
os.makedirs(out, exist_ok=True)


def literals(src):
    """Yield (is_interpolated, template) for each "..." / $"..." literal; holes become {0}, {1}..."""
    i, n = 0, len(src)
    while i < n:
        c = src[i]
        if src.startswith("//", i): i = src.find("\n", i); i = n if i < 0 else i; continue
        if src.startswith("/*", i): i = src.find("*/", i) + 2; continue
        if c == "'":   # char literal
            j = i + 1
            while j < n and src[j] != "'": j += 2 if src[j] == "\\" else 1
            i = j + 1; continue
        if c == '"' or (c in "$@" and i + 1 < n and src[i + 1] in '"$@'):
            j = i; interp = verbatim = False
            while src[j] in "$@": interp |= src[j] == "$"; verbatim |= src[j] == "@"; j += 1
            j += 1; buf = []; hole = 0
            while j < n:
                ch = src[j]
                if ch == "\\" and not verbatim: buf.append(src[j:j + 2]); j += 2; continue
                if ch == '"':
                    if verbatim and src[j + 1:j + 2] == '"': buf.append('"'); j += 2; continue
                    break
                if interp and ch == "{":
                    if src[j + 1:j + 2] == "{": buf.append("{{"); j += 2; continue
                    depth, k = 1, j + 1
                    while k < n and depth:   # skip the hole, nested strings included
                        if src[k] == '"':
                            k += 1
                            while k < n and src[k] != '"': k += 2 if src[k] == "\\" else 1
                        elif src[k] == "{": depth += 1
                        elif src[k] == "}": depth -= 1
                        k += 1
                    buf.append("{%d}" % hole); hole += 1; j = k; continue
                if interp and ch == "}" and src[j + 1:j + 2] == "}": buf.append("}}"); j += 2; continue
                buf.append(ch); j += 1
            yield "".join(buf)
            i = j + 1; continue
        i += 1


def wanted(t):
    words = re.sub(r"\{\d+\}", "", t)
    if len(re.findall(r"[A-Za-z]{2,}", words)) < 2: return False          # needs real words around the holes
    if not re.search(r"[a-z]", words): return False                       # skip ALLCAPS keys / ids
    if re.search(r"res://|\.json|\.cs\b|^\w+:\w|=>|\bvar\b|[<>]{2}|\\n\\n", t): return False
    if re.match(r"^(sig|lan|replay|aicheck|selftest|scenario)\b", t): return False
    return True


data = os.path.join(here, "..", "..", "..", "remake", "data")
names = {c["name"] for c in json.load(open(os.path.join(data, "cards.json"), encoding="utf-8"))}
names |= {c["name"] for c in json.load(open(os.path.join(engine, "pool_cards.json"), encoding="utf-8"))}
names |= {c.get("archetype", "") for c in json.load(open(os.path.join(engine, "pool_cards.json"), encoding="utf-8"))}
temps = {}
for f in sorted(glob.glob(os.path.join(engine, "**", "*.cs"), recursive=True)):
    if any(p in f for p in ("Engine.Check", os.sep + "bin" + os.sep, os.sep + "obj" + os.sep)): continue
    src = open(f, encoding="utf-8-sig").read()
    for t in literals(src):
        t = t.replace('\\"', '"').replace("\\n", "\n")
        if wanted(t) and t not in names: temps.setdefault(t, None)   # card names stay English
items = [(f"p{k:04d}", t) for k, t in enumerate(temps)]
size = int(sys.argv[1]) if len(sys.argv) > 1 else 300
for b in range(0, len(items), size):
    json.dump(dict(items[b:b + size]), open(os.path.join(out, f"batch_{b // size:02d}.en.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print(f"templates {len(items)}, chars {sum(len(t) for _, t in items)}, with holes {sum('{0}' in t for _, t in items)}, batches {(len(items) + size - 1) // size}")
