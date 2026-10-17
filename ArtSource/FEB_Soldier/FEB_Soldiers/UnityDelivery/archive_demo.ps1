$ErrorActionPreference = 'Stop'
$workspacePath = (Resolve-Path -LiteralPath 'E:/Unity/MonteCastello1945').Path
$archivePath = (Resolve-Path -LiteralPath (Join-Path $workspacePath 'ArtSource/FEB_Soldier/FEB_Soldiers/UnityDelivery/Archive_Demo_20261016')).Path
$validationPath = Join-Path $workspacePath 'ArtSource/FEB_Soldier/FEB_Soldiers/UnityDelivery/prefabs_only_validation.json'
if (-not (Get-Content -LiteralPath $validationPath -Raw | ConvertFrom-Json).passed) { throw 'Os prefabs precisam passar na validação antes de arquivar a demonstração.' }
$items = @(
    'Assets/Art/Characters/FEB_Soldiers/Animations',
    'Assets/Art/Characters/FEB_Soldiers/Animations.meta',
    'Assets/Art/Characters/FEB_Soldiers/Materials/FEB_TestGround.mat',
    'Assets/Art/Characters/FEB_Soldiers/Materials/FEB_TestGround.mat.meta',
    'Assets/Scenes/FEB_Character_Test.unity',
    'Assets/Scenes/FEB_Character_Test.unity.meta',
    'Assets/Scripts/FEB',
    'Assets/Scripts/FEB.meta',
    'Assets/Editor/FEBCharacters',
    'Assets/Editor/FEBCharacters.meta'
)
$planned = foreach ($relative in $items) {
    $source = (Resolve-Path -LiteralPath (Join-Path $workspacePath $relative)).Path
    $destination = [IO.Path]::GetFullPath((Join-Path $archivePath $relative))
    if (-not $source.StartsWith($workspacePath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Origem fora do projeto: $source" }
    if (-not $destination.StartsWith($archivePath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Destino fora do arquivo: $destination" }
    if (Test-Path -LiteralPath $destination) { throw "Destino já existe: $destination" }
    [PSCustomObject]@{ Source = $source; Destination = $destination }
}
$planned | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $archivePath 'archived_paths.json') -Encoding utf8
foreach ($item in $planned) {
    New-Item -ItemType Directory -Path (Split-Path -Parent $item.Destination) -Force | Out-Null
    Move-Item -LiteralPath $item.Source -Destination $item.Destination
}
$preserved = Get-Content -LiteralPath (Join-Path $archivePath 'preserved_assets_sha256.json') -Raw | ConvertFrom-Json
$changed = @($preserved | Where-Object { (Get-FileHash -LiteralPath $_.Path -Algorithm SHA256).Hash -ne $_.SHA256 })
if ($changed.Count -ne 0) { throw ('Modelo/material/textura alterado: ' + ($changed.Path -join ', ')) }
[PSCustomObject]@{ ArchivedItems = $planned.Count; UnchangedModelMaterialTextureFiles = $preserved.Count; Archive = $archivePath } | ConvertTo-Json
