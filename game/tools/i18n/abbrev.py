"""Game abbreviations in the translated text (2026-11-14, user: ATK should not stay English): ATK / DEF / LP / DP / GY
in every Spanish and Arabic value of game/Godot/i18n (UI, card text, duel prompts). Keys stay English; text inside
double quotes (card names) is left alone. Run after merge_cards.py / merge_prompts.py. Prints what it changed."""
import json, os, re, glob
here = os.path.dirname(os.path.abspath(__file__))
dest = os.path.join(here, "..", "..", "Godot", "i18n")
MAP = {
    "es": {"ATK": "ATQ", "DEF": "DEF", "LP": "PV", "DP": "PD", "GY": "Cementerio", "LV": "NV"},
    "ar": {"ATK": "هجوم", "DEF": "دفاع", "LP": "نقاط الحياة", "DP": "نقاط المبارزة", "GY": "المقبرة", "LV": "مستوى"},
}
# card-text words left in English (2026-11-15): "FLIP:", "Life Points" (Arabic sometimes glossed "(Life Points)")
PHRASE = {
    "es": [(" (Life Points)", ""), ("Life Points", "Puntos de Vida"), ("FLIP", "VOLTEO"), ("Battle Phase", "Fase de Batalla"),
           ("bonus de", "bonificación de")],
    "ar": [(" (Life Points)", ""), ("(Life Points)", ""), ("Life Points", "نقاط الحياة"), ("FLIP", "قلب")],
}
# not \b: Arabic letters are word characters, so "وDEF" has no boundary and stayed English
word = re.compile(r"(?<![A-Za-z])(ATK|DEF|LP|DP|GY)(?![A-Za-z])")
lv = re.compile(r"(?<![A-Za-z\"])LV(?![A-Za-z\d])")   # "LV" the label, never "LV5" in a card name


def fix(text, m, lang):
    parts = re.split(r'("[^"]*")', text)   # odd parts are quoted card names (none holds ATK/DEF/LP/GY as a word)
    for i in range(1, len(parts), 2): parts[i] = word.sub(lambda g: m[g.group(1)], parts[i])
    for i in range(0, len(parts), 2):
        for a, b in PHRASE[lang]: parts[i] = parts[i].replace(a, b)
        parts[i] = lv.sub(m["LV"], word.sub(lambda g: m[g.group(1)], parts[i]))
    return "".join(parts)


for lang, m in MAP.items():
    for f in glob.glob(os.path.join(dest, f"*{lang}.json")):
        if not re.search(rf"(^|[.\\/]){lang}\.json$", f): continue
        d = json.load(open(f, encoding="utf-8"))
        n = 0
        for k, v in d.items():
            nv = fix(v, m, lang)
            if nv != v: d[k] = nv; n += 1
        # UI table: the bare labels the code shows (Lang.Tr("ATK"), Labels whose text is "LP")
        if os.path.basename(f) == f"{lang}.json":
            for k in ("ATK", "DEF", "LP", "DP", "LV"): d.setdefault(k, m[k])
        json.dump(d, open(f, "w", encoding="utf-8"), ensure_ascii=False, indent=0 if "cards" in f or "duel" in f else 2)
        print(f"{os.path.basename(f)}: {n} values changed")
