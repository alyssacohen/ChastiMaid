# ChastiMaid

> Become the perfect, chaste and humble maid you were always meant to be.

ChastiMaid is a full-screen Windows desktop game that turns household chores into a role-play. You are a
maid working off a randomly generated virtual debt owed to your employer, Madame. Chores, attendance
check-ins and punishments move the debt up or down; the game is over when it is paid in full and every punishment is served.

The interface is available in English and Italian.

## Features

- **Contract-based games.** Each new game starts from a contract: debt range, interest, wages, chores,
  wardrobe, punishments and permissions. Contracts can be exported and imported as a single JSON file.
  Up to 5 saved games.
- **Daily outfit.** Madame proposes an outfit from your wardrobe, built from layers, slots, tags and
  requires/incompatible rules. Regenerating or composing it by hand lowers your earning bonus.
- **Chores in busy mode.** Each chore is timed against a hidden target. Ending too early voids the pay;
  ending too late halves it. The webcam silently takes photos and short videos while you work.
  One-off "occasional tasks" can be added from the dashboard.
- **Check-in popups in idle mode.** At random intervals a bell summons you to:
  - pose for the camera;
  - record a curtsy;
  - read a quote aloud (speech recognition);
  - kneel;
  - drink water;
  - answer an impossible question;
  - check an earlier photo against the outfit you were wearing.
  An idle wage is paid at regular intervals.
- **Punishments.** Fines, Cornertime (camera motion detection), Writing Lines (typing drill) and Forced
  Outfit (real-time). Severity scales with how displeased Madame currently is.
- **Permissions.** Ask for short breaks (toilet, sit down, cigarette, tea or coffee), each with its own
  grant chance, duration and cooldown.
- **Shop and Fortune Deck.** Spend money on cards that improve permission requests or soften and cancel
  punishments, or gamble on a draw from *Il Ventaglio della Sorte*.
- **Safe lock.** Lock a distracting object in a real combination safe. The app keeps an encrypted photo
  of the combination and shows it only at the end of a session (at a cost) or of the game.
- **Optional extras.** Chastity Device (a marked item added to every outfit, changed only when the safe
  is opened), Diaper Play and Plug & Play.
- **Local and offline.** Data stays in a `data/` folder next to the program. Speech recognition and voice
  run offline.

The [user manual draft](docs/manual-draft.md) describes every screen and setting.

## Requirements

- Windows 10 version 1809 or later, or Windows 11, on x64.
- A landscape display. The layout is designed for 1200×800 logical pixels and up, with touch-sized
  controls for tablets.
- Optional:
  - a webcam, for photo and video check-ins, Cornertime, the outfit video and the safe photo;
  - a microphone, for reading quotes aloud;
  - the speech models (see below).

  Without them, the related check-ins are skipped or replaced by typing.

## Installing and running

There is no installer. A release is a folder produced by `publish.ps1`:

```
ChastiMaid.exe   launcher: start the game from here
Bin/             the application
Content/         seed contracts and speech models
```

Copy the folder anywhere you can write to and run `ChastiMaid.exe`. Your data is created in `data/` next
to it. Keep that folder to keep your saves.

<!-- TODO: Bin/ is published framework-dependent. Decide whether a release requires the .NET 10 Desktop
     Runtime to be installed, or make publish.ps1 self-contained, and state it here. -->

## Building from source

Requires the .NET 10 SDK on Windows.

```powershell
git clone <repository-url>
cd ChastiMaid

# Optional: download the speech models (Whisper, Piper voices) into Content/Speech/
.\scripts\Download-SpeechModels.ps1

dotnet build                  # builds the app and the tests (ChastiMaid.slnx)
dotnet run                    # full-screen
dotnet run -- --landscape     # 1200×800 window, simulates a Surface Go
dotnet test                   # unit tests

.\publish.ps1                 # release folder in dist/Root/
```

Speech models are not tracked in git because of their size. Run the download script before
`publish.ps1` so they are copied into the release.

Translations live in `locale/*.po` and are compiled on every build. After changing user-facing strings,
run `.\l10n-refresh.ps1`. See [docs/design/localization.md](docs/design/localization.md).

## Tech stack

- C# / .NET 10, Avalonia UI 12 with ReactiveUI (MVVM)
- LiteDB 5 (password-encrypted database files)
- OpenCvSharp4 (webcam capture and motion detection)
- sherpa-onnx: Whisper (offline speech recognition) and Piper voices (offline text-to-speech); NAudio
- GetText.NET (gettext `.po` localization)
- xUnit (tests)

## Project status

This is an early **0.x** release. Expect rough edges, and no stability guarantees: features, settings,
the contract file format and save files may change between versions.

## Documentation

- [docs/manual-draft.md](docs/manual-draft.md): user manual (draft)
- [docs/specs/](docs/specs/): gameplay, wardrobe, punishments, permissions, shop, Fortune Deck
- [docs/design/](docs/design/): tone guide, localization, contract format, UI designs

## Contributing

<!-- TODO: decide on a contribution policy (no CONTRIBUTING.md yet). -->
Contribution guidelines have not been defined yet.

## License

<!-- TODO: no LICENSE file yet. Choose a license and add it, together with the license texts of the
     bundled fonts (Cormorant Infant, Special Elite, Lucide) and speech models. -->
**TODO:** no license has been chosen yet.
