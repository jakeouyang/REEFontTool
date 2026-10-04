$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$releaseDirectory = Join-Path $projectRoot 'Release'
if (Test-Path -LiteralPath $releaseDirectory) {
    Remove-Item -LiteralPath $releaseDirectory -Recurse -Force
}
dotnet publish (Join-Path $projectRoot 'src/REE.Font.Tool.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $releaseDirectory
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
$destination = Join-Path $releaseDirectory 'Projects'
New-Item -ItemType Directory -Force $destination | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'Projects/games.json') -Destination $destination
$catalog = Get-Content -LiteralPath (Join-Path $projectRoot 'Projects/games.json') -Raw | ConvertFrom-Json
foreach ($game in $catalog) {
    Copy-Item -LiteralPath (Join-Path $projectRoot ('Projects/' + $game.list)) -Destination $destination
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $projectRoot 'Release')
Copy-Item -LiteralPath (Join-Path $projectRoot 'ADDING-GAMES.zh-CN.md') -Destination (Join-Path $projectRoot 'Release')
