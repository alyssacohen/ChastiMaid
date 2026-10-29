<#
.SYNOPSIS
    Downloads and extracts the sherpa-onnx TTS/STT models ChastiMaid bundles with its build.

.DESCRIPTION
    Populates Content\Speech\{TTS\it, TTS\en, STT} from the official k2-fsa/sherpa-onnx GitHub
    releases. Run once after cloning/building, before `dotnet publish`. Idempotent — safe to
    re-run, overwrites any partial/previous download.

    Speech recognition uses the multilingual Whisper "base" model (~200 MB), a good compromise for a
    short read-aloud phrase on modest hardware.
#>

$ErrorActionPreference = "Stop"

# Must match SherpaOnnxSttService.WhisperVariant: the app only loads the "base-*" files.
$WhisperVariant = "base"

# ponytail: pin to the Windows-native bsdtar explicitly — an MSYS/Git-Bash tar earlier on PATH
# mis-parses a "C:\..." destination as a remote "host:path" spec and fails.
$tarExe = Join-Path $env:SystemRoot "System32\tar.exe"

$root = Split-Path -Parent $PSScriptRoot
$speechDir = Join-Path $root "Content\Speech"
$ttsItDir = Join-Path $speechDir "TTS\it"
$ttsEnDir = Join-Path $speechDir "TTS\en"
$sttDir = Join-Path $speechDir "STT"

$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "chastimaid-speech-models-$([guid]::NewGuid())"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

function Get-AndExtract {
    param(
        [string]$Url,
        [string]$ArchiveName,
        [string]$Destination,
        # Names (wildcards allowed) of archive entries the app never loads, left out of $Destination
        [string[]]$Exclude = @()
    )

    Write-Host "Downloading $ArchiveName ..."
    $archivePath = Join-Path $tempDir $ArchiveName
    Invoke-WebRequest -Uri $Url -OutFile $archivePath

    Write-Host "Extracting $ArchiveName ..."
    $extractDir = Join-Path $tempDir ([System.IO.Path]::GetFileNameWithoutExtension([System.IO.Path]::GetFileNameWithoutExtension($ArchiveName)))
    New-Item -ItemType Directory -Path $extractDir -Force | Out-Null
    & $tarExe -xjf $archivePath -C $extractDir
    if ($LASTEXITCODE -ne 0) {
        throw "tar failed to extract $ArchiveName"
    }

    # These archives always nest their content in a single top-level folder — flatten it away so
    # $Destination gets the model files directly, not a redundant extra subfolder.
    $topLevel = @(Get-ChildItem $extractDir)
    $sourceDir = if ($topLevel.Count -eq 1 -and $topLevel[0].PSIsContainer) { $topLevel[0].FullName } else { $extractDir }

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    Get-ChildItem $sourceDir -Exclude $Exclude | ForEach-Object {
        Copy-Item $_.FullName -Destination $Destination -Recurse -Force
    }
}

try {
    Get-AndExtract `
        -Url "https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/vits-piper-it_IT-paola-medium.tar.bz2" `
        -ArchiveName "vits-piper-it_IT-paola-medium.tar.bz2" `
        -Destination $ttsItDir

    Get-AndExtract `
        -Url "https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/vits-piper-en_US-amy-medium.tar.bz2" `
        -ArchiveName "vits-piper-en_US-amy-medium.tar.bz2" `
        -Destination $ttsEnDir

    Get-AndExtract `
        -Url "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-whisper-$WhisperVariant.tar.bz2" `
        -ArchiveName "sherpa-onnx-whisper-$WhisperVariant.tar.bz2" `
        -Destination $sttDir `
        -Exclude "*.int8.onnx", "test_wavs"
}
finally {
    Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "Verifying..."
$expected = @(
    (Join-Path $ttsItDir "it_IT-paola-medium.onnx"),
    (Join-Path $ttsItDir "tokens.txt"),
    (Join-Path $ttsItDir "espeak-ng-data"),
    (Join-Path $ttsEnDir "en_US-amy-medium.onnx"),
    (Join-Path $ttsEnDir "tokens.txt"),
    (Join-Path $ttsEnDir "espeak-ng-data"),
    (Join-Path $sttDir "$WhisperVariant-encoder.onnx"),
    (Join-Path $sttDir "$WhisperVariant-decoder.onnx"),
    (Join-Path $sttDir "$WhisperVariant-tokens.txt")
)

$missing = $expected | Where-Object { -not (Test-Path $_) }
if ($missing) {
    Write-Error "Missing expected files after download:`n$($missing -join "`n")"
    exit 1
}

foreach ($f in $expected) {
    $item = Get-Item $f
    if ($item.PSIsContainer) {
        continue
    }
    if ($item.Length -eq 0) {
        Write-Error "$f downloaded but is empty (0 bytes)"
        exit 1
    }
}

Write-Host "Done. Speech models are in $speechDir"
