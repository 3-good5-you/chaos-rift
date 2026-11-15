"""Which English display strings in the game UI have no translation: every sentence- or label-like literal in
game/Godot/ui (labs and samples left out) that is not a key of i18n/es.json, not a duel template, not a card name.
Run after adding screens. Prints the misses grouped by file."""
import json, os, re, glob, collections, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from export_prompts import literals   # noqa: E402  (same C# literal scanner)
here = os.path.dirname(os.path.abspath(__file__))
ui_dir = os.path.join(here, "..", "..", "Godot", "ui")
i18n = os.path.join(here, "..", "..", "Godot", "i18n")
keys = set(json.load(open(os.path.join(i18n, "es.json"), encoding="utf-8"))) | set(json.load(open(os.path.join(i18n, "duel.es.json"), encoding="utf-8")))
data = os.path.join(here, "..", "..", "..", "remake", "data")
names = {c["name"] for c in json.load(open(os.path.join(data, "cards.json"), encoding="utf-8"))}
names |= {c["name"] for c in json.load(open(os.path.join(here, "..", "..", "Engine", "pool_cards.json"), encoding="utf-8"))}
skip_file = re.compile(r"Lab|samples|Sandbox|Scenario|Signature|Overlays|Fonts\.cs|Demo|Watch\.cs")
skip = re.compile(r"res://|user://|\.(png|jpg|wav|ogg|mp3|tscn|json|webp|ttf|exe|bmp|ydk|ydc)\b|^--|shader_type|uniform |^[a-z_./]+$|^#?[0-9a-f]{6}$|^[A-Z][a-z]+[A-Z]\w*$|\{\{|^\W*$|^SE_|^[A-Z]_[a-z]|https?:")
miss = collections.defaultdict(set)
for f in glob.glob(os.path.join(ui_dir, "**", "*.cs"), recursive=True):
    if skip_file.search(f): continue
    for t in literals(open(f, encoding="utf-8-sig").read()):
        t = t.replace('\\"', '"')
        if not re.search(r"[A-Z]", t) or skip.search(t) or t in keys or t in names: continue
        if len(re.findall(r"[A-Za-z]{2,}", re.sub(r"\{\d+[^}]*\}", "", t))) == 0: continue
        miss[os.path.relpath(f, ui_dir)].add(t)
for f, s in sorted(miss.items()):
    print(f"{f}:")
    for t in sorted(s): print(f"    {t!r}")
print(f"total {sum(len(s) for s in miss.values())}")
