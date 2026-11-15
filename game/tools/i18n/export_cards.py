"""Export the card text the menus show (TCG wording, else the game's own) as unique texts in batches to translate."""
import json, os, sys
data = r"D:\YI-GI-OH Power Of Chaos Remastered\remake\data"
engine = r"D:\YI-GI-OH Power Of Chaos Remastered\game\Engine"
out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "cards")
os.makedirs(out, exist_ok=True)

gods = {  # Data.cs MakeGod: these replace the blank originals
    "Obelisk the Tormentor": "Requires 3 Tributes to Normal Summon (cannot be Normal Set). This card's Normal Summon cannot be negated. When Normal Summoned, cards and effects cannot be activated. Neither player can target this card with card effects. Once per turn, during the End Phase, if this card was Special Summoned: Send it to the GY. You can Tribute 2 monsters; destroy all monsters your opponent controls. This card cannot declare an attack the turn this effect is activated.",
    "Slifer the Sky Dragon": "Requires 3 Tributes to Normal Summon (cannot be Normal Set). This card's Normal Summon cannot be negated. When Normal Summoned, cards and effects cannot be activated. Once per turn, during the End Phase, if this card was Special Summoned: Send it to the GY. Gains 1000 ATK/DEF for each card in your hand. If a monster(s) is Normal or Special Summoned to your opponent's field in Attack Position: That monster(s) loses 2000 ATK, then if its ATK has been reduced to 0 as a result, destroy it.",
    "The Winged Dragon of Ra": "Cannot be Special Summoned. Requires 3 Tributes to Normal Summon (cannot be Normal Set). This card's Normal Summon cannot be negated. When Normal Summoned, other cards and effects cannot be activated. When this card is Normal Summoned: You can pay LP so that you only have 100 left; this card gains ATK/DEF equal to the amount of LP paid. You can pay 1000 LP, then target 1 monster on the field; destroy that target.",
}
text = {}  # idx -> english
for c in json.load(open(os.path.join(data, "cards.json"), encoding="utf-8")):
    if c.get("name"): text[c["idx"]] = c.get("desc") or ""
for c in json.load(open(os.path.join(engine, "pool_cards.json"), encoding="utf-8")):
    if c.get("name"): text[c["idx"]] = c.get("desc") or ""
for k, v in json.load(open(os.path.join(data, "tcg_text.json"), encoding="utf-8")).items():
    if int(k) in text: text[int(k)] = v
names = {c["idx"]: c["name"] for c in json.load(open(os.path.join(data, "cards.json"), encoding="utf-8"))}
for i, n in names.items():
    if n in gods: text[i] = gods[n]

unique = {}   # english -> uid
idx_to_uid = {}
for i, t in sorted(text.items()):
    if not t.strip(): continue
    uid = unique.setdefault(t, f"t{len(unique):04d}")
    idx_to_uid[i] = uid
json.dump(idx_to_uid, open(os.path.join(out, "idx_to_uid.json"), "w", encoding="utf-8"))
items = [(u, t) for t, u in unique.items()]
size = int(sys.argv[1]) if len(sys.argv) > 1 else 200
for b in range(0, len(items), size):
    json.dump(dict(items[b:b + size]), open(os.path.join(out, f"batch_{b // size:02d}.en.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print(f"cards {len(idx_to_uid)}, unique texts {len(items)}, chars {sum(len(t) for _, t in items)}, batches {(len(items) + size - 1) // size}")
