using Godot;

namespace PoC.Game.UI;

/// Who goes first (Chaos Rift, 2026-11-08): you in the light, the rival in the dark, the black lacquer coin with a gold
/// rim (Millennium Puzzle = you go first, Millennium Eye = the rival) tossed on the rift. When it lands the rift
/// flares, the winner's side stays bright, the other fades, and the result is written across the rift.
public partial class TurnOrder : RiftMenu
{
	/// The gold coin face, still used for coin effects in the duel (DuelField.Fx).
	public const string Face = @"shader_type spatial;
uniform sampler2D tex : source_color, filter_linear_mipmap;
uniform float art_scale = 1.0;
void fragment() {
  vec2 p = UV - 0.5;
  float r = length(p);
  if (r > 0.5) discard;
  // Brushed gold base with a raised inner ring.
  vec3 gold = mix(vec3(0.95, 0.76, 0.32), vec3(0.62, 0.42, 0.12), smoothstep(0.0, 0.5, r));
  gold += vec3(0.12, 0.09, 0.03) * sin(atan(p.y, p.x) * 40.0) * 0.15;
  float ring = smoothstep(0.012, 0.0, abs(r - 0.455));
  gold = mix(gold, vec3(1.0, 0.88, 0.5), ring);
  vec4 art = texture(tex, (UV - 0.5) / art_scale + 0.5);
  float inside = step(max(abs((UV.x - 0.5) / art_scale), abs((UV.y - 0.5) / art_scale)), 0.5);
  ALBEDO = mix(gold, art.rgb, art.a * inside);
  METALLIC = 0.75;
  ROUGHNESS = 0.28;
}";

	/// The coin face: black lacquer, a gold ring, the emblem drawn in cream.
	const string InkFace = @"shader_type spatial;
render_mode unshaded;
uniform sampler2D tex : source_color, filter_linear_mipmap;
uniform float art_scale = 1.0;
uniform bool flip = false;      // the back face is upside down after the half turn
void fragment() {
  vec2 p = UV - 0.5;
  if (flip) p = -p;
  float r = length(p);
  if (r > 0.5) discard;
  vec3 ink = vec3(0.045, 0.04, 0.05), cream = vec3(0.93, 0.92, 0.9), gold = vec3(0.86, 0.66, 0.3);
  vec3 c = ink + vec3(0.03) * smoothstep(0.5, 0.0, r);
  c = mix(c, gold, smoothstep(0.012, 0.0, abs(r - 0.46)) + smoothstep(0.006, 0.0, abs(r - 0.43)) * 0.6);
  vec4 art = texture(tex, p / art_scale + 0.5);
  float inside = step(max(abs(p.x / art_scale), abs(p.y / art_scale)), 0.5);
  c = mix(c, mix(ink, cream, smoothstep(0.22, 0.5, dot(art.rgb, vec3(0.3, 0.59, 0.11)))), art.a * inside);
  c += vec3(0.08) * exp(-pow((UV.x + UV.y - 0.7) * 6.0, 2.0));   // soft sheen
  ALBEDO = c;
}";

	bool youFirst;
	Control youRoot = null!, themRoot = null!;

	protected override string? MusicPath => Md("solo_gate");

	protected override void Build()
	{
		if (Session.Picks.Length == 0) Session.RollPicks();
		if (Session.Selected < 0) Session.Selected = 1;
		youFirst = Lan.On ? Lan.First == Lan.Seat : Session.Rng.Coin();   // LAN: the host's coin
		if (OS.GetCmdlineUserArgs().Contains("--demo")) youFirst = !OS.GetCmdlineUserArgs().Contains("--rivalfirst");
		Session.YouFirst = youFirst;
		Rift(0.5f, 0.5f);
		Put(this, "WHO GOES FIRST", Title, 30, Ink, new Vector2(80, 58));
		Hair(this, new Vector2(82, 100), 160, GoldC);
		Duelists();
		Coin();
	}

	string TheirName => Lan.On ? Lan.TheirName : Session.Rival.Name;

	/// You on the light side (framed picture, name, deck), the rival on the dark side (standing portrait or picture).
	void Duelists()
	{
		const float A = 280;
		youRoot = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
		themRoot = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
		AddChild(youRoot); AddChild(themRoot);
		var c = new Vector2(W * 0.2f, H * 0.46f);
		var pic = Cosmetics.Framed(Avatar, P.Frame, A);
		pic.Position = c - new Vector2(A, A) / 2; youRoot.AddChild(pic);
		Put(youRoot, Settings.PlayerName.ToUpperInvariant(), Title, 46, Ink, new Vector2(c.X - 400, c.Y + A / 2 + 30), new Vector2(800, 60), HorizontalAlignment.Center);
		var deckName = (Session.ModeDeck ?? Session.Playing)?.Name ?? "";
		Put(youRoot, deckName.ToUpperInvariant(), Thin, 18, Graphite, new Vector2(c.X - 400, c.Y + A / 2 + 96), new Vector2(800, 26), HorizontalAlignment.Center);

		float cx = W * 0.8f;
		var rival = Session.Rival;
		if (Lan.On || !RivalPanels.HasPortrait(rival))
		{
			var tc = new Vector2(cx, H * 0.46f);
			if (Lan.On) { var tp = Cosmetics.Framed(Lan.TheirAvatar, Lan.TheirFrame, A); tp.Position = tc - new Vector2(A, A) / 2; themRoot.AddChild(tp); }
			else Pic(themRoot, RivalPanels.Art(rival), tc - new Vector2(A, A) / 2, new Vector2(A, A)).Material = Kit.RoundedMat(1, 0.5f);
			Put(themRoot, TheirName.ToUpperInvariant(), Title, 46, Silver, new Vector2(tc.X - 400, tc.Y + A / 2 + 30), new Vector2(800, 60), HorizontalAlignment.Center);
			return;
		}
		var tex = RivalPanels.Portrait(rival);
		float ph = H * 0.7f, pw = ph * tex.GetWidth() / tex.GetHeight();
		Pic(themRoot, tex, new Vector2(cx - pw / 2, H - ph), new Vector2(pw, ph));
		Put(themRoot, TheirName.ToUpperInvariant(), Title, 46, Silver, new Vector2(cx - 400, 110), new Vector2(800, 60), HorizontalAlignment.Center);
	}

	void Coin()
	{
		var holder = new SubViewportContainer { Position = new Vector2(W / 2 - 500, 80), Size = new Vector2(1000, H * 0.62f), Stretch = true, MouseFilter = MouseFilterEnum.Ignore };
		var vp = new SubViewport { TransparentBg = true, Msaa3D = Viewport.Msaa.Msaa4X, OwnWorld3D = true };
		holder.AddChild(vp);
		AddChild(holder);
		var world = new Node3D();
		vp.AddChild(world);
		world.AddChild(new WorldEnvironment
		{
			Environment = new Godot.Environment
			{
				BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(0, 0, 0, 0),
				AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.95f, 0.92f, 0.86f), AmbientLightEnergy = 1.1f,
			},
		});
		world.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-35, -25, 0), LightEnergy = 1.0f });
		var cam = new Camera3D { Position = new Vector3(0, 1.2f, 6.5f), Fov = 40 };
		world.AddChild(cam);
		cam.LookAt(new Vector3(0, 0.6f, 0));

		var coin = new Node3D { Position = new Vector3(0, 0.6f, 0) };
		world.AddChild(coin);
		var rim = new StandardMaterial3D { AlbedoColor = new Color(0.82f, 0.62f, 0.26f), Metallic = 0.7f, Roughness = 0.3f };
		coin.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 1.2f, BottomRadius = 1.2f, Height = 0.14f, RadialSegments = 96 }, MaterialOverride = rim, RotationDegrees = new Vector3(90, 0, 0) });
		ShaderMaterial FaceMat(string img, float scale, bool flip)
		{
			var m = new ShaderMaterial { Shader = new Shader { Code = InkFace } };
			m.SetShaderParameter("tex", Tex($"res://assets/coin/{img}.png"));
			m.SetShaderParameter("art_scale", scale);
			m.SetShaderParameter("flip", flip);
			return m;
		}
		var faceMesh = new QuadMesh { Size = new Vector2(2.4f, 2.4f) };
		coin.AddChild(new MeshInstance3D { Mesh = faceMesh, MaterialOverride = FaceMat("puzzle", 0.66f, false), Position = new Vector3(0, 0, 0.071f) });
		coin.AddChild(new MeshInstance3D { Mesh = faceMesh, MaterialOverride = FaceMat("eye", 0.74f, true), Position = new Vector3(0, 0, -0.071f), RotationDegrees = new Vector3(0, 180, 0) });

		float turns = 9 + (youFirst ? 0 : 0.5f); // whole turns leave the puzzle (you) facing the camera
		var tw = CreateTween();
		tw.TweenInterval(0.6);
		tw.TweenMethod(Callable.From<float>(k =>
		{
			float h = 4f * k * (1 - k);
			coin.Position = new Vector3(0, 0.6f + h * 1.1f, -h * 2.5f);
			coin.Rotation = new Vector3(Mathf.Tau * turns * Mathf.SmoothStep(0, 1, k), 0, 0.25f * Mathf.Sin(k * 6));
		}), 0f, 1f, 2.2).SetTrans(Tween.TransitionType.Linear);
		// landing: the rift flares where the coin lands
		tw.TweenCallback(Callable.From(() => bg.P("focus_y", 0.42f)));
		tw.TweenMethod(Callable.From<float>(v => bg.P("burst", v)), 1f, 0f, 0.8).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		tw.Parallel().TweenProperty(coin, "scale", new Vector3(1.1f, 1.1f, 1.1f), 0.12);
		tw.TweenProperty(coin, "scale", Vector3.One, 0.3).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
		Announce(2.9f);
	}

	/// The losing side fades, the winning side stays; the result is written across the rift in two-tone ink.
	void Announce(float delay)
	{
		GetTree().CreateTimer(delay).Timeout += () => CreateTween().TweenProperty(youFirst ? themRoot : youRoot, "modulate", new Color(1, 1, 1, 0.35f), 0.5);
		var who = T(this, youFirst ? "YOU" : TheirName.ToUpperInvariant(), Title, 96, new Vector2(W / 2 - 600, H - 300), new Vector2(1200, 120), HorizontalAlignment.Center);
		who.Mat().P("lit", 1f);
		var rest = T(this, youFirst ? "GO FIRST" : "GOES FIRST", Title, 44, new Vector2(W / 2 - 600, H - 170), new Vector2(1200, 60), HorizontalAlignment.Center);
		var pa = Pip(this, new Vector2(W / 2 - 200, H - 140), 10); var pb = Pip(this, new Vector2(W / 2 + 200, H - 140), 10);
		foreach (var c in new Control[] { who, rest, pa, pb })
		{
			float d = delay + (c == who ? 0 : 0.25f);
			c.Modulate = new Color(1, 1, 1, 0);
			CreateTween().TweenProperty(c, "modulate:a", 1f, 0.35).SetDelay(d);
		}
		if (!Shooting) GetTree().CreateTimer(delay + 2.2).Timeout += () => Transition.Go(this, "res://ui/field/DuelField.tscn");
	}

	protected override void OnBack() { if (!Lan.On) Transition.Go(this, "res://ui/DuelSetup.tscn"); }   // LAN: the duel is dealt, no way back
}
