using PoC.Engine;

namespace PoC.Check;

/// Random-ish players used to drive headless duels (not the game AI).
static class Bots
{
    static List<string>? decks;

    static List<CardData>? effectCards, normals, fusions;

    public static DuelSetup RandomSetup(Rng rng, bool chaos = false)
    {
        var db = CardDb.Shared;
        decks ??= db.DeckNames().ToList();
        var s = new DuelSetup { Seed = rng.Next(), FirstPlayer = rng.Int(2), Classic = rng.Int(4) != 0 };
        for (int p = 0; p < 2; p++)
        {
            if (chaos)
            {
                // Stress deck: 24 random effect cards + 16 small normal monsters, 6 random Fusion monsters.
                effectCards ??= db.Effects.Keys.Where(db.ByKonami.ContainsKey).Select(k => db.ByKonami[k][0])
                    .Concat(db.ByIdx.Values.Where(c => c.Modern && !c.IsNormalMonster && Scripts.IsWired(c)).GroupBy(c => c.Name).Select(g => g.First()))
                    .Where(c => !c.IsFusion).ToList();   // PoC effects and the expansion
                normals ??= db.ByIdx.Values.Where(c => c.IsNormalMonster && c.Level <= 4 && c.Atk > 0).ToList();
                fusions ??= db.ByIdx.Values.Where(c => c.IsFusion).ToList();
                for (int i = 0; i < 24; i++) s.Main[p].Add(effectCards[rng.Int(effectCards.Count)].Idx);
                for (int i = 0; i < 16; i++) s.Main[p].Add(normals[rng.Int(normals.Count)].Idx);
                for (int i = 0; i < 6; i++) s.Extra[p].Add(fusions[rng.Int(fusions.Count)].Idx);
                continue;
            }
            var (main, extra) = db.Deck(decks[rng.Int(decks.Count)]);
            s.Main[p].AddRange(main);
            s.Extra[p].AddRange(extra);
        }
        return s;
    }

    public static Command Decide(Duel d, Rng rng)
    {
        var p = d.Pending!;
        var o = p.Options;
        int Idx(Func<Opt, bool> f) => o.FindIndex(x => f(x));
        int pick;
        switch (p.Kind)
        {
            case Ask.Main:
                {
                    // Prefer doing things; end/attack phase moves are the fallback.
                    var acts = Enumerable.Range(0, o.Count).Where(i => o[i].Act is not (Act.ToBattle or Act.EndTurn or Act.ChangePos)).ToList();
                    if (acts.Count > 0 && rng.Int(10) < 7) pick = acts[rng.Int(acts.Count)];
                    else { pick = Idx(x => x.Act == Act.ToBattle); if (pick < 0 || rng.Int(10) == 0) pick = Idx(x => x.Act == Act.EndTurn); }
                    break;
                }
            case Ask.Battle:
                {
                    var atk = Enumerable.Range(0, o.Count).Where(i => o[i].Act == Act.Attack).ToList();
                    var other = Enumerable.Range(0, o.Count).Where(i => o[i].Act == Act.Activate).ToList();
                    if (other.Count > 0 && rng.Int(5) == 0) pick = other[rng.Int(other.Count)];
                    else if (atk.Count > 0 && rng.Int(10) < 8) pick = atk[rng.Int(atk.Count)];
                    else pick = Idx(x => x.Act == Act.ToMain2);
                    if (pick < 0) pick = atk[rng.Int(atk.Count)];   // a monster must attack
                    break;
                }
            case Ask.Respond:
                pick = rng.Int(3) == 0 ? rng.Int(o.Count - 1) : o.Count - 1;
                break;
            case Ask.Pick:
                {
                    int n = p.Min + rng.Int(p.Max - p.Min + 1);
                    var idx = Enumerable.Range(0, o.Count).OrderBy(_ => rng.Next()).Take(n).ToArray();
                    return new Command(p.Player, idx);
                }
            default:
                pick = rng.Int(o.Count);
                break;
        }
        return new Command(p.Player, new[] { pick });
    }
}
