# Third-party notices

ChastiMaid is licensed under the GNU General Public License, version 3 or (at your option) any later version
(see `LICENSE`). Copyright (C) 2026 Alyssa C.

The release also ships the third-party components below. Each keeps its own licence; one copy of each licence
text is in `licenses/`, and the copyright lines are quoted from upstream.

## .NET runtime

The first two rows apply to the self-contained release (`ChastiMaid-<version>-win-x64.zip`) only. The
framework-dependent release uses the .NET runtime installed on the computer.

| Component | Version | Licence | Copyright | Upstream |
|---|---|---|---|---|
| .NET runtime | 10.0.11 | MIT (`licenses/MIT.txt`) and its own notices (`licenses/dotnet-THIRD-PARTY-NOTICES.txt`) | Copyright (c) .NET Foundation and Contributors | https://github.com/dotnet/runtime |
| `coreclr.dll`, `Microsoft.DiaSymReader.Native.amd64.dll`, the runtime inside the single-file launcher | 10.0.11 | Microsoft .NET Library License (`licenses/dotnet-library-license.txt`), per https://github.com/dotnet/core/blob/main/license-information-windows.md | Microsoft Corporation | https://dotnet.microsoft.com/dotnet_library_license.htm |
| Windows SDK projection (`Microsoft.Windows.SDK.NET.dll`) | 10.0.26100.55 | Microsoft Windows SDK licence terms | Copyright (c) Microsoft Corporation. All rights reserved. | https://aka.ms/WinSDKLicenseURL |
| C#/WinRT runtime (`WinRT.Runtime.dll`) | 2.2.0 | MIT | Copyright (c) Microsoft Corporation. | https://github.com/microsoft/CsWinRT |

## Libraries

| Component | Version | Licence | Copyright | Upstream |
|---|---|---|---|---|
| Avalonia (incl. `Avalonia.Fonts.Inter`) | 12.1.1 | MIT | Copyright (c) AvaloniaUI OÜ | https://github.com/AvaloniaUI/Avalonia |
| ReactiveUI | 24.1.0 | MIT | Copyright (c) .NET Foundation and Contributors | https://github.com/reactiveui/reactiveui |
| ReactiveUI.Avalonia | 12.1.1 | MIT | Copyright (c) 2019-2026 ReactiveUI and Avalonia Teams, and Contributors | https://github.com/reactiveui/ReactiveUI.Avalonia |
| ReactiveUI.Primitives, ReactiveUI.Disposables | 7.1.0 | MIT | Copyright (c) 2019-2026 ReactiveUI Association Incorporated | https://github.com/reactiveui/Primitives |
| Splat | 20.2.0 | MIT | Copyright (c) ReactiveUI 2018 - 2025 | https://github.com/reactiveui/splat |
| System.Reactive | 7.0.0 | MIT | Copyright (c) .NET Foundation and Contributors | https://github.com/dotnet/reactive |
| System.Numerics.Tensors | 9.0.0 | MIT | Copyright (c) .NET Foundation and Contributors | https://github.com/dotnet/runtime |
| LiteDB | 5.0.21 | MIT | Copyright (c) 2014-2022 Mauricio David | https://github.com/mbdavid/LiteDB |
| NAudio (`NAudio.Core`, `NAudio.WinMM`) | 3.0.0 | MIT | Copyright 2008-2026 Mark Heath | https://github.com/naudio/NAudio |
| GetText.NET | 10.0.1 | MIT | Copyright (c) 2012 Vitaly Zilnik; Copyright (c) 2020 perpetualKid | https://github.com/perpetualKid/GetText.NET |
| MicroCom.Runtime | 0.11.6 | MIT | Copyright (c) 2021 Nikita Tsukanov | https://github.com/kekekeks/MicroCom |
| Tmds.DBus.Protocol | 0.94.1 | MIT | Copyright 2006 Alp Toker <alp@atoker.com>; Copyright 2010 Other Contributors; Copyright 2016 Tom Deseyn <tom.deseyn@gmail.com> | https://github.com/tmds/Tmds.DBus |
| SkiaSharp, HarfBuzzSharp (incl. native Skia and HarfBuzz) | 3.119.4 / 8.3.1.3 | MIT, plus `licenses/SkiaSharp-HarfBuzzSharp-THIRD-PARTY-NOTICES.txt` | Copyright (c) 2015-2016 Xamarin, Inc.; Copyright (c) 2017-2018 Microsoft Corporation. | https://github.com/mono/SkiaSharp |
| ANGLE (`av_libglesv2.dll`) | 2.1.1 (`1c89805903c1`) | BSD-3-Clause (`licenses/BSD-3-Clause-ANGLE.txt`) | Copyright 2018 The ANGLE Project Authors. | https://github.com/AvaloniaUI/angle |
| OpenCvSharp | 4.13.0.20260627 | Apache-2.0 | shimat (package author) | https://github.com/shimat/opencvsharp |
| OpenCV (inside `OpenCvSharpExtern.dll`) | 4.13.0 | Apache-2.0 | Copyright (C) 2000-2022, Intel Corporation; Copyright (C) 2009-2011, Willow Garage Inc.; Copyright (C) 2009-2016, NVIDIA Corporation; Copyright (C) 2010-2013, Advanced Micro Devices, Inc.; Copyright (C) 2015-2023, OpenCV Foundation; Copyright (C) 2008-2016, Itseez Inc.; Copyright (C) 2019-2023, Xperience AI; Copyright (C) 2019-2022, Shenzhen Institute of Artificial Intelligence and Robotics for Society; Copyright (C) 2022-2023, Southern University of Science And Technology; Copyright (C) 2023-2025, OpenCV AI. All rights reserved. | https://github.com/opencv/opencv |
| ONNX Runtime | 1.27.1 | MIT, plus `licenses/onnxruntime-ThirdPartyNotices.txt` | Copyright (c) Microsoft Corporation | https://github.com/microsoft/onnxruntime |
| sherpa-onnx | 1.13.5 (`3dc7c569`) | Apache-2.0 | The Next-gen Kaldi development team (package author) | https://github.com/k2-fsa/sherpa-onnx |
| espeak-ng (statically linked into `sherpa-onnx-c-api.dll`) | 1.52.0.1 (`csukuangfj/espeak-ng` @ `ed530aa1`) | GPL-3.0-or-later (`LICENSE`); `getopt.c` BSD-2-Clause (`licenses/BSD-2-Clause-espeak-ng-getopt.txt`); Unicode data (`licenses/Unicode-espeak-ng-UCD.txt`) | See the notices in the espeak-ng source files | https://github.com/csukuangfj/espeak-ng |
| piper-phonemize (statically linked) | `f3ff95af` | MIT | Copyright (c) 2023 Michael Hansen | https://github.com/csukuangfj/piper-phonemize |
| OpenFst (statically linked) | 1.8.5 | Apache-2.0 | Copyright 2005-2026 Google LLC. | https://github.com/csukuangfj/openfst |
| kaldi-decoder (statically linked) | 0.3.0 | Apache-2.0 | No copyright line in the upstream `LICENSE` | https://github.com/k2-fsa/kaldi-decoder |
| simple-sentencepiece (statically linked) | 0.7 | Apache-2.0 | No copyright line in the upstream `LICENSE` | https://github.com/pkufool/simple-sentencepiece |
| nlohmann/json (statically linked) | 3.12.0 | MIT | Copyright (c) 2013-2025 Niels Lohmann | https://github.com/nlohmann/json |

## Speech models and data

| Component | Version | Licence | Copyright | Upstream |
|---|---|---|---|---|
| Whisper `base` (speech recognition) | sherpa-onnx export | MIT | Copyright (c) 2022 OpenAI | https://github.com/openai/whisper |
| `espeak-ng-data` (in each voice folder) | from the sherpa-onnx voice archives | GPL-3.0-or-later (`LICENSE`) | See the notices in the espeak-ng source files | https://github.com/espeak-ng/espeak-ng |
| Voices `en_US-amy-medium`, `it_IT-paola-medium` | Piper 1.0.0 | Their own terms: see below | — | https://huggingface.co/rhasspy/piper-voices |

**Speech voices.** The two voices **keep their own terms**, are **not** covered by ChastiMaid's licence, and
**may restrict commercial use**. Their `MODEL_CARD` (in each voice folder) states, verbatim:

- `en_US-amy-medium`: "URL: https://github.com/MycroftAI/mimic3-voices", "License: See URL".
- `it_IT-paola-medium`: "URL: https://huggingface.co/datasets/paolapersico1/Voice-Dataset-Italian", "License: See URL".

Both say "Finetuned from U.S. English lessac voice (medium quality)". The `lessac` dataset licence is given as
https://www.cstr.ed.ac.uk/projects/blizzard/2013/lessac_blizzard2013/license.html ("License for Blizzard 2013
Materials", a research licence agreement).

## Fonts and icons

| Component | Version | Licence | Copyright | Upstream |
|---|---|---|---|---|
| Inter | 3.019 | SIL OFL 1.1 (`licenses/OFL-1.1.txt`) | Copyright (c) 2016-2020 The Inter Project Authors. | https://github.com/rsms/inter |
| Cormorant Infant | 4.001 | SIL OFL 1.1 (`licenses/OFL-1.1.txt`) | Copyright 2015 the Cormorant Project Authors (github.com/CatharsisFonts/Cormorant) | https://github.com/CatharsisFonts/Cormorant |
| Special Elite | 1.001 | Apache-2.0 | Copyright (c) 2010 by Brian J. Bonislawsky DBA Astigmatic (AOETI). All rights reserved. | https://github.com/google/fonts/tree/main/apache/specialelite |
| Lucide | 1.63.0 | ISC; MIT for the icons derived from Feather (`licenses/ISC-MIT-Lucide.txt`) | Copyright (c) 2026 Lucide Icons and Contributors; Copyright (c) 2013-present Cole Bemis | https://github.com/lucide-icons/lucide |

MIT and Apache-2.0 texts: `licenses/MIT.txt`, `licenses/Apache-2.0.txt`. Source code for the GPL parts:
`SOURCE.txt`.
