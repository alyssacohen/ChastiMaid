# Licence audit — ChastiMaid 0.1.0

Technical inventory of what a release ships and under which terms, compiled on 2026-10-30 from the real
publish output. It is not legal advice.

**How it was checked.**
- Publish: `dotnet publish` with the settings `publish.ps1` used at commit `31d0d3f` (app
  framework-dependent and RID-agnostic; launcher self-contained `win-x64` single file), into a scratch folder
  outside the repo.
- NuGet licences: read from each package's `.nuspec` in the local NuGet cache, for every package listed by
  `dotnet list package --include-transitive`.
- Native DLLs: string search and PE import table.
- Upstream sources: read at the exact tag or commit (raw files from GitHub and Hugging Face).
- Fonts: read from their `name` table.

| Component | Version | Licence | Where it ships | Obligation | Status |
|---|---|---|---|---|---|
| ChastiMaid (this project) | 0.1.0 | GPL-3.0-or-later (decided) | `Bin/ChastiMaid.dll`, `ChastiMaid.exe` (launcher) | Ship `LICENSE`; make the corresponding source available | `LICENSE` missing |
| .NET runtime | 10.0.11 (runtime pack `microsoft.netcore.app.runtime.win-x64`, commit `e2f47b0110ed`) | MIT + third-party notices. Per "License information for .NET on Windows", `coreclr.dll`, `Microsoft.DiaSymReader.Native.*.dll` and runtimes inside single-file binaries are under the Microsoft .NET Library License | Embedded in the launcher today; `Bin/` once the app is self-contained | Ship the runtime `LICENSE.TXT`, `THIRD-PARTY-NOTICES.TXT` and the .NET Library License | Texts in `licenses/` |
| Avalonia (`Avalonia`, `.Desktop`, `.Themes.Fluent`, `.Skia`, `.Win32`, `.X11`, `.Native`, `.FreeDesktop`, `.FreeDesktop.AtSpi`, `.HarfBuzz`, `.Remote.Protocol`) | 12.1.1 | MIT | `Bin/Avalonia*.dll` | Copyright + MIT text | Not shipped yet |
| `Avalonia.Angle.Windows.Natives` (ANGLE) | 2.1.27548.20260419 (DLL 2.1.1, git `1c89805903c1`) | BSD-3-Clause style ("Copyright 2018 The ANGLE Project Authors", package `LICENSE` file) | `Bin/runtimes/win-*/native/av_libglesv2.dll` | Copyright + licence text | Not shipped yet |
| SkiaSharp (+ `NativeAssets.Win32`) | 3.119.4 | MIT; package ships `THIRD-PARTY-NOTICES.txt` (Skia, ANGLE and others) | `Bin/SkiaSharp.dll`, `libSkiaSharp.dll` | MIT text + the package's third-party notices | Not shipped yet |
| HarfBuzzSharp (+ `NativeAssets.Win32`) | 8.3.1.3 | MIT; package ships `THIRD-PARTY-NOTICES.txt` | `Bin/HarfBuzzSharp.dll`, `libHarfBuzzSharp.dll` | MIT text + the package's third-party notices | Not shipped yet |
| `MicroCom.Runtime` | 0.11.6 | MIT | `Bin/` | MIT text | Not shipped yet |
| `Tmds.DBus.Protocol` (Linux only, still copied) | 0.94.1 | MIT | `Bin/` | MIT text | Not shipped yet |
| ReactiveUI (`ReactiveUI`, `.Core`, `.Avalonia` 12.1.1), `ReactiveUI.Primitives*`, `ReactiveUI.Disposables` | 24.1.0 / 7.1.0 | MIT | `Bin/ReactiveUI*.dll` | MIT text | Not shipped yet |
| Splat (`Splat`, `.Core`, `.Builder`, `.Logging`) | 20.2.0 | MIT | `Bin/Splat*.dll` | MIT text | Not shipped yet |
| `ReactiveUI.SourceGenerators` | 3.2.0 | MIT | Build time only, not shipped | None | OK |
| `System.Reactive` | 7.0.0 | MIT | `Bin/` | MIT text | Not shipped yet |
| `System.Numerics.Tensors` | 9.0.0 | MIT | `Bin/` | MIT text | Not shipped yet |
| LiteDB | 5.0.21 | MIT | `Bin/LiteDB.dll` | MIT text | Not shipped yet |
| NAudio (`NAudio.Core`, `NAudio.WinMM`; the `NAudio` meta-package was replaced so WinForms is no longer pulled in) | 3.0.0 | MIT | `Bin/NAudio*.dll` | MIT text | Not shipped yet |
| GetText.NET | 10.0.1 | MIT (package `LICENSE` file; includes "Copyright (c) 2012 Vitaly Zilnik", NGettext) | `Bin/GetText.dll` | MIT text with both copyright lines | Not shipped yet |
| OpenCvSharp4 + `OpenCvSharp4.runtime.win` | 4.13.0.20260627 | Apache-2.0 | `Bin/OpenCvSharp.dll`, `OpenCvSharpExtern.dll` | Apache text | Not shipped yet |
| OpenCV (inside `OpenCvSharpExtern.dll`) | 4.13.0 | Apache-2.0; bundles third-party libraries | `OpenCvSharpExtern.dll` | Apache text + OpenCV third-party notices (TODO: list them) | TODO |
| FFmpeg (OpenCV videoio plugin) | 4.4.6 (DLL 2025.12.0), built with libaom, libopenh264, libvpx | "LGPL version 2.1 or later" (embedded string; no `--enable-gpl` / `--enable-nonfree`) | `Bin/runtimes/win-x64/native/opencv_videoio_ffmpeg4130_64.dll` | None if not shipped | Removed from both zips by `publish.ps1`; video recording still to be tested |
| sherpa-onnx (managed + native) | 1.13.5, tag commit `3dc7c569f31ca2cd4a20ed6f7db780327e6714c5` (built Aug 11 2026) | Apache-2.0 | `Bin/sherpa-onnx.dll`, `sherpa-onnx-c-api.dll` | Apache text | Not shipped yet |
| **espeak-ng**, statically linked into `sherpa-onnx-c-api.dll` | 1.52.0.1, `csukuangfj/espeak-ng` @ `ed530aa113046142eb5115cf2fc9157854d0ffe1` | GPL-3.0-or-later (plus BSD-2 `getopt.c`, `COPYING.APACHE`, `COPYING.UCD`) | `sherpa-onnx-c-api.dll` | GPL-3.0 text; corresponding source at that commit | **Confirmed** (evidence below) |
| piper-phonemize, statically linked into `sherpa-onnx-c-api.dll` | `csukuangfj/piper-phonemize` @ `f3ff95afc03640bc1399e113e83361192a2fafb4` | MIT ("Copyright (c) 2023 Michael Hansen"); its `licenses/uni-algo` is public domain | `sherpa-onnx-c-api.dll` | MIT text | Not shipped yet |
| OpenFst, kaldi-decoder, simple-sentencepiece, nlohmann/json, statically linked into `sherpa-onnx-c-api.dll` (found by symbol search; kaldi-native-fbank, Eigen, hclust-cpp and cargs were not found) | `csukuangfj/openfst` v1.8.5-2026-07-09 / 0.3.0 / 0.7 / 3.12.0 | Apache-2.0 / Apache-2.0 / Apache-2.0 / MIT | `sherpa-onnx-c-api.dll` | Licence texts, copyright lines | Listed in `THIRD-PARTY-NOTICES.md` |
| ONNX Runtime | 1.27.1 | MIT ("© Microsoft Corporation") | `Bin/runtimes/*/native/onnxruntime.dll` | MIT text + its `ThirdPartyNotices.txt` | In `licenses/` |
| Windows SDK projection `Microsoft.Windows.SDK.NET.dll` | 10.0.26100.55 (ref pack 10.0.26100.57) | Microsoft terms at `https://aka.ms/WinSDKLicenseURL` (nuspec `licenseUrl`) | `Bin/` | TODO: read the terms | TODO |
| `WinRT.Runtime.dll` (C#/WinRT) | 2.2.0.48161 | MIT ("Copyright (c) Microsoft Corporation.", CsWinRT `LICENSE` at tag `2.2.0.241111.1`) | `Bin/` | MIT text | Listed in `THIRD-PARTY-NOTICES.md` |
| Whisper `base` (sherpa-onnx ONNX export) | `sherpa-onnx-whisper-base` archive | MIT ("Copyright (c) 2022 OpenAI"; upstream README: "code and model weights are released under the MIT License") | `Content/Speech/STT/base-*.onnx`, `base-tokens.txt` | MIT text | Not shipped yet |
| Piper voice `en_US-amy-medium` | piper_version 1.0.0 | MODEL_CARD: "License: See URL" (URL `https://github.com/MycroftAI/mimic3-voices`); "Finetuned from U.S. English lessac voice (medium quality)" | `Content/Speech/TTS/en/` | Unclear | Open issue 2 |
| Piper voice `it_IT-paola-medium` | piper_version 1.0.0 | MODEL_CARD: "License: See URL" (URL `https://huggingface.co/datasets/paolapersico1/Voice-Dataset-Italian`); "Finetuned from U.S. English lessac voice (medium quality)" | `Content/Speech/TTS/it/` | Unclear | Open issue 2 |
| `espeak-ng-data` (identical copy in both voice folders) | phondata version header `0x014801`; producing espeak-ng commit TODO | GPL-3.0-or-later (part of espeak-ng) | `Content/Speech/TTS/{en,it}/espeak-ng-data/` | GPL-3.0 text; source | TODO: exact source |
| Inter (inside `Avalonia.Fonts.Inter.dll`) | 3.019 | SIL OFL 1.1 ("Copyright 2020 The Inter Project Authors") | `Bin/Avalonia.Fonts.Inter.dll` | OFL text with the font | Not shipped yet |
| Cormorant Infant | 4.001 | SIL OFL 1.1 ("Copyright 2015 The Cormorant Project Authors") | Embedded in `Bin/ChastiMaid.dll` (`Assets/Fonts`) | OFL text with the font | Licence file missing |
| Special Elite | 1.001 | Apache-2.0 ("Copyright (c) 2010 by Brian J. Bonislawsky DBA Astigmatic (AOETI)") | Embedded in `Bin/ChastiMaid.dll` | Apache text + copyright line | Licence file missing |
| Lucide icon font (`lucide.ttf`, from the `lucide-font-1.63.0.zip` release asset) | 1.63.0 (per the author; the font file itself has no version) | ISC ("Copyright (c) 2026 Lucide Icons and Contributors") + MIT for the Feather-derived icons ("Copyright (c) 2013-present Cole Bemis"), upstream `LICENSE` at tag `1.63.0` | Embedded in `Bin/ChastiMaid.dll` | Full upstream `LICENSE` (both notices) | Licence file missing |
| Sounds (`Assets/Sounds/*.wav`, 6 files) | — | Own work (per the author): project licence | Embedded in `Bin/ChastiMaid.dll` | — | OK |
| Card back (`deck-rear.png/.svg`), `app.ico` | — | Own work (per the author): project licence | Embedded in `Bin/ChastiMaid.dll`, exe icons | — | OK |
| Seed contracts, translations (`Content/{en,it}`, `Content/locale`) | — | Project's own (GPL-3.0-or-later) | `Content/` | — | OK |

**Evidence for espeak-ng in `sherpa-onnx-c-api.dll`.**
- The DLL imports only `onnxruntime.dll`, `KERNEL32.dll` and `ADVAPI32.dll`, yet contains the espeak-ng API
  (`espeak_Initialize`, `espeak_Cancel`, `espeak_CompileDictionary`, …) and its messages ("Failed to
  initialize espeak-ng with data dir: %s", "Wrong version of espeak-ng-data").
- It contains `ESPEAK_DATA_PATH` and the version string `1.52.0.1`, which matches `project(espeak-ng VERSION
  1.52.0.1)` in the fork at the commit above.
- It contains piper-phonemize symbols (`?phonemize_eSpeak@piper@@…`).
- sherpa-onnx `v1.13.5` `cmake/espeak-ng-for-piper.cmake` and `cmake/piper-phonemize.cmake` fetch the two
  commits above; piper-phonemize is built with `BUILD_SHARED_LIBS OFF`.

## Open issues

Notices approach: one copy of each licence text in `licenses/` (MIT, Apache-2.0, BSD-3-Clause, BSD-2-Clause,
ISC+MIT for Lucide, OFL-1.1, Unicode), all copyright lines in `THIRD-PARTY-NOTICES.md`, and the vendors' own
notices files for the .NET runtime, SkiaSharp/HarfBuzzSharp and ONNX Runtime. GPL text: `LICENSE`.


1. **FFmpeg may be unnecessary.** Decision: leave it out of the release if video recording works without it
   (to test in Phase 4). The app writes MJPG in `.avi` (`WebcamPreviewService.cs:376`), which
   OpenCV can do without FFmpeg, and captures through Windows' own backends. Leaving
   `opencv_videoio_ffmpeg4130_64.dll` out of the release would remove the LGPL/codec obligations. Not tested.
   If FFmpeg ever comes back: check how OpenH264 was built into it (Cisco's patent terms concern Cisco's own binaries).
2. **Voice licences unclear.** Decision: keep both voices, with a notice in `THIRD-PARTY-NOTICES.md` that
   they keep their own terms.
   - Both MODEL_CARDs only say "See URL".
   - The Amy URL (`mimic3-voices`) has no `amy` voice in `voices/en_US`, so its terms are not documented there.
   - The Paola dataset's Hugging Face metadata says `cc0-1.0`.
   - Both voices are fine-tuned from `lessac`, whose dataset page is a Blizzard 2013 "RESEARCH LICENCE
     AGREEMENT".
3. ~~**Lucide version and licence.**~~ Resolved: release 1.63.0, ISC + MIT (see table).
4. ~~**Provenance of sounds, card back and `app.ico`.**~~ Resolved: the author's own work.
5. **Platform-specific clutter.** The current publish copies native libraries for Linux, macOS, Android,
   win-x86 and win-arm64 (about 870 MB under `runtimes/`). A `win-x64` self-contained publish should drop them;
   to confirm in Phase 4.
6. **Still open:**
   - whether `Microsoft.Windows.SDK.NET.dll` is listed as Distributable Code in the Windows SDK terms;
   - the notices of the libraries OpenCV bundles into `OpenCvSharpExtern.dll`;
   - the exact espeak-ng source of `espeak-ng-data`.
   Read and shipped: the .NET runtime notices, the .NET Library License, and ONNX Runtime's notices.
7. **Different terms for part of .NET.** The .NET Library License covers `coreclr.dll` and the single-file
   launcher's runtime. Among other things, it asks distributors to bind end users to terms protecting the code
   "at least as much as this agreement". The project licence is GPL-3.0-or-later. Reported as found, without
   drawing conclusions.
