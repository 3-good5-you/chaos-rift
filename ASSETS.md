# Assets

This repository is code only. The following are **not** included and are not covered by the MIT licence:

| Folder (under `game/Godot/`) | What it holds |
|---|---|
| `cards/` | Card images |
| `assets/art`, `assets/rivals`, `assets/walls` | Character and background art |
| `assets/audio` | Music and sound effects |
| `assets/vfx`, `assets/exodia`, `assets/coin`, `assets/counters` | Effect textures |
| `assets/backs`, `assets/mats`, `assets/frames`, `assets/title` | Card backs, playmats, frames, logo |

What **is** included:

- `assets/fonts`: Anton, Barlow, Cabin, Cinzel and Cinzel Decorative, each under the SIL Open Font License (licence
  texts beside the fonts).
- `remake/data/*.json`: the card database, deck lists and rule tables the engine embeds at build time. Card names and
  text belong to their owners; they are here because the engine cannot build or be tested without them.

## Running the game from source

1. Install the release build.
2. Unpack its `.pck` with Godot's export tools, or ask in the issues for the asset pack.
3. Copy the folders listed above into `game/Godot/`.

The engine and its checks (`dotnet run --project game/Engine.Check`) need none of this.
