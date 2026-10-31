# ChastiMaid

> Become the perfect, chaste and humble maid you were always meant to be.

> [!WARNING]
> **18+ · Adults only.** ChastiMaid contains references to adult themes, such as chastity devices and diaper
> play. It has no explicit images, video or audio, but it is intended for adults only. See the
> [Disclaimer](#disclaimer).

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
- With the self-contained zip, nothing else to install: it includes the .NET runtime and the speech models.
  The framework-dependent zip needs the .NET 10 Runtime (x64), see below.
- Optional:
  - a webcam, for photo and video check-ins, Cornertime, the outfit video and the safe photo;
  - a microphone, for reading quotes aloud.

  Without them, the related check-ins are skipped or replaced by typing.

## Installing and running

There is no installer. Each release comes as two zips with the same game; pick one:

| Zip | Size | Needs |
|---|---|---|
| `ChastiMaid-<version>-win-x64-self-contained.zip` | ~410 MB | Nothing: the .NET runtime is included. **Choose this one if unsure.** |
| `ChastiMaid-<version>-win-x64-framework-dependent.zip` | ~360 MB | The [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (x64) installed on the computer |

The framework-dependent zip is smaller because it uses the .NET installed on the computer. If .NET 10 is
missing, Windows shows Microsoft's message "You must install or update .NET to run this application" with a
link to download it; install the ".NET Runtime 10" for x64 and start the game again.

Both contain a `ChastiMaid-<version>` folder:

```
ChastiMaid.exe          launcher: start the game from here
Bin/                    the application
Content/                seed contracts and speech models
LICENSE, THIRD-PARTY-NOTICES.md, SOURCE.txt, licenses/
```

Extract the folder anywhere you can write to and run `ChastiMaid.exe`. Your data is created in `data/` next
to it. Keep that folder to keep your saves.

Only the photo of the safe combination is encrypted. The other photos and videos the game takes are plain
files under `data/`. See the [user manual draft](docs/manual-draft.md).

## Building from source

Requires the .NET 10 SDK on Windows.

```powershell
git clone https://github.com/alyssacohen/ChastiMaid.git
cd ChastiMaid

# Optional: download the speech models (Whisper, Piper voices) into Content/Speech/
.\scripts\Download-SpeechModels.ps1

dotnet build                  # builds the app and the tests (ChastiMaid.slnx)
dotnet run                    # full-screen
dotnet run -- --landscape     # 1200×800 window, simulates a Surface Go
dotnet test                   # unit tests

.\publish.ps1                 # both release zips in dist/ (self-contained and framework-dependent)
```

Speech models are not tracked in git because of their size. Run the download script before
`publish.ps1`: it stops with a list of the missing files otherwise, and it checks each finished zip for the
licence files, the speech models and the right runtime before reporting its size.

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

Issues and suggestions are welcome. For pull requests, please open an issue first to discuss the change.

## Disclaimer

ChastiMaid is intended for adults (18+, or the age of majority where you live). It refers to adult themes in
text only.

The software is provided "as is", without warranty of any kind, express or implied (see sections 15 and 16 of
the [LICENSE](LICENSE)). You use it entirely at your own risk. The authors and contributors accept no
liability for any harm, injury, loss or damage arising from its use or misuse, including from the physical
activities it suggests, such as wearing garments or devices, kneeling, standing still or drinking water.
Nothing in the game overrides your health, your safety or your judgement: stop whenever something feels
unsafe.

## License

Copyright (C) 2026 Alyssa C.

ChastiMaid is free software: you can redistribute it and/or modify it under the terms of the GNU General
Public License as published by the Free Software Foundation, either version 3 of the License, or (at your
option) any later version. See [LICENSE](LICENSE).

The releases include third-party libraries, fonts and speech models under their own licences; see
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) and the `licenses/` folder. The two speech voices keep their
own terms: they are not covered by this project's licence and may restrict commercial use.
