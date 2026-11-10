using Godot;

namespace PoC.Game.UI;

/// Connected-lobby mockups, round 2 (no card art behind the players; the duel itself is the picture).
/// `-- --demo --variant=d|e|f|g`: d = Clash (two energies collide at a seam), e = Arena (a rune-circle floor,
/// you near and big, the rival far, a bolt between), f = Shadow Game (purple fog, the Millennium Eye),
/// g = Card Storm (both players' card backs whirl around the two icons).
public partial class LanSamples
{
    const string Noise = @"
float h1(vec2 p) { vec3 q = fract(vec3(p.xyx) * 0.1031); q += dot(q, q.yzx + 33.33); return fract((q.x + q.y) * q.z); }  // sin-free: sin() hashes break into blocks on Intel GPUs
float n1(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
  return mix(mix(h1(i), h1(i + vec2(1, 0)), f.x), mix(h1(i + vec2(0, 1)), h1(i + vec2(1, 1)), f.x), f.y); }
float fbm(vec2 p) { float v = 0.0, s = 0.5; for (int i = 0; i < 5; i++) { v += s * n1(p); p *= 2.03; s *= 0.5; } return v; }
";
    const string YouFrame = "gold", ThemFrame = "dark";

    ColorRect Full(string shader, params (string, Variant)[] u)
    {
        var r = new ColorRect { Size = new Vector2(W, barY), MouseFilter = MouseFilterEnum.Ignore, Material = Mat(shader, u) };
        AddChild(r);
        return r;
    }

    /// A soft coloured glow disc (behind an icon).
    void Glow(Vector2 centre, float radius, Color c, float strength = 1)
    {
        AddChild(new ColorRect
        {
            Position = centre - new Vector2(radius, radius), Size = new Vector2(radius * 2, radius * 2), MouseFilter = MouseFilterEnum.Ignore,
            Material = Mat(@"shader_type canvas_item;
uniform vec4 col : source_color; uniform float k = 1.0;
void fragment() { float r = length(UV - 0.5) * 2.0; COLOR = vec4(col.rgb, k * pow(max(0.0, 1.0 - r), 2.2)); }", ("col", c), ("k", strength)),
        });
    }

    Control Icon(int pic, string frame, float size, Vector2 centre)
    {
        var f = Cosmetics.Framed(Pic(pic), frame, size);
        f.Position = centre - new Vector2(size / 2, size / 2);
        AddChild(f);
        return f;
    }

    void Name(string text, Color c, Vector2 centre, int size = 84) =>
        Kit.Put(this, text, Kit.Impact, size, c, centre - new Vector2(500, 0), new Vector2(1000, size * 1.3f), HorizontalAlignment.Center, 12);

    /// Your deck name and their status, small, under the names.
    void Lines(Vector2 you, Vector2 them)
    {
        Kit.Put(this, "YOUR DECK  ·  " + (Session.Decks.FirstOrDefault()?.Name ?? "DECK").ToUpperInvariant(), Kit.Cond, 26, RivalPanels.Gold, you - new Vector2(400, 0), new Vector2(800, 34), HorizontalAlignment.Center);
        Kit.Put(this, "PICKING A DECK…", Kit.Cond, 26, Cool, them - new Vector2(400, 0), new Vector2(800, 34), HorizontalAlignment.Center);
    }

    Label Vs(Vector2 centre, int size = 170, Color? c = null) =>
        Kit.Put(this, "VS", Kit.Impact, size, c ?? RivalPanels.Gold, centre - new Vector2(200, size * 0.62f), new Vector2(400, size * 1.3f), HorizontalAlignment.Center, 18);

    /// A jagged lightning bolt between two screen points.
    void Bolt(Vector2 a, Vector2 b, Color c)
    {
        Full("shader_type canvas_item;" + Noise + @"
uniform vec2 p0; uniform vec2 p1; uniform vec4 col : source_color; uniform float aspect;
void fragment() {
  vec2 P = vec2(UV.x * aspect, UV.y), A = vec2(p0.x * aspect, p0.y), B = vec2(p1.x * aspect, p1.y), ab = B - A;
  float t = clamp(dot(P - A, ab) / dot(ab, ab), 0.0, 1.0);
  vec2 nr = normalize(vec2(-ab.y, ab.x));
  float off = (fbm(vec2(t * 9.0, floor(TIME * 12.0))) - 0.5) * 0.09 * sin(t * 3.1416);
  float d = length(P - (A + ab * t) - nr * off);
  float g = exp(-d * 260.0) + exp(-d * 30.0) * 0.35;
  COLOR = vec4(mix(col.rgb, vec3(1.0), clamp(g * g, 0.0, 1.0)), clamp(g, 0.0, 1.0));
}", ("p0", a / new Vector2(W, barY)), ("p1", b / new Vector2(W, barY)), ("col", c), ("aspect", W / barY));
    }

    // ---- d: Clash

    void Clash()
    {
        Full("shader_type canvas_item;" + Noise + @"
uniform vec4 a : source_color; uniform vec4 b : source_color; uniform float aspect;
void fragment() {
  vec2 uv = UV, p = vec2(uv.x * aspect, uv.y);
  float t = TIME * 0.35;
  float seam = 0.5 + (fbm(vec2(uv.y * 3.0, t)) - 0.5) * 0.14;
  float d = uv.x - seam;
  float flow = fbm(p * 2.6 + vec2(-sign(d) * t * 2.5, t * 0.3));
  vec3 col = d < 0.0 ? a.rgb : b.rgb;
  float near = exp(-abs(d) * 3.5);
  vec3 c = vec3(0.012, 0.012, 0.02) + col * (0.05 + 0.75 * near * flow * flow);
  float core = exp(-abs(d) * 120.0) * (0.5 + fbm(vec2(uv.y * 24.0, t * 6.0)));
  c += vec3(1.0, 0.96, 0.88) * core;
  // round embers drifting off the seam
  vec2 cell = vec2(p.x * 40.0, p.y * 40.0 + t * 18.0);
  float e = step(0.93, h1(floor(cell))) * smoothstep(0.22, 0.0, length(fract(cell) - 0.5));
  c += vec3(1.0, 0.85, 0.6) * e * exp(-abs(d) * 9.0);
  c *= 1.0 - 0.55 * length((uv - 0.5) * vec2(1.1, 1.5));
  COLOR = vec4(c, 1.0);
}", ("a", RivalPanels.Gold), ("b", Cool), ("aspect", W / barY));
        var you = new Vector2(W * 0.22f, barY * 0.42f);
        var them = new Vector2(W * 0.78f, barY * 0.42f);
        Glow(you, 330, RivalPanels.Gold, 0.55f); Glow(them, 330, Cool, 0.55f);
        Icon(Settings.Picture, YouFrame, 380, you);
        Icon(Them.Pic, ThemFrame, 380, them);
        Name(You, Colors.White, you + new Vector2(0, 230));
        Name(Them.Name, Colors.White, them + new Vector2(0, 230));
        Lines(you + new Vector2(0, 345), them + new Vector2(0, 345));
        Vs(new Vector2(W / 2, barY * 0.45f), 190, Colors.White);
        BottomBar("LAN DUEL", "READY");
    }

    // ---- e: Arena

    void Arena()
    {
        Full("shader_type canvas_item;" + Noise + @"
uniform vec4 gold : source_color; uniform vec4 cool : source_color;
void fragment() {
  vec2 uv = UV; float hz = 0.40;
  vec3 c = mix(vec3(0.015, 0.016, 0.03), vec3(0.05, 0.04, 0.035), smoothstep(0.0, hz, uv.y));
  // light shafts from above
  float shaft = pow(max(0.0, 1.0 - abs(uv.x - 0.5 - (uv.y - 0.0) * 0.15) * 3.0), 3.0) * (1.0 - uv.y) * 0.12;
  c += gold.rgb * shaft;
  float y = uv.y - hz;
  if (y > 0.0) {
    float z = 0.22 / y;
    vec2 g = vec2((uv.x - 0.5) * z * 3.0, z + TIME * 0.4);
    vec2 f = abs(fract(g) - 0.5);
    float line = smoothstep(0.47, 0.5, max(f.x, f.y)) * exp(-z * 0.18);
    c += mix(gold.rgb, cool.rgb, uv.x) * line * 0.55;
    // rune circle on the floor
    float r = length((uv - vec2(0.5, 0.72)) * vec2(1.0, 3.0));
    float ring = exp(-abs(r - 0.30) * 90.0) + exp(-abs(r - 0.24) * 120.0) * 0.6 + exp(-abs(r - 0.36) * 140.0) * 0.4;
    float a = atan((uv.y - 0.72) * 3.0, uv.x - 0.5);
    ring += step(0.6, fract(a * 6.0 / 3.1416 + TIME * 0.1)) * exp(-abs(r - 0.33) * 220.0) * 0.8;
    c += gold.rgb * ring * 0.9;
    c += gold.rgb * exp(-r * 4.0) * 0.18;
  }
  c += vec3(0.5, 0.45, 0.4) * exp(-abs(y) * 60.0) * 0.12;   // horizon haze
  c *= 1.0 - 0.45 * length((uv - vec2(0.5, 0.55)) * vec2(1.1, 1.3));
  COLOR = vec4(c, 1.0);
}", ("gold", RivalPanels.Gold), ("cool", Cool));
        var you = new Vector2(W * 0.24f, barY * 0.66f);
        var them = new Vector2(W * 0.74f, barY * 0.28f);
        Bolt(you, them, RivalPanels.Gold.Lerp(Cool, 0.5f));
        Glow(you, 340, RivalPanels.Gold, 0.6f); Glow(them, 240, Cool, 0.6f);
        Icon(Settings.Picture, YouFrame, 420, you);
        Icon(Them.Pic, ThemFrame, 270, them);
        Name(You, Colors.White, you + new Vector2(330, -40), 96);
        Kit.Put(this, "YOUR DECK  ·  " + (Session.Decks.FirstOrDefault()?.Name ?? "DECK").ToUpperInvariant(), Kit.Cond, 26, RivalPanels.Gold, you + new Vector2(240, 82));
        Kit.Put(this, Them.Name, Kit.Impact, 72, Cool, them - new Vector2(860, 60), new Vector2(680, 100), HorizontalAlignment.Right, 10);
        Kit.Put(this, "PICKING A DECK…", Kit.Cond, 26, Cool, them - new Vector2(860, -40), new Vector2(680, 34), HorizontalAlignment.Right);
        Vs((you + them) / 2, 150);
        BottomBar("LAN DUEL", "READY");
    }

    // ---- f: Shadow Game

    void Shadow()
    {
        Full("shader_type canvas_item;" + Noise + @"
uniform float aspect;
void fragment() {
  vec2 uv = UV, p = (uv - 0.5) * vec2(aspect, 1.0);
  float r = length(p), t = TIME * 0.12;
  float ang = 1.6 * exp(-r * 1.4);
  p = mat2(vec2(cos(ang), -sin(ang)), vec2(sin(ang), cos(ang))) * p;
  vec2 q = vec2(fbm(p * 2.2 + t), fbm(p * 2.2 + vec2(5.2, 1.3) - t));
  float f = fbm(p * 2.2 + 2.0 * q);
  vec3 c = mix(vec3(0.015, 0.0, 0.03), vec3(0.32, 0.07, 0.48), f * f * 1.5);
  c += vec3(0.95, 0.65, 0.2) * pow(f, 7.0) * 1.2;
  c += vec3(0.7, 0.3, 1.0) * exp(-r * 3.0) * 0.25;
  c *= 1.0 - 0.6 * smoothstep(0.3, 1.1, r);
  COLOR = vec4(c, 1.0);
}", ("aspect", W / barY));
        var mid = new Vector2(W / 2, barY * 0.44f);
        var eye = new TextureRect
        {
            Texture = Tex("res://assets/coin/eye.png"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Size = new Vector2(760, 760), Position = mid - new Vector2(380, 380), Modulate = new Color(1, 0.85f, 0.6f, 0.22f), MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(eye);
        var you = new Vector2(W * 0.2f, barY * 0.44f);
        var them = new Vector2(W * 0.8f, barY * 0.44f);
        Bolt(you + new Vector2(200, 0), mid - new Vector2(120, 0), new Color(0.85f, 0.5f, 1f));
        Bolt(them - new Vector2(200, 0), mid + new Vector2(120, 0), new Color(0.85f, 0.5f, 1f));
        Glow(you, 300, new Color(0.75f, 0.4f, 1f), 0.5f); Glow(them, 300, new Color(0.75f, 0.4f, 1f), 0.5f);
        Icon(Settings.Picture, YouFrame, 360, you);
        Icon(Them.Pic, ThemFrame, 360, them);
        Name(You, Colors.White, you + new Vector2(0, 220));
        Name(Them.Name, Colors.White, them + new Vector2(0, 220));
        Lines(you + new Vector2(0, 335), them + new Vector2(0, 335));
        Kit.Put(this, "THE SHADOW GAME BEGINS", Kit.Cond, 34, new Color(0.9f, 0.75f, 1f), new Vector2(W / 2 - 500, 60), new Vector2(1000, 44), HorizontalAlignment.Center);
        Vs(mid + new Vector2(0, 300), 120);
        BottomBar("LAN DUEL", "READY");
    }

    // ---- g: Card Storm

    Control? storm;

    void Storm()
    {
        Full(@"shader_type canvas_item;
uniform vec4 gold : source_color; uniform float aspect;
void fragment() {
  vec2 p = (UV - vec2(0.5, 0.46)) * vec2(aspect, 1.0); float r = length(p);
  vec3 c = mix(vec3(0.02, 0.018, 0.03), vec3(0.0), smoothstep(0.0, 1.2, r));
  c += gold.rgb * exp(-r * 5.0) * 0.55;
  float rays = pow(0.5 + 0.5 * sin(atan(p.y, p.x) * 18.0 + TIME * 0.6), 6.0) * exp(-r * 2.2) * 0.25;
  c += gold.rgb * rays;
  COLOR = vec4(c, 1.0);
}", ("gold", RivalPanels.Gold), ("aspect", W / barY));
        var mid = new Vector2(W / 2, barY * 0.46f);
        storm = new Control { Position = mid, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(storm);
        var backs = new[] { Cosmetics.Back("12_millennium", 360), Cosmetics.Back("01_vortex", 360) };
        for (int i = 0; i < 70; i++)
        {
            float a = i * 2.39996f, r = 250 + i * 8.5f, s = 0.55f + r / 900;
            var size = new Vector2(86, 125) * s;
            var c = new TextureRect
            {
                Texture = backs[i % 2], ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
                Size = size, PivotOffset = size / 2, Position = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.55f) * r - size / 2,
                Rotation = a + 1.3f, Modulate = new Color(1, 1, 1, Math.Clamp(1.1f - r / 950, 0.15f, 0.95f)), MouseFilter = MouseFilterEnum.Ignore,
                Material = Kit.RoundedMat(0.69f, 0.04f),
            };
            storm.AddChild(c);
        }
        var you = mid - new Vector2(190, 0);
        var them = mid + new Vector2(190, 0);
        Glow(mid, 420, RivalPanels.Gold, 0.5f);
        Icon(Them.Pic, ThemFrame, 340, them);
        Icon(Settings.Picture, YouFrame, 340, you);
        // A flash where the two icons meet.
        Glow(mid, 120, Colors.White, 0.9f);
        Kit.Put(this, You, Kit.Impact, 96, Colors.White, new Vector2(70, barY - 250), outline: 12);
        Kit.Put(this, "YOUR DECK  ·  " + (Session.Decks.FirstOrDefault()?.Name ?? "DECK").ToUpperInvariant(), Kit.Cond, 26, RivalPanels.Gold, new Vector2(76, barY - 120));
        Kit.Put(this, Them.Name, Kit.Impact, 96, Cool, new Vector2(W - 70 - 800, barY - 250), new Vector2(800, 130), HorizontalAlignment.Right, 12);
        Kit.Put(this, "PICKING A DECK…", Kit.Cond, 26, Cool, new Vector2(W - 76 - 600, barY - 120), new Vector2(600, 34), HorizontalAlignment.Right);
        Kit.Put(this, "DUEL!", Kit.Impact, 120, RivalPanels.Gold, new Vector2(W / 2 - 300, mid.Y + 210), new Vector2(600, 160), HorizontalAlignment.Center, 14);
        BottomBar("LAN DUEL", "READY");
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (storm != null) storm.Rotation += (float)delta * 0.08f;
    }
}
