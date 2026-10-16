# Refreshes the translation files after source strings change: regenerates locale/ChastiMaid.pot, syncs every
# locale/*.po (new strings added, removed ones dropped and listed), then builds so the .mo files are recompiled.
$ErrorActionPreference = "Stop"

dotnet run --project (Join-Path $PSScriptRoot "tools\ChastiMaid.L10n") -- extract $PSScriptRoot
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build (Join-Path $PSScriptRoot "ChastiMaid.csproj") --nologo -v q
exit $LASTEXITCODE
