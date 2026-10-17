$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath('E:\Unity\MonteCastello1945')
$sourceFolder = [System.IO.Path]::GetFullPath('E:\Unity\MonteCastello1945\Assets\Art\Characters')
$archiveFolder = [System.IO.Path]::GetFullPath('E:\Unity\MonteCastello1945\ArtSource\Characters_Archive_20261016')
foreach ($checkedPath in @($sourceFolder, $archiveFolder)) {
    if (-not $checkedPath.StartsWith($projectRoot + '\', [System.StringComparison]::OrdinalIgnoreCase)) { throw "Outside project: $checkedPath" }
}
if ($sourceFolder -ne 'E:\Unity\MonteCastello1945\Assets\Art\Characters') { throw 'Unexpected source folder' }
if (Test-Path -LiteralPath $archiveFolder) { throw 'Archive already exists; no move performed.' }
$sourceFiles = @(Get-ChildItem -LiteralPath $sourceFolder -Recurse -File)
$manifest = @($sourceFiles | ForEach-Object { @{ path=$_.FullName.Substring($sourceFolder.Length+1); bytes=$_.Length; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } })
Move-Item -LiteralPath $sourceFolder -Destination $archiveFolder
New-Item -ItemType Directory -Path $sourceFolder | Out-Null
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $archiveFolder 'ARCHIVE_MANIFEST.json') -Encoding utf8
foreach ($entry in $manifest) {
    $savedPath = Join-Path $archiveFolder $entry.path
    if ((Get-FileHash -LiteralPath $savedPath -Algorithm SHA256).Hash -ne $entry.sha256) { throw "Archive verification failed: $savedPath" }
}
if (@(Get-ChildItem -LiteralPath $sourceFolder -Force).Count -ne 0) { throw 'Characters folder is not empty' }
Write-Output ('Assets/Art/Characters is empty; preserved files: ' + $manifest.Count)
Write-Output ('Archive: ' + $archiveFolder)
