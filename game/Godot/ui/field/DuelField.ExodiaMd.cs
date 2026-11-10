using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// The Exodia finish, LOCKED 2026-10-29: the card part (ExodiaAssemble), then Master Duel's Exodia win from its fire
/// wall on (fire wall, silhouette rising through it, reveal on the explosion, eye flash, power ball, release, white
/// flash). Two builds of the same look, both in the FX Lab rows 1 and 2:
/// - A, film: frames baked out of the web rebuild (remake/samples/md_exodia/full.html `__bake()` -> assets/exodia/md,
///   git-ignored MD material), MD timeline 2.583s (fire wall) to the white flash at 9.6s.
/// - B, native: the same scene built in Godot (DuelField.ExodiaNative.cs), with only Exodia himself baked. Duels play
///   this one (picked over A 2026-10-29).
/// Without the git-ignored frames (a fresh clone) duels fall back to the original game's Exodia (ExodiaFinal).
public partial class DuelField
{
    const double MdT0 = 2.583, MdFps = 30;
    const int MdFrames = 211, MdOpaqueFrom = 13;   // frame 13 = 3.0s: MD's black backdrop is in and the frames cover the screen
    const bool ExodiaNativeInDuels = true;         // B picked over A; false plays the film

    static string MdFrame(int i) => $"res://assets/exodia/md/f_{i:000}.webp";
    static bool MdFilmThere => ResourceLoader.Exists(MdFrame(MdFrames - 1));
    static bool MdNativeThere => Godot.FileAccess.FileExists(MdDir + "data.json") && Godot.FileAccess.FileExists(MdDir + "bones.json") && ResourceLoader.Exists($"{MdDir}x_204.webp");
    const string MdSound = "res://assets/exodia/md/SE_EV_EXODIA.wav";

    async Task ExodiaFinalMd(List<View> p, bool native)
    {
        var temp = new List<Control>();
        T Add<T>(T n) where T : Control { hud.AddChild(n); temp.Add(n); return n; }
        TextureRect Pic(Texture2D t, Vector2 size, Vector2 pos, Color mod) => Add(new TextureRect { Texture = t, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Size = size, PivotOffset = size / 2, Position = pos, Modulate = mod, MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps });

        var dim = Add(new ColorRect { Size = new Vector2(W, H), Color = new Color(0, 0, 0, 0), MouseFilter = MouseFilterEnum.Ignore });
        CreateTween().TweenProperty(dim, "color:a", 0.8f, 0.7);
        await Wait(0.4);
        var (big, _) = await ExodiaAssemble(p, Pic);
        if (native) { await ExodiaNative(big, dim, Add); return; }

        // The first frames are flames on black, added over the cards while MD's black backdrop fades in (2.6-3.0s).
        var brack = Add(new ColorRect { Size = new Vector2(W, H), Color = new Color(0, 0, 0, 0), MouseFilter = MouseFilterEnum.Ignore });
        CreateTween().TweenProperty(brack, "color:a", 0.98f, MdOpaqueFrom / MdFps).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        var tex = ImageTexture.CreateFromImage(ResImage(MdFrame(0)));
        var film = Add(new TextureRect { Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, Size = new Vector2(W, H), Material = AddMat, MouseFilter = MouseFilterEnum.Ignore });

        // MD's one sound for the whole cutscene (SE_EV_EXODIA) starts at 0.3s, so it is picked up 2.283s in.
        var snd = new AudioStreamPlayer { Stream = GD.Load<AudioStream>(MdSound), VolumeDb = -4, Bus = "Sound" };
        AddChild(snd);
        snd.Play((float)(MdT0 - 0.3));

        // Frames are decoded one at a time into a single texture (all 211 at once would be ~780 MB of VRAM).
        ulong start = Time.GetTicksUsec();
        for (int shown = 0; ;)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            int i = (int)((Time.GetTicksUsec() - start) / 1e6 * MdFps);
            if (i >= MdFrames) break;
            if (i == shown) continue;
            shown = i;
            tex.Update(ResImage(MdFrame(i)));
            if (i >= MdOpaqueFrom && film.Material != null) { film.Material = null; foreach (var r in big) r.Visible = false; }
        }

        // MD's Fade layer: white at WinStart (9.6s), fading back to the field over 1.8s.
        var white = Add(new ColorRect { Size = new Vector2(W, H), Color = Colors.White, MouseFilter = MouseFilterEnum.Ignore });
        film.Visible = brack.Visible = false;
        dim.Color = new Color(0, 0, 0, 0);
        CreateTween().TweenProperty(white, "color:a", 0f, 1.8).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        await Wait(0.3);
        After(2.0, () => { foreach (var n in temp) if (IsInstanceValid(n)) n.QueueFree(); snd.QueueFree(); });
    }

    async Task ExodiaLabMd(int side, bool native)
    {
        var p = DealExodia(side); RivalBoardFor(side);
        await Wait(0.5);
        await ExodiaFinalMd(p, native);
        await WipeSide(1 - side, burn: true, gap: 0.05);
        await Wait(0.6);
        await Finale(side);
    }
}
