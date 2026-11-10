<h1 align="center">Chaos Rift</h1>
<p align="center">All three <i>Power of Chaos</i> games in one, rebuilt from scratch and then taken further.</p>
<p align="center">
  <a href="https://3good5you.itch.io/chaos-rift"><b>Download (Windows / macOS)</b></a>
</p>

![Main menu](docs/screens/01-main-menu.jpg)

## What this is

*Power of Chaos* was three separate PC games: Yugi the Destiny, Kaiba the Revenge and Joey the Passion. Each had one
opponent, and carrying your cards from one to the next meant juggling installs. The series stopped in 2004.

Chaos Rift puts all three into one game with one collection and one deck builder, rebuilt from scratch, and then
keeps going: Jaden as a fourth rival, a card pool of 2,015 cards (the three originals together had under 800), new
modes, a tournament, and a duel table in 3D.

It is free and it will stay free. There is no account and nothing to buy.

![A duel](docs/screens/02-duel.jpg)

## What's in it

**The cards.** 2,015 of them, and each one does what its text says. I did not want "close enough" effects, so chains,
flip effects, fusions, rituals, counters, coin tosses and dice all go through a real rules engine. You can play under
the 2003 or the 2004 ban list.

**The rivals.** Yugi, Kaiba, Joey and Jaden. Each has a set of decks that gets nastier as you beat them. The old CPU
followed a fixed script and you could learn to walk around it. This one tries its options out on copies of the duel
before it commits. It does not look at your hand or your face-down cards to do that; it shuffles what it cannot see
and plays the turn out. In a best-of-three it also side-decks against what you showed it.

**Ways to play**

| | |
|---|---|
| Regular | One duel, or a best-of-three match with side decking |
| Gauntlet | Four rivals back to back with one deck. Your Life Points carry over |
| Pack Duel | Open six packs and duel with whatever you pulled |
| Limited Duel | A different restriction on your collection each day |
| Legend | A rival with their best deck. Pays the most |
| Chaos Duel | Every third turn a rule changes for both players |
| Challenges | Eight puzzles. You have one turn to win and there is one way to do it |

There is also an eight-player tournament, LAN duels for two PCs on the same network, and replays you can save and
watch later.

![Tournament](docs/screens/10-tournament.jpg)

**The collection.** You win cards after every duel, flipped face up one at a time, and buy the ones you are missing
with Duel Points in the Shop. Beat a rival for the first time and you get their starter deck, revealed card by card.

![Deck unlocked](docs/screens/14-deck-unlock.jpg)

**Make the table yours.** The Shop also sells 31 card backs, 20 playmats and 18 profile frames. Each playmat brings its
own world under the table, and a few can only be won by taking the tournament.

![Card backs in the shop](docs/screens/16-shop-card-backs.jpg)

**The deck builder.** Main, Side and Fusion decks, filters that stay out of the way, and import/export for `.ydk`
files and the original games' `.ydc` files, so your old decks still work.

![Deck builder](docs/screens/07-deck-builder.jpg)

**Small things I cared about.** Winning with Exodia or Destiny Board gets a proper finish and not a pop-up. Right
click always means back or pass. Space moves to the next phase. Double-click your deck to give up.

## Status

Version 1.0. The Windows build is the one I play and test. The macOS build exists but nobody has tried it on a real
Mac yet, so tell me if it works.

Known rough spots: the CPU still makes the odd strange play, and about 870 of the newer cards have only been checked
by the automated tests and not by hand in a real duel. If a card does something wrong, please report it.

## Coming next

- Online duels (today it is LAN only)
- Hand-checking the newer cards in real duels
- A smarter CPU in the late game
- A tested macOS build
- More rivals, decks, card backs and playmats

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
