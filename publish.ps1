# Publishes ChastiMaid into a clean Root/{ChastiMaid.exe, Data, Content, Bin} layout.
# Data/ is not created here: DatabaseService creates it under AppPaths.Root on first run.
$ErrorActionPreference = "Stop"

$distRoot = Join-Path $PSScriptRoot "dist\Root"
$appPublish = Join-Path $PSScriptRoot "dist\_app-publish"
$launcherPublish = Join-Path $PSScriptRoot "dist\_launcher-publish"

Remove-Item $distRoot, $appPublish, $launcherPublish -Recurse -Force -ErrorAction SilentlyContinue

dotnet publish (Join-Path $PSScriptRoot "ChastiMaid.csproj") -c Release -o $appPublish
dotnet publish (Join-Path $PSScriptRoot "ChastiMaid.Launcher\ChastiMaid.Launcher.csproj") -c Release -o $launcherPublish `
    -r win-x64 --self-contained true -p:PublishSingleFile=true

New-Item -ItemType Directory -Force -Path (Join-Path $distRoot "Bin") | Out-Null

Move-Item (Join-Path $appPublish "Content") (Join-Path $distRoot "Content")
Move-Item (Join-Path $appPublish "*") (Join-Path $distRoot "Bin")
Copy-Item (Join-Path $launcherPublish "ChastiMaid.exe") (Join-Path $distRoot "ChastiMaid.exe")

Write-Host "Published to $distRoot"
