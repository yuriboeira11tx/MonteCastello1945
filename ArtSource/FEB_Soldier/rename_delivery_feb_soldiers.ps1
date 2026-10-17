$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath('E:\Unity\MonteCastello1945')
$assetSource = [System.IO.Path]::GetFullPath('E:\Unity\MonteCastello1945\Assets\Art\Characters\FEB_Rangers')
$assetTarget = [System.IO.Path]::GetFullPath('E:\Unity\MonteCastello1945\Assets\Art\Characters\FEB_Soldiers')
$artSource = [System.IO.Path]::GetFullPath('E:\Unity\MonteCastello1945\ArtSource\FEB_Soldier\RangersFEB')
$artTarget = [System.IO.Path]::GetFullPath('E:\Unity\MonteCastello1945\ArtSource\FEB_Soldier\FEB_Soldiers')
foreach ($targetPath in @($assetSource, $assetTarget, $artSource, $artTarget)) {
    if (-not $targetPath.StartsWith($projectRoot + '\', [System.StringComparison]::OrdinalIgnoreCase)) { throw "Path outside intended project: $targetPath" }
}
if ((Test-Path -LiteralPath $assetTarget) -or (Test-Path -LiteralPath $artTarget)) { throw 'Target already exists; no folder was moved.' }
Move-Item -LiteralPath $assetSource -Destination $assetTarget
Move-Item -LiteralPath ($assetSource + '.meta') -Destination ($assetTarget + '.meta')
Move-Item -LiteralPath $artSource -Destination $artTarget

$modelOld = Join-Path $assetTarget 'Models\FEB_Rangers_6_Soldados.fbx'
Rename-Item -LiteralPath $modelOld -NewName 'FEB_Soldiers_6_Soldados.fbx'
Rename-Item -LiteralPath ($modelOld + '.meta') -NewName 'FEB_Soldiers_6_Soldados.fbx.meta'
$textureOld = Join-Path $assetTarget 'Textures\american_rangers_0_4X.png'
Rename-Item -LiteralPath $textureOld -NewName 'FEB_Soldiers_Uniforme_4K.png'
Rename-Item -LiteralPath ($textureOld + '.meta') -NewName 'FEB_Soldiers_Uniforme_4K.png.meta'
Get-ChildItem -LiteralPath $artTarget -File | Where-Object { $_.Name.StartsWith('FEB_Rangers') } | ForEach-Object {
    $newName = $_.Name.Replace('FEB_Rangers_Adaptados', 'FEB_Soldiers').Replace('FEB_Rangers', 'FEB_Soldiers')
    Rename-Item -LiteralPath $_.FullName -NewName $newName
}
Rename-Item -LiteralPath (Join-Path $artTarget 'package_rangers.py') -NewName 'package_feb_soldiers.py'

$textFiles = @(Get-ChildItem -LiteralPath $artTarget -File | Where-Object { $_.Extension -in '.py', '.json', '.txt' -and $_.Name -ne 'rename_feb_soldiers_baseline.json' })
$textFiles += @(Get-ChildItem -LiteralPath $assetTarget -File -Filter '*.txt')
foreach ($item in $textFiles) {
    $oldText = [System.IO.File]::ReadAllText($item.FullName)
    $newText = $oldText.Replace('FEB_Rangers_Adaptados', 'FEB_Soldiers').Replace('RangersFEB', 'FEB_Soldiers').Replace('FEB_Rangers_american_rangers_0', 'FEB_Soldiers_Uniforme').Replace('american_rangers_0_4X.png', 'FEB_Soldiers_Uniforme_4K.png').Replace('FEB_Rangers', 'FEB_Soldiers').Replace('FEB_Adaptados', 'FEB_Soldiers').Replace('package_rangers.py', 'package_feb_soldiers.py')
    if ($newText -ne $oldText) { [System.IO.File]::WriteAllText($item.FullName, $newText, [System.Text.UTF8Encoding]::new($false)) }
}
Write-Output ('Unity: ' + $assetTarget)
Write-Output ('Blender: ' + $artTarget)
