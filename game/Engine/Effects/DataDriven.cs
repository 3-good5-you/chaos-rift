using System.Text.Json;

namespace PoC.Engine;

// Layer 1: cards fully described by numbers in effects.json / rule_tables.json
// (Equip Spells, Field Spells, attribute boosters).
public static partial class Scripts
{
    static readonly HashSet<string> Attributes = new() { "LIGHT", "DARK", "WATER", "FIRE", "EARTH", "WIND" };

    static bool Fits(Duel d, Card m, string? sel) =>
        sel == null || (Attributes.Contains(sel) ? d.Attr(m) == sel : d.Race(m) == sel);

    static void DataDriven(CardDb db)
    {
        foreach (var e in db.Effects.Values)
        {
            if (!db.EffectValues.TryGetValue(e.KonamiId.ToString(), out var v)) continue;
            if (!db.ByKonami.ContainsKey(e.KonamiId)) continue;
            var name = db.ByKonami[e.KonamiId][0].Name;

            if (e.Trig.StartsWith("equip spell") && v.TryGetProperty("atk", out var atkEl) && atkEl.ValueKind == JsonValueKind.Number)
            {
                int atk = atkEl.GetInt32();
                int def = v.TryGetProperty("def", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetInt32() : 0;
                string? sel = v.TryGetProperty("applies_to", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString() : null;
                // "(FIRE only)" / "(Warrior only)" in the trigger text when there is no applies_to.
                var m = System.Text.RegularExpressions.Regex.Match(e.Trig, @"\((\S+) only\)");
                if (sel == null && m.Success) sel = m.Groups[1].Value;
                Equip(name, atk, def, sel);
            }
            else if (e.Trig == "field spell")
            {
                var s = S(name);
                var bonus = new List<(string sel, int atk, int def, bool defPos)>();
                foreach (var p in v.EnumerateObject())
                {
                    if (p.Value.ValueKind == JsonValueKind.Number) bonus.Add((p.Name, p.Value.GetInt32(), p.Value.GetInt32(), false));
                    else if (p.Name == "defense_position") bonus.Add(("", 0, p.Value.GetProperty("def").GetInt32(), true));
                    else bonus.Add((p.Name, p.Value.GetProperty("atk").GetInt32(), p.Value.GetProperty("def").GetInt32(), false));
                }
                s.Stats = (x, c, st) =>
                {
                    foreach (var b in bonus)
                    {
                        if (b.defPos) { if (c.Defense) st.Add(0, b.def); }
                        else if (Attributes.Contains(b.sel) ? st.Attr == b.sel : st.Race == b.sel) st.Add(b.atk, b.def);
                    }
                };
            }
            else if (e.Trig == "continuous (monster)")
            {
                var map = v.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt32());
                S(name).Stats = (x, c, st) => { if (map.TryGetValue(st.Attr, out var n)) st.Add(n, 0); };
            }
        }
    }

    /// Equip Spell giving flat ATK/DEF to a monster matching `sel` (race, attribute, or null = any).
    internal static Script Equip(string name, int atk, int def, string? sel = null)
    {
        var s = S(name);
        if (sel != null && sel.Contains('/'))
        {
            var names = sel.Split('/', StringSplitOptions.TrimEntries);
            s.EquipOk = (x, m) => names.Contains(m.Name);
        }
        else if (sel != null && sel.StartsWith("non-"))
        {
            var not = sel[4..];
            s.EquipOk = (x, m) => !Fits(x.D, m, not);
        }
        else s.EquipOk = (x, m) => Fits(x.D, m, sel);
        s.Stats = (x, c, st) => { if (x.Self.EquippedTo == c) st.Add(atk, def); };
        return s;
    }
}
