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
            (Text.Length == 0 || d.Name.Contains(Text, StringComparison.OrdinalIgnoreCase) || Lang.Tr(d.Name).Contains(Text, StringComparison.OrdinalIgnoreCase) || d.Desc.Contains(Text, StringComparison.OrdinalIgnoreCase) || Lang.Desc(d).Contains(Text, StringComparison.OrdinalIgnoreCase))
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
            int rows = Math.Max(1, (int)((grid.Size.Y + gy) / (th + gy)));
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
            // Deck Builder: the cards sit centred in their space, so a big arrow fits in the gutter on each side
            if (Compact) bar.Position = area.Position + new Vector2(Ox, 0);
            bool dark = !m.OnLight(area.Position);
            var ink = dark ? Silver : RiftMenu.Ink; var dim = ink * new Color(1, 1, 1, 0.5f); var gold = dark ? GoldLight : GoldDeep;
            Label sort = null!;
            float x = 0, y = 48;
            if (Compact) y = CompactRows(ink, dim, gold);
            else
            {
                // search
                Put(bar, "⌕", Body, 24, dim, new Vector2(0, -3));
                var search = new LineEdit { Text = Q.Text, Position = new Vector2(28, -6), Size = new Vector2(170, 40), Flat = true, PlaceholderText = "SEARCH", ClearButtonEnabled = true };
                search.AddThemeFontOverride("font", Body); search.AddThemeFontSizeOverride("font_size", 20);
                search.AddThemeColorOverride("font_color", ink); search.AddThemeColorOverride("font_placeholder_color", dim); search.AddThemeColorOverride("caret_color", ink);
                search.TextChanged += t => { Q.Text = t.Trim(); list = Sorted(source().Where(Q.Match)).ToList(); Page = 0; BuildGrid(); count.Text = Lang.F("{0:N0} CARDS", list.Count); };
                bar.AddChild(search);
                Hair(bar, new Vector2(0, 32), 200, ink * new Color(1, 1, 1, 0.3f));
                x = 236;
                var kinds = new (string, Kind?)[] { ("ALL", null), ("MONSTER", PoC.Engine.Kind.Monster), ("SPELL", PoC.Engine.Kind.Spell), ("TRAP", PoC.Engine.Kind.Trap) };
                // longer translated words: the kind row shrinks until it is no wider than the English one, so FILTERS
                // stays on this side of the rift
                float Row(Func<string, string> tr, int size) => kinds.Sum(kv => Title.GetStringSize(tr(kv.Item1), HorizontalAlignment.Left, -1, Z(size)).X + 24);
                int ks = 17;
                while (ks > 12 && Row(Lang.Tr, ks) > Row(s => s, 17) + 8) ks--;
                foreach (var (label, k) in kinds)
                {
                    var kk = k;
                    x += m.Word(bar, label, new Vector2(x, 2), ks, Q.Kind == k, () => { Q.Kind = kk; if (kk != PoC.Engine.Kind.Monster) { Q.Attribute = null; Q.Race = null; } Q.Frame = null; Refresh(); }).W + 24;
                }
                var ft = Put(bar, Lang.Tr("FILTERS") + (open ? " ▴" : " ▾") + (Q.Extra > 0 ? $"  {Q.Extra}" : ""), Title, 16, gold, new Vector2(x + 6, 3));
                Click(ft, () => { open = !open; BuildBar(); BuildGrid(); });
                x += 6 + Title.GetStringSize(Lang.Tr(ft.Text), HorizontalAlignment.Left, -1, Z(16)).X + 26;
                sort = Put(bar, Lang.Tr(sorts[Sort]) + " ▾", Title, 16, dim, new Vector2(x, 3));
                sort.TooltipText = "Sort";
                Click(sort, () => { Sort = (Sort + 1) % sorts.Length; Refresh(); });
            }
            x = 0;
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
                    x += m.Word(bar, Lang.Tr(f).ToUpperInvariant(), new Vector2(x, y + 4), 15, Q.Frame == f, () => { Q.Frame = Q.Frame == ff ? null : ff; Refresh(); }).W + 20;
                }
                y += 46;
                if (Q.Kind is null or PoC.Engine.Kind.Monster)
                {
                    // a small capsule so it reads as a drop-down (Menu Lab pick 2026-11-16)
                    string label = Lang.Tr("TYPE") + ":  " + Lang.Tr(Q.Race ?? "ANY").ToUpperInvariant() + "   ▾";
                    float lw = Title.GetStringSize(label, HorizontalAlignment.Left, -1, Z(14)).X + 36;
                    var cap = new Panel { Position = new Vector2(0, y - 4), Size = new Vector2(lw, 36) };
                    cap.AddThemeStyleboxOverride("panel", Box(Q.Race != null ? (dark ? GoldC : RiftMenu.Ink) : new Color(0, 0, 0, 0), dark ? Silver * new Color(1, 1, 1, 0.4f) : RiftMenu.Ink * new Color(1, 1, 1, 0.35f), Q.Race != null ? 0 : 1.5f, 99));
                    bar.AddChild(cap);
                    var tl = Put(cap, label, Title, 14, Q.Race != null ? (dark ? RiftMenu.Ink : GoldLight) : ink, new Vector2(0, Z(14) * 0.12f), cap.Size, HorizontalAlignment.Center);
                    tl.VerticalAlignment = VerticalAlignment.Center;
                    float ty = y;
                    Click(cap, () => TypePopup(new Vector2(0, ty + 46)));
                    y += 46;
                }
                Hair(bar, new Vector2(0, y), area.Size.X, GoldC with { A = 0.45f });
                y += 10;
            }
            if (!Compact) CountLine(sort, y, dim, gold);
            grid.Position = new Vector2(area.Position.X + (Compact ? Ox : 0), area.Position.Y + y + (Compact ? 16 : 40));
            grid.Size = new Vector2(Compact ? GridW : area.Size.X, area.End.Y - grid.Position.Y - (Compact ? 44 : 0));
            // Deck Builder: big rift arrows on the grid's edges, the page counter centred under it (user 2026-11-17);
            // elsewhere the small pager on the count line
            setPage = Compact ? EdgePager() : Pager(bar, new Vector2(area.Size.X - 260, y), Turn, dark);
        }

        /// Gap left of (and right of) the centred grid.
        float Ox => (area.Size.X - GridW) / 2;

        Action<int, int> EdgePager()
        {
            // level with the page counter, at the grid's two edges (user 2026-11-17); Arabic letters sit lower in the line
            float cy = area.Size.Y - 16 + (Lang.Code == "ar" ? 2 : -4);   // the right arrow is the next page in every language
            const float w = 24, h = 56;
            Control Arrow(int dir, float cx)
            {
                var c = new Control { Position = new Vector2(cx - w / 2, cy - h / 2), Size = new Vector2(w, h), PivotOffset = new Vector2(w, h) / 2, MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
                bool hov = false;
                c.Draw += () => DrawEdgeArrow(c, dir, w, h, hov);
                bar.AddChild(c);
                Click(c, () => Turn(dir), on => { hov = on; c.QueueRedraw(); Ink_.Only(m, c).TweenProperty(c, "scale", on ? new Vector2(1.08f, 1.08f) : Vector2.One, 0.14); });
                return c;
            }
            var left = Arrow(-1, w / 2); var right = Arrow(1, GridW - w / 2);
            var (prev, next) = (left, right);
            var text = Put(bar, "", Title, 17, RiftMenu.Ink, new Vector2(0, area.Size.Y - 28), new Vector2(GridW, 24), HorizontalAlignment.Center);
            return (page, pages) =>
            {
                left.Visible = right.Visible = text.Visible = pages > 1;
                text.Text = Lang.F("PAGE {0} / {1}", page + 1, pages);
                prev.Modulate = new Color(1, 1, 1, page > 0 ? 1 : 0.2f); next.Modulate = new Color(1, 1, 1, page < pages - 1 ? 1 : 0.2f);
            };
        }

        void CountLine(Label sort, float y, Color dim, Color gold)
        {
            count = Put(bar, Lang.F("{0:N0} CARDS", list.Count), Thin, 15, dim, new Vector2(0, y + 4));
            if (Q.Extra > 0)
            {
                // CLEAR sits on the count line, never among the filter words
                var cl = Put(bar, "CLEAR FILTERS", Title, 14, gold, new Vector2(Thin.GetStringSize(Lang.Tr(count.Text), HorizontalAlignment.Left, -1, Z(15)).X + 30, y + 4));
                Click(cl, () => { Q.Attribute = Q.Frame = Q.Race = null; Refresh(); });
            }
            float sortEnd = sort.Position.X + Title.GetStringSize(sort.Text, HorizontalAlignment.Left, -1, Z(16)).X;
            if (sortEnd > area.Size.X) sort.Position = new Vector2(area.Size.X - 290 - (sortEnd - sort.Position.X), y + 3);   // longer translated words: same, left of the pager
        }

        /// Width the grid's columns really use (the bar lines up with it).
        float GridW { get { float gx = tileW * 0.16f; int cols = Math.Max(1, (int)((area.Size.X + gx) / (tileW + gx))); return cols * (tileW + gx) - gx; } }

        /// Deck Builder's bar (user 2026-11-17: the loose words read messy), two rows lined up with the grid: a search
        /// capsule with the card count inside + a FILTERS capsule; then ALL / MONSTER / SPELL / TRAP as one segmented
        /// capsule (the chosen kind filled ink) with CLEAR FILTERS and the sort on the right. Returns where the bar ends.
        float CompactRows(Color ink, Color dim, Color gold)
        {
            const float h = 44, h2 = 40, y2 = 52;
            Panel Cap(float x, float y, float w, float hh, bool filled)
            {
                var p = new Panel { Position = new Vector2(x, y), Size = new Vector2(w, hh), MouseFilter = MouseFilterEnum.Pass };
                p.AddThemeStyleboxOverride("panel", Box(filled ? RiftMenu.Ink : new Color(1, 1, 1, 0.55f), ink * new Color(1, 1, 1, 0.28f), filled ? 0 : 1.5f, (int)(hh / 2)));
                bar.AddChild(p);
                return p;
            }
            Label Text(Control host, string t, Font f, int size, Color c, Vector2 at, Vector2 box, HorizontalAlignment al = HorizontalAlignment.Center)
            {
                var l = Put(host, t, f, size, c, at + new Vector2(0, Z(size) * 0.12f), box, al);
                l.VerticalAlignment = VerticalAlignment.Center;
                return l;
            }
            // measured from real labels (tracked title font)
            float Wid(string t, int size, Font? f = null) { var l = Put(bar, t, f ?? Title, size, ink, Vector2.Zero); float w = l.GetMinimumSize().X; l.Free(); return w; }
            float right = GridW;
            // row 1: FILTERS at the right end, search fills the rest
            string ftText = Lang.Tr("FILTERS") + (open ? " ▴" : " ▾") + (Q.Extra > 0 ? $"  {Q.Extra}" : "");
            float fw = Wid(ftText, 15) + 44;
            bool lit = open || Q.Extra > 0;
            var fp = Cap(right - fw, -6, fw, h, lit);
            Text(fp, ftText, Title, 15, lit ? GoldLight : ink, Vector2.Zero, fp.Size);
            Click(fp, () => { open = !open; BuildBar(); BuildGrid(); });
            float sw = right - fw - 14;
            var sp = Cap(0, -6, sw, h, false);
            Put(sp, "⌕", Body, 22, dim, new Vector2(18, 6));
            string cnt = Lang.F("{0:N0} CARDS", list.Count);
            float cw = Wid(cnt, 14, Thin) + 30;
            count = Text(sp, cnt, Thin, 14, dim, new Vector2(sw - cw - 18, 0), new Vector2(cw, h), HorizontalAlignment.Right);
            var search = new LineEdit { Text = Q.Text, Position = new Vector2(44, 2), Size = new Vector2(sw - 44 - cw - 24, h - 4), Flat = true, PlaceholderText = "SEARCH", ClearButtonEnabled = true };
            search.AddThemeFontOverride("font", Body); search.AddThemeFontSizeOverride("font_size", 18);
            search.AddThemeColorOverride("font_color", ink); search.AddThemeColorOverride("font_placeholder_color", dim); search.AddThemeColorOverride("caret_color", ink);
            search.TextChanged += t => { Q.Text = t.Trim(); list = Sorted(source().Where(Q.Match)).ToList(); Page = 0; BuildGrid(); count.Text = Lang.F("{0:N0} CARDS", list.Count); };
            sp.AddChild(search);
            // row 2, right: sort (and CLEAR FILTERS left of it)
            var kinds = new (string, Kind?)[] { ("ALL", null), ("MONSTER", PoC.Engine.Kind.Monster), ("SPELL", PoC.Engine.Kind.Spell), ("TRAP", PoC.Engine.Kind.Trap) };
            float Seg(int size) => kinds.Sum(k => Wid(Lang.Tr(k.Item1), size) + 28);
            string st = Lang.Tr("SORT:") + " " + Lang.Tr(sorts[Sort]) + " ▾";
            if (Seg(12) + 8 + Wid(st, 14) + 24 + (Q.Extra > 0 ? Wid(Lang.Tr("CLEAR FILTERS"), 13) + 24 : 0) > right) st = Lang.Tr(sorts[Sort]) + " ▾";
            float stw = Wid(st, 14);
            var sort = Text(bar, st, Title, 14, gold, new Vector2(right - stw, y2), new Vector2(stw, h2), HorizontalAlignment.Right);
            sort.MouseFilter = MouseFilterEnum.Stop; sort.TooltipText = "Sort";
            Click(sort, () => { Sort = (Sort + 1) % sorts.Length; Refresh(); });
            float used = stw + 24;
            if (Q.Extra > 0)
            {
                float clw = Wid(Lang.Tr("CLEAR FILTERS"), 13);
                var cl = Text(bar, "CLEAR FILTERS", Title, 13, gold, new Vector2(right - used - clw, y2), new Vector2(clw, h2));
                cl.MouseFilter = MouseFilterEnum.Stop;
                Click(cl, () => { Q.Attribute = Q.Frame = Q.Race = null; Refresh(); });
                used += clw + 24;
            }
            // row 2, left: the kinds in one segmented capsule, words shrink to fit (longer translations)
            int ks = 14;
            while (ks > 10 && Seg(ks) + 8 > right - used) ks--;
            var seg = Cap(0, y2, Seg(ks) + 8, h2, false);
            float x = 4;
            foreach (var (label, k) in kinds)
            {
                var kk = k; bool on = Q.Kind == k;
                float w = Wid(Lang.Tr(label), ks) + 28;
                var cell = new Panel { Position = new Vector2(x, 4), Size = new Vector2(w, h2 - 8), MouseFilter = MouseFilterEnum.Pass };
                cell.AddThemeStyleboxOverride("panel", on ? Box(RiftMenu.Ink, default, 0, (int)(h2 / 2 - 4)) : new StyleBoxEmpty());
                seg.AddChild(cell);
                var l = Text(cell, label, Title, ks, on ? GoldLight : ink * new Color(1, 1, 1, 0.6f), Vector2.Zero, cell.Size);
                Click(cell, () => { Q.Kind = kk; if (kk != PoC.Engine.Kind.Monster) { Q.Attribute = null; Q.Race = null; } Q.Frame = null; Refresh(); },
                    hv => { if (!on) l.AddThemeColorOverride("font_color", ink * new Color(1, 1, 1, hv ? 1f : 0.6f)); });
                x += w;
            }
            return y2 + h2 + 14;
        }

        /// The monster types under the TYPE capsule (Menu Lab pick 2026-11-16): a soft rounded sheet with a notch, "Any
        /// type" across the top, two columns, the chosen type ticked; the grid behind fades. Picking one closes it.
        void TypePopup(Vector2 at)
        {
            bool dark = !m.OnLight(area.Position);
            var ink = dark ? Silver : RiftMenu.Ink; var gold = dark ? GoldLight : GoldDeep; var paper = dark ? new Color(0.07f, 0.07f, 0.09f, 0.98f) : Colors.White;
            var catcher = new Control { Size = m.GetViewportRect().Size, MouseFilter = MouseFilterEnum.Stop };
            m.AddChild(catcher);
            catcher.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true }) catcher.QueueFree(); };
            catcher.AddChild(new ColorRect { Position = grid.Position, Size = grid.Size, Color = dark ? new Color(0.02f, 0.02f, 0.03f, 0.6f) : new Color(0.97f, 0.97f, 0.96f, 0.6f), MouseFilter = MouseFilterEnum.Ignore });
            var pop = new Control { Position = bar.Position + at };
            catcher.AddChild(pop);
            var races = CardQuery.Races;
            var pool = source().Where(c => c.IsMonster).ToList();
            const float cw = 250, rh = 40;
            int rows = (races.Length + 1) / 2;
            float pw = 2 * cw + 28, ph = rows * rh + 100;
            var sheet = new Panel { Position = new Vector2(-14, 0), Size = new Vector2(pw, ph), MouseFilter = MouseFilterEnum.Stop };
            sheet.AddThemeStyleboxOverride("panel", Box(paper, dark ? new Color(1, 1, 1, 0.1f) : new Color(0, 0, 0, 0.07f), 1, 18, 34, new Color(0.05f, 0.05f, 0.1f, dark ? 0.5f : 0.25f), new Vector2(0, 12)));
            pop.AddChild(sheet);
            pop.AddChild(new Polygon2D { Polygon = new[] { new Vector2(30, 1), new Vector2(42, -11), new Vector2(54, 1) }, Color = paper });
            Put(pop, "MONSTER TYPE", Thin, 13, ink * new Color(1, 1, 1, 0.6f), new Vector2(10, 18));
            var close = Put(pop, "✕", Body, 18, ink * new Color(1, 1, 1, 0.6f), new Vector2(pw - 60, 12), new Vector2(36, 28), HorizontalAlignment.Center);
            close.MouseFilter = MouseFilterEnum.Stop;
            Click(close, () => catcher.QueueFree());
            void Row(Rect2 r, string text, string? race, int count)
            {
                bool on = race == Q.Race;
                var hl = new Panel { Position = r.Position, Size = r.Size };
                var off = Box(on ? GoldC with { A = 0.2f } : new Color(0, 0, 0, 0), default, 0, 10);
                hl.AddThemeStyleboxOverride("panel", off);
                pop.AddChild(hl);
                var l = Put(hl, Lang.Tr(text), Body, 17, on ? gold : ink, new Vector2(14, 0), new Vector2(r.Size.X - 70, r.Size.Y));
                l.VerticalAlignment = VerticalAlignment.Center;
                var n = Put(hl, count.ToString(), Body, 15, ink * new Color(1, 1, 1, 0.5f), Vector2.Zero, new Vector2(r.Size.X - (on ? 40 : 14), r.Size.Y), HorizontalAlignment.Right);
                n.VerticalAlignment = VerticalAlignment.Center;
                if (on) { var t = Put(hl, "✓", Body, 17, gold, new Vector2(r.Size.X - 34, 0), new Vector2(28, r.Size.Y), HorizontalAlignment.Center); t.VerticalAlignment = VerticalAlignment.Center; }
                Click(hl, () => { catcher.QueueFree(); Q.Race = race; if (race != null) Q.Kind ??= PoC.Engine.Kind.Monster; Refresh(); },
                    h => { if (!on) hl.AddThemeStyleboxOverride("panel", h ? Box(dark ? new Color(1, 1, 1, 0.06f) : new Color(0, 0, 0, 0.05f), default, 0, 10) : off); });
            }
            Row(new Rect2(0, 48, 2 * cw, rh - 4), "Any type", null, pool.Count);
            Hair(pop, new Vector2(4, 48 + rh + 2), 2 * cw - 8, ink * new Color(1, 1, 1, 0.08f), 1);
            for (int i = 0; i < races.Length; i++)
                Row(new Rect2(i / rows * cw, 52 + rh + i % rows * rh, cw - 8, rh - 4), races[i], races[i], pool.Count(c => c.Race == races[i]));
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
