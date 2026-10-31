# Builds the two release zips of ChastiMaid:
#   dist\ChastiMaid-<version>-win-x64-self-contained.zip        includes the .NET runtime: nothing to install
#   dist\ChastiMaid-<version>-win-x64-framework-dependent.zip   needs the .NET 10 Runtime (x64) installed
# Each zip holds one ChastiMaid-<version>\ folder: ChastiMaid.exe (launcher), Bin\, Content\ and the licence files.
# Data\ is not created here: DatabaseService creates it under AppPaths.Root on first run.
$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$dist = Join-Path $root "dist"

# The version comes from Directory.Build.props, the single place it is set.
[xml]$props = Get-Content (Join-Path $root "Directory.Build.props")
$version = @($props.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]
if (-not $version) { throw "No <Version> found in Directory.Build.props." }

# Refuse to build an incomplete release: the speech models are not in git (scripts\Download-SpeechModels.ps1).
$speech = Join-Path $root "Content\Speech"
$required = @(
    "STT\base-encoder.onnx", "STT\base-decoder.onnx", "STT\base-tokens.txt",
    "TTS\en\en_US-amy-medium.onnx", "TTS\en\en_US-amy-medium.onnx.json", "TTS\en\tokens.txt", "TTS\en\MODEL_CARD",
    "TTS\it\it_IT-paola-medium.onnx", "TTS\it\it_IT-paola-medium.onnx.json", "TTS\it\tokens.txt", "TTS\it\MODEL_CARD"
)
foreach ($lang in "en", "it") {
    # The files espeak-ng cannot start without.
    foreach ($f in "phontab", "phondata", "phonindex", "intonations") { $required += "TTS\$lang\espeak-ng-data\$f" }
}
$missing = $required | Where-Object { -not (Test-Path (Join-Path $speech $_)) }
if ($missing) {
    throw "Speech models or espeak-ng-data missing under Content\Speech (run scripts\Download-SpeechModels.ps1):`n  " +
        ($missing -join "`n  ")
}

function Invoke-Dotnet([string[]]$arguments) {
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($arguments -join ' ') failed with exit code $LASTEXITCODE" }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

# Reopens a finished zip and fails unless it holds every shipped file (licences, speech models, espeak-ng-data)
# and carries the .NET runtime exactly when it is the self-contained one.
function Test-ReleaseZip([string]$zip, [bool]$selfContained) {
    $prefix = "ChastiMaid-$version/"
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $names = [string[]]@($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
    }
    finally {
        $archive.Dispose()
    }
    $entries = [System.Collections.Generic.HashSet[string]]::new($names, [System.StringComparer]::OrdinalIgnoreCase)

    $expected = @("ChastiMaid.exe", "Bin/ChastiMaid.exe", "Bin/ChastiMaid.dll", "LICENSE", "THIRD-PARTY-NOTICES.md", "SOURCE.txt")
    foreach ($dir in "licenses", "Content\Speech") {
        $expected += @(Get-ChildItem (Join-Path $root $dir) -Recurse -File |
            ForEach-Object { $_.FullName.Substring($root.Length + 1).Replace('\', '/') })
    }

    $problems = @($expected | Where-Object { -not $entries.Contains($prefix + $_) } | ForEach-Object { "missing: $_" })
    $hasRuntime = $entries.Contains("${prefix}Bin/coreclr.dll")
    if ($selfContained -and -not $hasRuntime) { $problems += "missing: the .NET runtime (Bin/coreclr.dll)" }
    if (-not $selfContained -and $hasRuntime) { $problems += "unexpected: the .NET runtime (Bin/coreclr.dll)" }
    if ($problems) {
        throw "$zip is not a complete release:`n  " + ($problems -join "`n  ")
    }
}

function New-Release([string]$suffix, [bool]$selfContained) {
    $work = Join-Path $dist "_work$suffix"
    $folder = Join-Path $work "ChastiMaid-$version"
    $appOut = Join-Path $work "_app"
    $launcherOut = Join-Path $work "_launcher"
    $zip = Join-Path $dist "ChastiMaid-$version-win-x64$suffix.zip"
    Remove-Item $work, $zip -Recurse -Force -ErrorAction SilentlyContinue

    # App: Windows x64 only (drops the Linux/macOS/Android native libraries), ReadyToRun for a faster start-up
    # on low-power devices. No trimming: Avalonia and reflection make it unsafe.
    Invoke-Dotnet @("publish", (Join-Path $root "ChastiMaid.csproj"), "-c", "Release", "-o", $appOut,
        "-r", "win-x64", "--self-contained", "$selfContained".ToLower(), "-p:PublishReadyToRun=true")

    # Launcher: one exe. The self-contained one carries its own trimmed runtime (it only starts a process).
    $launcherArgs = @("publish", (Join-Path $root "ChastiMaid.Launcher\ChastiMaid.Launcher.csproj"), "-c", "Release",
        "-o", $launcherOut, "-r", "win-x64", "--self-contained", "$selfContained".ToLower(), "-p:PublishSingleFile=true")
    if ($selfContained) { $launcherArgs += "-p:PublishTrimmed=true" }
    Invoke-Dotnet $launcherArgs

    # Not shipped: debug symbols, and the FFmpeg plugin of OpenCV (videos are MJPG/AVI, written without it).
    Get-ChildItem $appOut -Recurse -Include "*.pdb", "opencv_videoio_ffmpeg*.dll" | Remove-Item -Force

    New-Item -ItemType Directory -Force -Path (Join-Path $folder "Bin") | Out-Null
    Move-Item (Join-Path $appOut "Content") (Join-Path $folder "Content")
    Move-Item (Join-Path $appOut "*") (Join-Path $folder "Bin")
    Copy-Item (Join-Path $launcherOut "ChastiMaid.exe") $folder
    foreach ($f in "LICENSE", "THIRD-PARTY-NOTICES.md", "SOURCE.txt", "licenses") {
        Copy-Item (Join-Path $root $f) $folder -Recurse
    }

    # includeBaseDirectory = true: the zip contains the ChastiMaid-<version> folder itself.
    [System.IO.Compression.ZipFile]::CreateFromDirectory($folder, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $true)
    Remove-Item $work -Recurse -Force
    Test-ReleaseZip $zip $selfContained

    $mb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
    Write-Host "Created $zip ($mb MB)"
}

New-Item -ItemType Directory -Force -Path $dist | Out-Null
New-Release "-self-contained" $true
New-Release "-framework-dependent" $false
