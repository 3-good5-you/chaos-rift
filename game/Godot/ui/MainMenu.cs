using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Main menu (Chaos Rift, locked 2026-11-08): light left, dark right, torn by the rift. The logo top right; the menu on
/// the crack (graphite words on the light side, silver on the dark, gold with two diamonds when focused); two cards
/// drawn at random from the whole pool each time the menu opens, one on each side; your profile bottom left (click it
/// for Options). The first time the menu opens in a session the loading intro plays: the Yu-Gi-Oh! logo in the void,
/// "Chaos Rift" rises, the rift tears down the screen, the light floods in, the logo morphs to the top right.
/// `-- --intro` forces the intro; `-- --seed=N` fixes the two cards (screenshots).
public partial class MainMenu : RiftMenu
{
	/// Holo-foil shimmer over a rival's card art (rival select, LAN lobby).
	public const string Foil = @"shader_type canvas_item;
uniform vec4 tint : source_color = vec4(0.4, 0.15, 0.6, 1.0);
uniform float phase = 0.0;
vec3 hue(float h) { return clamp(abs(fract(h + vec3(0.0, 0.6667, 0.3333)) * 6.0 - 3.0) - 1.0, 0.0, 1.0); }
float hash(vec2 p) { return fract(sin(dot(p, vec2(41.3, 289.1))) * 43758.5); }
void fragment() {
  vec2 px = TEXTURE_PIXEL_SIZE * 0.7;
  vec3 c = texture(TEXTURE, UV).rgb * 0.4
         + (texture(TEXTURE, UV + vec2(px.x, 0)).rgb + texture(TEXTURE, UV - vec2(px.x, 0)).rgb
          + texture(TEXTURE, UV + vec2(0, px.y)).rgb + texture(TEXTURE, UV - vec2(0, px.y)).rgb) * 0.15;
  c = mix(c, tint.rgb * dot(c, vec3(0.33)) * 1.6, 0.22) * 0.78;
  float t = TIME * 0.12 + phase;
  float band = exp(-pow((UV.x + UV.y * 0.45 - (fract(t) * 2.4 - 0.6)) * 7.0, 2.0));
  c += hue(UV.x * 0.9 + UV.y * 0.6 + TIME * 0.04 + phase) * (0.07 + band * 0.25);
  c += vec3(1.0, 0.97, 0.9) * band * 0.22;
  vec2 cell = floor(UV * vec2(26.0, 34.0)) + vec2(17.31, 9.73);
  float tw = pow(max(0.0, sin(TIME * (1.5 + hash(cell) * 2.0) + hash(cell + 3.1) * 6.28)), 24.0) * step(0.86, hash(cell + 7.7));
  vec2 f = fract(UV * vec2(26.0, 34.0)) - 0.5;
  tw *= smoothstep(0.02, 0.08, UV.y);
  c += vec3(1.0) * tw * smoothstep(0.12, 0.0, min(abs(f.x), abs(f.y))) * smoothstep(0.5, 0.1, length(f));
  c = mix(c, tint.rgb * 0.22, smoothstep(0.5, 1.0, UV.y));
  c *= smoothstep(-0.1, 0.25, UV.y) * 0.4 + 0.6;
  COLOR = vec4(c, 1.0);
}";

	static bool introPlayed;
	/// The menu entries; `-- --modes` previews the menu with CHALLENGES added (game-mode samples, 2026-11-08).
	static readonly string[] Menu = OS.GetCmdlineUserArgs().Contains("--modes")
		? new[] { "Duel", "Tournament", "Challenges", "LAN Duel", "Deck Builder", "Card List", "Shop", "Options", "Quit" }
		: new[] { "Duel", "Tournament", "LAN Duel", "Replays", "Deck Builder", "Card List", "Shop", "Options", "Quit" };
	protected override int ItemCount => Menu.Length;
	const int MenuSize = 30;
	readonly List<(Label L, float Y)> rows = new();
	readonly ColorRect[] pips = new ColorRect[2];
	TextureRect logoPic = null!, namePic = null!;
	Rect2 logoHome, nameHome;
	ColorRect bgRect = null!;

	protected override string? MusicPath => Md("menu_01");
	protected override void OnBack() { }

	protected override void Build()
	{
		Demo.Ensure();
		RenderingServer.SetDefaultClearColor(new Color(0.008f, 0.008f, 0.012f));
		Session.ClearMode();   // back at the menu: no mode left over (a puzzle, a chaos duel ...)   // the intro's shake shows the clear colour at the edges
		Rift(0.58f, 0.44f);
		bgRect = (ColorRect)GetChild(GetChildCount() - 1);
		Logo();
		float step = Menu.Length > 8 ? 0.062f : 0.068f, y0 = Menu.Length > 8 ? 0.36f : 0.39f;
		for (int i = 0; i < Menu.Length; i++)
		{
			float y = y0 + i * step;
			float cx = Mathf.Lerp(Top, Bottom, y) * W;
			var l = T(this, Menu[i].ToUpperInvariant(), Title, MenuSize, new Vector2(cx - 400, y * H - MenuSize * 0.75f), new Vector2(800, MenuSize * 1.5f), HorizontalAlignment.Center, 0.9f);
			float tw = Title.GetStringSize(l.Text, HorizontalAlignment.Left, -1, MenuSize).X;
			l.Position = new Vector2(cx - tw / 2 - 20, l.Position.Y); l.Size = new Vector2(tw + 40, l.Size.Y);   // hover area = the word
			Hook(l, i);
			rows.Add((l, y));
		}
		Profile();

		bool intro = OS.GetCmdlineUserArgs().Contains("--intro") || !introPlayed && !Shooting && !OS.GetCmdlineUserArgs().Contains("--nointro");
		introPlayed = true;
		if (intro) Intro(); else RandomPair();
		GetTree().CreateTimer(intro ? 4.6 : 0.6).Timeout += () => { if (Focus < 0) SetFocus(0); };
		Updater.Check(this, v => GetTree().CreateTimer(intro ? 5.0 : 0.5).Timeout += () => { if (starter == null) Ask($"Chaos Rift {v} is out (you have {Updater.Current}).", ("UPDATE", () => { CloseToast(); OS.ShellOpen(Updater.Page); }), ("LATER", CloseToast)); });
		if (Session.NeedsStarter && (!Shooting || OS.GetCmdlineUserArgs().Contains("--starter"))) GetTree().CreateTimer(intro ? 5.0 : 1.0).Timeout += StarterPick;
	}

	// ---------------------------------------------------------------- pieces

	void Logo()
	{
		logoPic = Pic(this, GD.Load<Texture2D>("res://assets/title/rift/R0_logo.png"), Vector2.Zero, Vector2.One);
		namePic = Pic(this, GD.Load<Texture2D>("res://assets/title/rift/R0_name.png"), Vector2.Zero, Vector2.One);
		float lw = 560, lh = lw * logoPic.Texture.GetHeight() / logoPic.Texture.GetWidth();
		float nw = lw * 0.8f, nh = nw * namePic.Texture.GetHeight() / namePic.Texture.GetWidth();
		float cx = W - 360;
		logoHome = new Rect2(cx - lw / 2, 30, lw, lh);
		nameHome = new Rect2(cx - nw / 2, 30 + lh * 0.86f, nw, nh);
		Place(logoPic, logoHome); Place(namePic, nameHome);
	}
	static void Place(Control c, Rect2 r) { c.Position = r.Position; c.Size = r.Size; }

	/// Your picture and name bottom left; click for Options (name, picture, frame live there).
	void Profile()
	{
		var at = new Vector2(56, H - 112);
		var root = new Control { Position = at, Size = new Vector2(420, 80), TooltipText = "Profile and options" };
		AddChild(root);
		root.AddChild(Cosmetics.Framed(Avatar, P.Frame, 70));
		var name = Put(root, Settings.PlayerName.ToUpperInvariant(), Title, 22, Ink, new Vector2(84, 10));
		Put(root, $"{P.Dp:N0} DP", Thin, 17, Ink * new Color(1, 1, 1, 0.55f), new Vector2(85, 42));
		Click(root, () => Transition.Go(this, "res://ui/Options.tscn"), h => name.AddThemeColorOverride("font_color", h ? GoldDeep : Ink));
	}

	/// Two cards drawn at random from every card in the game, one on each side; they stay put while you browse.
	void RandomPair()
	{
		var pool = Db.ByIdx.Values.Where(c => Db.Canon(c.Idx) == c.Idx && c.Name.Length > 0 && !c.Name.EndsWith("Token")).GroupBy(c => c.Name).Select(g => g.First()).ToList();
		var rng = new Random(int.TryParse(Demo.Arg("seed"), out var sd) ? sd : System.Environment.TickCount);
		foreach (var (d, at, tilt) in new[] { (pool[rng.Next(pool.Count)], new Vector2(W * 0.2f, H * 0.54f), -0.055f), (pool[rng.Next(pool.Count)], new Vector2(W * 0.8f, H * 0.6f), 0.055f) })
		{
			var c = Card(this, d, at, H * 0.56f, tilt, 1f, large: true);
			MoveChild(c, bgRect.GetIndex() + 1);
			c.Modulate = new Color(1, 1, 1, 0);
			var tw = CreateTween().SetParallel();
			tw.TweenProperty(c, "modulate:a", 1f, 0.35);
			tw.TweenProperty(c, "position:y", c.Position.Y - 12, 0.5).From(c.Position.Y + 22).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		}
	}

	protected override void OnFocus(int old, int now)
	{
		for (int i = 0; i < rows.Count; i++) rows[i].L.Mat().P("lit", i == now ? 1f : 0f);
		if (now < 0) return;
		var (l, y) = rows[now];
		bg.P("focus_y", y);
		float w = Title.GetStringSize(l.Text, HorizontalAlignment.Left, -1, MenuSize).X;
		float cx = Mathf.Lerp(Top, Bottom, y) * W;
		for (int s = 0; s < 2; s++)
		{
			pips[s] ??= Pip(this, Vector2.Zero, 12);
			pips[s].Position = new Vector2(cx + (s == 0 ? -w / 2 - 30 : w / 2 + 18), y * H - 6);
			pips[s].Scale = Vector2.Zero;
			CreateTween().TweenProperty(pips[s], "scale", Vector2.One, 0.25).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		}
	}

	protected override void OnChoose(int i)
	{
		if (Session.NeedsStarter && Menu[i] != "Quit" && Menu[i] != "Options") { StarterPick(); return; }
		switch (Menu[i])
		{
			case "Duel": Session.Tour = null; Transition.Go(this, "res://ui/DuelModes.tscn"); break;
			case "Tournament": Transition.Go(this, "res://ui/Tournament.tscn"); break;
			case "LAN Duel": Transition.Go(this, "res://ui/LanLobby.tscn"); break;
			case "Replays": Transition.Go(this, "res://ui/Replays.tscn"); break;
			case "Deck Builder": Transition.Go(this, "res://ui/DeckSelect.tscn"); break;
			case "Card List": Transition.Go(this, "res://ui/CardList.tscn"); break;
			case "Shop": Transition.Go(this, "res://ui/ShopScreen.tscn"); break;
			case "Options": Transition.Go(this, "res://ui/Options.tscn"); break;
			case "Quit": GetTree().CreateTimer(0.4).Timeout += () => GetTree().Quit(); break;
		}
	}

	// ---------------------------------------------------------------- loading intro

	void Intro()
	{
		var menu = GetChildren().OfType<CanvasItem>().Where(c => c != logoPic && c != namePic && c != bgRect).ToList();
		foreach (var c in menu) c.Modulate = c.Modulate with { A = 0 };
		// No input until the menu is on screen: the hidden rows took hover, keys and clicks (playtest 2026-11-09).
		var block = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Stop };
		AddChild(block);
		SetProcessUnhandledInput(false);
		bg.P("draw", 0f).P("reveal", 0f).P("smoke", 0f);
		float lw = 1040, lh = lw * logoHome.Size.Y / logoHome.Size.X, nw = lw * 0.8f, nh = nw * nameHome.Size.Y / nameHome.Size.X;
		var logoMid = new Rect2(W / 2 - lw / 2, H * 0.42f - lh / 2 - 40, lw, lh);
		var nameMid = new Rect2(W / 2 - nw / 2, logoMid.Position.Y + lh * 0.86f, nw, nh);
		Place(logoPic, logoMid); Place(namePic, nameMid);
		logoPic.Modulate = new Color(1, 1, 1, 0); namePic.Modulate = new Color(1, 1, 1, 0);
		var glow = new ColorRect { Position = new Vector2(W / 2 - 900, logoMid.Position.Y + lh * 0.55f - 500), Size = new Vector2(1800, 1000), MouseFilter = MouseFilterEnum.Ignore,
			Material = new ShaderMaterial { Shader = new Shader { Code = "shader_type canvas_item; void fragment(){ vec2 p = (UV - 0.5) * vec2(1.8, 1.0); float r = length(p); COLOR = vec4(0.62, 0.58, 0.75, exp(-r * r * 5.0) * 0.32); }" } } };
		AddChild(glow); MoveChild(glow, bgRect.GetIndex() + 1);
		glow.Modulate = new Color(1, 1, 1, 0);
		var flash = new ColorRect { Size = new Vector2(W, H), Color = new Color(1, 1, 1, 0), MouseFilter = MouseFilterEnum.Ignore };
		AddChild(flash);
		var sparks = new CpuParticles2D { Emitting = false, Amount = 140, Lifetime = 0.6, Direction = Vector2.Up, Spread = 180, Gravity = new Vector2(0, 900),
			InitialVelocityMin = 250, InitialVelocityMax = 650, ScaleAmountMin = 2, ScaleAmountMax = 5, Color = new Color(1f, 0.92f, 1f) };
		AddChild(sparks);
		// the rift strike (2026-11-09, sfxforge rift_strike): the tear falling, its boom landing with the rift at 0.22 s
		var hit = new AudioStreamPlayer { Stream = GD.Load<AudioStream>("res://assets/audio/gen/G_rift_strike.wav"), VolumeDb = -1, Bus = "Sound" };
		var swoosh = new AudioStreamPlayer { Stream = GD.Load<AudioStream>("res://assets/audio/gen/C_whoosh_gate.wav"), VolumeDb = -6, Bus = "Sound" };
		AddChild(hit); AddChild(swoosh);
		Vector2 CrackAt(float y) => new(Mathf.Lerp(Top, Bottom, y) * W, y * H);
		void Shake(Tween t, float amp, float time, int n)
		{
			for (int k = 0; k < n; k++)
			{
				float a = amp * (1 - (float)k / n);
				t.TweenProperty(this, "position", new Vector2((float)GD.RandRange(-a, a), (float)GD.RandRange(-a, a)), time / n);
			}
			t.TweenProperty(this, "position", Vector2.Zero, time / n);
		}

		var tw = CreateTween();
		tw.TweenProperty(glow, "modulate:a", 1f, 0.8);
		tw.Parallel().TweenProperty(logoPic, "modulate:a", 1f, 0.8);
		tw.TweenProperty(namePic, "modulate:a", 1f, 0.5);
		tw.Parallel().TweenProperty(namePic, "position:y", nameMid.Position.Y, 0.6).From(nameMid.Position.Y + 30).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		tw.TweenInterval(0.35);
		Shake(tw, 3, 0.4f, 10);
		tw.TweenCallback(Callable.From(() => { sparks.Emitting = true; if (!Shooting) hit.Play(); }));
		tw.TweenMethod(Callable.From<float>(v => { bg.P("draw", v); sparks.Position = CrackAt(Mathf.Min(v * 1.15f, 1f)); }), 0f, 1f, 0.22).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		tw.TweenCallback(Callable.From(() => { sparks.Emitting = false; flash.Color = new Color(1, 1, 1, 0.85f); bg.P("smoke", 1f); }));
		tw.TweenProperty(flash, "color:a", 0f, 0.45).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
		tw.Parallel().TweenMethod(Callable.From<float>(v => bg.P("burst", v)), 1f, 0f, 0.9).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		tw.Parallel().TweenMethod(Callable.From<float>(v => bg.P("reveal", v)), 0f, 1f, 0.45).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
		tw.Parallel().TweenProperty(glow, "modulate:a", 0f, 0.4);
		tw.Parallel().TweenCallback(Callable.From(() => { var s2 = CreateTween(); Shake(s2, 14, 0.35f, 9); }));
		tw.TweenCallback(Callable.From(() => { if (!Shooting) swoosh.Play(); }));
		foreach (var (pic, home) in new[] { (logoPic, logoHome), (namePic, nameHome) })
		{
			tw.Parallel().TweenProperty(pic, "position", home.Position, 0.85).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
			tw.Parallel().TweenProperty(pic, "size", home.Size, 0.85).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
		}
		tw.TweenCallback(Callable.From(RandomPair));
		for (int i = 0; i < menu.Count; i++)
			if (menu[i] != glow && menu[i] != flash && menu[i] != block) tw.Parallel().TweenProperty(menu[i], "modulate:a", 1f, 0.45).SetDelay(0.05 + i * 0.02);
		tw.TweenCallback(Callable.From(() => { glow.QueueFree(); flash.QueueFree(); block.QueueFree(); SetProcessUnhandledInput(true); }));
	}

	// ---------------------------------------------------------------- first launch: pick a starter deck

	Control? starter;

	void StarterPick()
	{
		if (starter != null) return;
		starter = new Control { Size = new Vector2(W, H), ZIndex = 30 };
		var dim = new ColorRect { Size = starter.Size, Color = new Color(0.02f, 0.02f, 0.03f, 0), MouseFilter = MouseFilterEnum.Stop };
		starter.AddChild(dim);
		AddChild(starter);
		CreateTween().TweenProperty(dim, "color:a", 0.94f, 0.3);
		Put(starter, "CHOOSE YOUR STARTER DECK", Title, 46, Silver, new Vector2(0, H * 0.14f), new Vector2(W, 70), HorizontalAlignment.Center);
		Hair(starter, new Vector2(W / 2 - 120, H * 0.14f + 74), 240, GoldC);
		const float cw = 230, gap = 150;
		int n = DeckRules.Starters.Length;
		for (int i = 0; i < n; i++)
		{
			int k = i;
			var (name, key) = DeckRules.Starters[i];
			var (main, extra) = Db.Deck(key);
			var cover = Db.Get(new Deck(name, main, extra).CoverCard());
			var c = new Vector2(W / 2 - (n - 1) * (cw + gap) / 2 + i * (cw + gap), H * 0.5f);
			var card = Card(starter, cover, c, cw * 1.458f, (i - 1.5f) * 0.03f, 1f, large: true);
			var l = Put(starter, name.ToUpperInvariant(), Title, 22, Silver, c + new Vector2(-cw, cw * 0.73f + 30), new Vector2(2 * cw, 30), HorizontalAlignment.Center);
			var hit = new Control { Position = card.Position, Size = card.Size };
			starter.AddChild(hit);
			Click(hit, () => TakeStarter(k), h =>
			{
				Ink_.Only(this, card).TweenProperty(card, "scale", h ? new Vector2(1.06f, 1.06f) : Vector2.One, 0.18);
				l.AddThemeColorOverride("font_color", h ? GoldLight : Silver);
			});
		}
	}

	void TakeStarter(int k)
	{
		var d = DeckRules.GrantStarter(P, k);
		Session.Decks.Add(d);
		Session.Playing = d;
		Session.Save();
		var s = starter!; starter = null;
		var tw = CreateTween();
		tw.TweenProperty(s, "modulate:a", 0f, 0.35);
		tw.TweenCallback(Callable.From(s.QueueFree));
	}
}
