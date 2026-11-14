using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

public abstract partial class RiftMenu
{
    /// Card search state: name/text query, kind, attribute, category (Normal / Effect / Fusion ... or a spell's
    /// Continuous, Equip ...) and monster type. Kept deliberately small (user 2026-11-08: no level, keep it clean).
    public sealed class CardQuery
    {
        public string Text = "";
        public Kind? Kind;
        public string? Attribute, Frame, Race;
        public int Extra => (Attribute != null ? 1 : 0) + (Frame != null ? 1 : 0) + (Race != null ? 1 : 0);
        public bool Match(CardData d) =>
            (Text.Length == 0 || d.Name.Contains(Text, StringComparison.OrdinalIgnoreCase) || d.Desc.Contains(Text, StringComparison.OrdinalIgnoreCase))
            && (Kind == null || d.Kind == Kind) && (Attribute == null || d.Attribute == Attribute) && (Frame == null || d.Frame == Frame) && (Race == null || d.Race == Race);
        public static string[] Frames(Kind? k) => k switch
        {
            PoC.Engine.Kind.Monster => new[] { "Normal", "Effect", "Fusion", "Ritual" },
            PoC.Engine.Kind.Spell => new[] { "Normal", "Continuous", "Equip", "Field", "Quick-Play", "Ritual" },
            PoC.Engine.Kind.Trap => new[] { "Normal", "Continuous", "Counter" },
            _ => new[] { "Normal", "Effect", "Fusion", "Ritual" },
        };
        public static readonly string[] Attrs = { "DARK", "LIGHT", "EARTH", "WATER", "FIRE", "WIND" };
        public static readonly string[] Races = Filters.Races;
    }

    /// Filter bar + paged card grid. One row: search · ALL / MONSTER / SPELL / TRAP · FILTERS (n) · sort. FILTERS opens
    /// one panel: attribute orbs, category words, monster type words. The mouse wheel / ‹ › turn pages.
    public sealed class CardBrowser
    {
        readonly RiftMenu m;
        readonly Rect2 area;
        readonly float tileW;
        readonly Func<List<CardData>> source;
        readonly string[] sorts;
        public readonly CardQuery Q = new();
        public int Sort, Page;
        public Action<CardData>? Picked, Hovered, Right;
        /// Extra decoration per tile (price, owned diamonds, NEW ...): (tile root, card, tile size).
        public Action<Control, CardData, Vector2>? Decorate;
        public CardData? Selected;
        /// Cards drawn faded (e.g. not owned).
        public Func<CardData, bool>? Faded;
        /// Deck Builder: a tighter bar, Monster > Spell > Trap before any sort, and clicks on release so tiles can be dragged.
        public bool Compact, GroupKinds, ReleaseClick;
        public Control Grid => grid;
        Action<int, int> setPage = (_, _) => { };
        bool open;
        public bool FiltersOpen { set => open = value; }
        Control bar = null!, grid = null!;
        List<CardData> list = new();
        Label count = null!;

        public CardBrowser(RiftMenu menu, Rect2 area, float tileW, Func<List<CardData>> source, string[]? sorts = null)
        {
            m = menu; this.area = area; this.tileW = tileW; this.source = source;
            this.sorts = sorts ?? new[] { "NAME", "ATK", "DEF", "LEVEL" };
            bar = new Control { Position = area.Position, Size = new Vector2(area.Size.X, 10), MouseFilter = MouseFilterEnum.Ignore };
            grid = new Control { MouseFilter = MouseFilterEnum.Stop };
            m.AddChild(bar); m.AddChild(grid);
            grid.GuiInput += e =>
            {
                if (e is InputEventMouseButton { Pressed: true } mb && mb.ButtonIndex is MouseButton.WheelDown or MouseButton.WheelUp) Turn(mb.ButtonIndex == MouseButton.WheelDown ? 1 : -1);
            };
        }

        // OrderBy is stable: sorting by kind last keeps the chosen order inside each kind
        IEnumerable<CardData> Sorted(IEnumerable<CardData> l) => GroupKinds ? ByChoice(l).OrderBy(c => c.Kind).ThenBy(c => c.IsFusion) : ByChoice(l);

        IEnumerable<CardData> ByChoice(IEnumerable<CardData> l) => sorts[Sort] switch
        {
            "ATK" => l.OrderByDescending(c => c.IsMonster ? c.Atk : -1).ThenBy(c => c.Name),
            "DEF" => l.OrderByDescending(c => c.IsMonster ? c.Def : -1).ThenBy(c => c.Name),
            "LEVEL" => l.OrderByDescending(c => c.IsMonster ? c.Level : -1).ThenBy(c => c.Name),
            "PRICE" => l.OrderByDescending(Shop.Price).ThenBy(c => c.Name),
            "NAME" => l.OrderBy(c => c.Name),
            _ => l,   // the source's own order (e.g. the shop's newest first)
        };

        public void Refresh(bool keepPage = false)
        {
            list = Sorted(source().Where(Q.Match)).ToList();
            if (!keepPage) Page = 0;
            BuildBar();
            BuildGrid();
        }

        int PerPage(out int cols, out float th, out float gy)
        {
            float gx = tileW * 0.16f; th = tileW * 1.458f; gy = th * 0.14f + (Decorate != null ? 18 : 0);
            cols = Math.Max(1, (int)((area.Size.X + gx) / (tileW + gx)));
            int rows = Math.Max(1, (int)((area.End.Y - grid.Position.Y + gy) / (th + gy)));
            return cols * rows;
        }

        void Turn(int d)
        {
            int pages = Math.Max(1, (list.Count + PerPage(out _, out _, out _) - 1) / PerPage(out _, out _, out _));
            int np = Math.Clamp(Page + d, 0, pages - 1);
            if (np == Page) return;
            Page = np; BuildGrid();
        }

        void BuildBar()
        {
            foreach (var c in bar.GetChildren()) c.QueueFree();
            bool dark = !m.OnLight(area.Position);
            var ink = dark ? Silver : RiftMenu.Ink; var dim = ink * new Color(1, 1, 1, 0.5f); var gold = dark ? GoldLight : GoldDeep;
            // search
            Put(bar, "⌕", Body, 24, dim, new Vector2(0, -3));
            var search = new LineEdit { Text = Q.Text, Position = new Vector2(28, -6), Size = new Vector2(Compact ? 130 : 170, 40), Flat = true, PlaceholderText = "SEARCH", ClearButtonEnabled = true };
            search.AddThemeFontOverride("font", Body); search.AddThemeFontSizeOverride("font_size", 20);
            search.AddThemeColorOverride("font_color", ink); search.AddThemeColorOverride("font_placeholder_color", dim); search.AddThemeColorOverride("caret_color", ink);
            search.TextChanged += t => { Q.Text = t.Trim(); list = Sorted(source().Where(Q.Match)).ToList(); Page = 0; BuildGrid(); count.Text = $"{list.Count:N0} CARDS"; };
            bar.AddChild(search);
            Hair(bar, new Vector2(0, 32), Compact ? 160 : 200, ink * new Color(1, 1, 1, 0.3f));
            float x = Compact ? 188 : 236;
            var kinds = new (string, Kind?)[] { ("ALL", null), ("MONSTER", PoC.Engine.Kind.Monster), ("SPELL", PoC.Engine.Kind.Spell), ("TRAP", PoC.Engine.Kind.Trap) };
            foreach (var (label, k) in kinds)
            {
                var kk = k;
                x += m.Word(bar, label, new Vector2(x, 2), 17, Q.Kind == k, () => { Q.Kind = kk; if (kk != PoC.Engine.Kind.Monster) { Q.Attribute = null; Q.Race = null; } Q.Frame = null; Refresh(); }).W + (Compact ? 14 : 24);
            }
            var ft = Put(bar, (open ? "FILTERS ▴" : "FILTERS ▾") + (Q.Extra > 0 ? $"  {Q.Extra}" : ""), Title, 16, gold, new Vector2(x + 6, 3));
            Click(ft, () => { open = !open; BuildBar(); BuildGrid(); });
            x += 6 + Title.GetStringSize(ft.Text, HorizontalAlignment.Left, -1, Z(16)).X + 26;
            var sort = Put(bar, (Compact ? "SORT: " : "") + sorts[Sort] + " ▾", Title, 16, Compact ? gold : dim, new Vector2(x, 3));
            sort.TooltipText = "Sort";
            Click(sort, () => { Sort = (Sort + 1) % sorts.Length; Refresh(); });
            float y = 48;
            if (open)
            {
                Hair(bar, new Vector2(0, y), area.Size.X, GoldC with { A = 0.45f });
                y += 18;
                if (Q.Kind is null or PoC.Engine.Kind.Monster)
                {
                    for (int i = 0; i < CardQuery.Attrs.Length; i++)
                    {
                        string at = CardQuery.Attrs[i];
                        var o = new Control { Position = new Vector2(i * 42, y - 2), Size = new Vector2(34, 34), TooltipText = at };
                        o.Draw += () => Kit.Orb(o, o.Size / 2, 15, at, Q.Attribute == null || Q.Attribute == at ? 1f : 0.28f);
                        bar.AddChild(o);
                        Click(o, () => { Q.Attribute = Q.Attribute == at ? null : at; Q.Kind ??= PoC.Engine.Kind.Monster; Refresh(); });
                    }
                    x = 6 * 42 + 30;
                }
                else x = 0;
                foreach (var f in CardQuery.Frames(Q.Kind))
                {
                    var ff = f;
                    x += m.Word(bar, f.ToUpperInvariant(), new Vector2(x, y + 4), 15, Q.Frame == f, () => { Q.Frame = Q.Frame == ff ? null : ff; Refresh(); }).W + 20;
                }
                y += 46;
                if (Q.Kind is null or PoC.Engine.Kind.Monster)
                {
                    var tl = Put(bar, "TYPE  " + (Q.Race?.ToUpperInvariant() ?? "ANY") + "  ▾", Title, 15, Q.Race != null ? gold : dim, new Vector2(0, y));
                    Click(tl, () => TypePopup(new Vector2(0, y + 30)));
                    y += 40;
                }
                Hair(bar, new Vector2(0, y), area.Size.X, GoldC with { A = 0.45f });
                y += 10;
            }
            count = Put(bar, $"{list.Count:N0} CARDS", Thin, 15, dim, new Vector2(0, y + 4));
            if (Q.Extra > 0)
            {
                // CLEAR sits on the count line, never among the filter words
                var cl = Put(bar, "CLEAR FILTERS", Title, 14, gold, new Vector2(Thin.GetStringSize(count.Text, HorizontalAlignment.Left, -1, Z(15)).X + 30, y + 4));
                Click(cl, () => { Q.Attribute = Q.Frame = Q.Race = null; Refresh(); });
            }
            if (Compact) sort.Position = new Vector2(area.Size.X - 470, y + 3);   // narrow bar: the sort sits on the count line
            setPage = Pager(bar, new Vector2(area.Size.X - 260, y), Turn, dark);
            grid.Position = new Vector2(area.Position.X, area.Position.Y + y + 40);
            grid.Size = new Vector2(area.Size.X, area.End.Y - grid.Position.Y);
        }

        /// The monster types as a small sheet under the TYPE word; picking one (or ANY) closes it.
        void TypePopup(Vector2 at)
        {
            bool dark = !m.OnLight(area.Position);
            var catcher = new Control { Size = m.GetViewportRect().Size, MouseFilter = MouseFilterEnum.Stop };
            m.AddChild(catcher);
            catcher.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true }) catcher.QueueFree(); };
            var pop = new Control { Position = bar.Position + at };
            catcher.AddChild(pop);
            const int cols = 4; float cw = 200, rh = 34;
            var names = new[] { "ANY" }.Concat(CardQuery.Races.Select(r => r.ToUpperInvariant())).ToArray();
            int rows = (names.Length + cols - 1) / cols;
            var sheet = new ColorRect { Position = new Vector2(-16, -10), Size = new Vector2(cols * cw + 20, rows * rh + 20), Color = dark ? new Color(0.05f, 0.05f, 0.07f, 0.97f) : new Color(0.97f, 0.97f, 0.96f, 0.98f), MouseFilter = MouseFilterEnum.Stop };
            pop.AddChild(sheet);
            Shadow(pop, sheet.Position, sheet.Size, 0.25f, new Color(0.1f, 0.1f, 0.12f), new Vector2(0, 8)).ShowBehindParent = true;
            Hair(pop, sheet.Position, sheet.Size.X, GoldC with { A = 0.6f });
            for (int i = 0; i < names.Length; i++)
            {
                string? race = i == 0 ? null : CardQuery.Races[i - 1];
                bool on = race == Q.Race;
                var l = Put(pop, names[i], Title, 15, on ? (dark ? GoldLight : GoldDeep) : (dark ? Silver : RiftMenu.Ink) * new Color(1, 1, 1, 0.75f), new Vector2(i % cols * cw, i / cols * rh));
                Click(l, () => { catcher.QueueFree(); Q.Race = race; if (race != null) Q.Kind ??= PoC.Engine.Kind.Monster; Refresh(); },
                    h => l.AddThemeColorOverride("font_color", h ? (dark ? GoldLight : GoldDeep) : on ? (dark ? GoldLight : GoldDeep) : (dark ? Silver : RiftMenu.Ink) * new Color(1, 1, 1, 0.75f)));
                l.Size = new Vector2(cw - 10, rh); l.MouseFilter = MouseFilterEnum.Stop;
            }
        }

        public void BuildGrid()
        {
            foreach (var c in grid.GetChildren()) c.QueueFree();
            int per = PerPage(out int cols, out float th, out float gy);
            float gx = tileW * 0.16f;
            int pages = Math.Max(1, (list.Count + per - 1) / per);
            Page = Math.Clamp(Page, 0, pages - 1);
            setPage(Page, pages);
            var slice = list.Skip(Page * per).Take(per).ToList();
            for (int i = 0; i < slice.Count; i++)
            {
                var d = slice[i];
                var at = new Vector2(i % cols * (tileW + gx), i / cols * (th + gy));
                var card = m.Card(grid, d, at + new Vector2(tileW, th) / 2, th, 0, d == Selected ? 1.3f : 0.5f);
                var hit = new Control { Position = at, Size = new Vector2(tileW, th) };
                grid.AddChild(hit);
                Decorate?.Invoke(hit, d, new Vector2(tileW, th));
                if (Faded?.Invoke(d) == true) card.Modulate = new Color(1, 1, 1, 0.42f);
                if (d == Selected) { card.Scale = new Vector2(1.07f, 1.07f); Pip(grid, at + new Vector2(tileW / 2, th + 12), 8); }
                Click(hit, () => { Selected = d; Picked?.Invoke(d); BuildGrid(); }, h =>
                {
                    if (!IsInstanceValid(card)) return;   // the grid was rebuilt under the mouse
                    if (d != Selected) Ink_.Only(m, card).TweenProperty(card, "scale", h ? new Vector2(1.05f, 1.05f) : Vector2.One, 0.12);
                    if (h) Hovered?.Invoke(d);
                }, Right == null ? null : () => Right(d), ReleaseClick);
            }
            if (slice.Count == 0) Put(grid, "NO CARDS MATCH", Title, 22, m.InkAt(grid.Position + new Vector2(10, 10)) * new Color(1, 1, 1, 0.5f), new Vector2(0, 40));
        }

        public int Count => list.Count;
    }
}
