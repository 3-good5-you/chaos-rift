<h1 align="center">Chaos Rift</h1>
<p align="center"><b>Three games. One deck. The Gods are waking up.</b></p>
<p align="center">
  <a href="https://3good5you.itch.io/chaos-rift"><b>Download free for Windows / macOS</b></a>
</p>

![Main menu](docs/screens/01-main-menu.jpg)

Remember *Power of Chaos*? Yugi the Destiny, Kaiba the Revenge, Joey the Passion. Three separate games, one duelist
each, and every time you switched you left your cards behind.

Chaos Rift ends that. Every duelist, every card, one collection, one deck you take anywhere. Then it keeps going where
the old games stopped.

**Free. No account. No shop that wants your money.**

![A duel](docs/screens/02-duel.jpg)

### 2,015 cards that actually work

The originals had under 800. Chaos Rift has 2,015, and every effect runs on a real rules engine: chains, flips,
fusions, rituals, coin tosses. Dark Magician, Blue-Eyes, Red-Eyes, Exodia, all here. Play by the 2003 or 2004 ban list.

### Rivals who fight back

Yugi, Kaiba, Joey, and now Jaden. Their decks get meaner every time you beat them. The CPU plays your turn out in its
head before it commits, so the old tricks won't save you. It never cheats by looking at your hand. In a best-of-three
it side-decks against whatever you showed it.

### Ten ways to duel

| | |
|---|---|
| Regular | One duel, or best of three with side decking |
| Gauntlet | Four rivals back to back. Your Life Points carry over |
| Pack Duel | Six fresh packs. Build from what you pulled |
| Limited Duel | A new restriction on your collection every day |
| Legend | A rival at full strength. Biggest payout in the game |
| Chaos Duel | Every third turn, the rules break |
| Challenges | One turn. One way to win. Find it |
| Tournament | Eight duelists, one bracket, prizes by placing |
| LAN Duel | You and a friend, same network |
| Replays | Keep the wins worth watching again |

![Tournament](docs/screens/10-tournament.jpg)

### Win it, own it

Cards drop after every duel. First win over a rival hands you their starter deck. Spend your Duel Points on 31 card
backs, 20 playmats and 18 frames, or win the Tournament for playmats you can't buy anywhere.

![Deck unlocked](docs/screens/14-deck-unlock.jpg)

![Card backs in the shop](docs/screens/16-shop-card-backs.jpg)

### Build the deck you remember

Main, Side and Fusion decks, fast filters, and import/export for `.ydk` files and the original games' `.ydc` files.
Your old decks still work.

![Deck builder](docs/screens/07-deck-builder.jpg)

### Finishes worth the grind

Complete Exodia and the duel doesn't end on a text box. It ends on a show.

## Coming next: the Egyptian Gods

Obelisk the Tormentor. Slifer the Sky Dragon. The Winged Dragon of Ra. They're the next big drop, and they won't just
be big numbers on a card.

Also on the way: online duels, more rivals, more decks, more ways to dress your table.

## Status

Version 1.0. The Windows build is the one I play and test. The macOS build exists but nobody has tried it on a real
Mac yet, so tell me if it works.

Known rough spots: the CPU still makes the odd strange play, and about 870 of the newer cards have only been checked
by the automated tests and not by hand in a real duel. If a card does something wrong, please report it.

## How it's built

- `game/Engine` is the duel engine: plain C# on .NET 8, no dependency on Godot. A duel is a starting setup plus a list
  of commands, and that one idea is what replays, LAN play and the CPU's look-ahead are all built on.
- `game/Engine.Check` is the test program. It runs ruling scenarios, plays CPU-vs-CPU duels, checks that two LAN
  peers stay in sync and that replays play back the same. It is also where the CPU gets trained and measured.
- `game/Godot` is the game (Godot 4.7, C#). The screens are built in code, not in the editor.
- `game/tools` has the packaging script and the generators for the playmats, card backs and sound effects I made.

## Building

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download) and [Godot 4.7 .NET](https://godotengine.org/download).

Run the engine tests (no Godot needed):

```bash
dotnet run --project game/Engine.Check -c Release
```

Run the game:

```bash
dotnet build game/Godot/PoC.csproj
godot --path game/Godot
```

The art and audio are not in this repository, so the game will not start from a fresh clone. [ASSETS.md](ASSETS.md)
explains what is missing and how to get it from an installed copy.

## Reporting bugs

Save a replay of the duel where it went wrong (the button is on the results screen) and attach it to an issue. A
replay reproduces the whole duel on my machine, which makes most bugs much quicker to find.

Fixes for card rulings are welcome. Please add a scenario to `game/Engine.Check/Scenarios.cs` that fails without your
change.

## Legal

This is an unofficial fan project. It is free, has no ads, and I do not take donations for it.

Yu-Gi-Oh!, Power of Chaos, and the card names, characters and artwork belong to their owners, including Konami and
Studio Dice/Shueisha. I am not affiliated with or endorsed by any of them. If you hold rights to something here and
want it gone, open an issue and I will remove it.

The code is under the [MIT License](LICENSE). That covers the code only.
