$ErrorActionPreference = 'Stop'
$assetRoot = Join-Path (Split-Path $PSScriptRoot -Parent) 'Assets\GrandLibrary\Realism'
$ids = @('sofa_02','mid_century_lounge_chair','dining_chair_02','round_wooden_table_01','wooden_table_02','wooden_bookshelf_worn','modern_ceiling_lamp_01','desk_lamp_arm_01')
$manifest = @()
function Save-Verified($record, $target) {
    if (-not $record -or -not $record.url) { throw "Missing download metadata for $target" }
    if (Test-Path -LiteralPath $target) {
        if ((Get-FileHash -LiteralPath $target -Algorithm MD5).Hash.ToLowerInvariant() -eq $record.md5) { return }
    }
    Invoke-WebRequest -Uri $record.url -OutFile $target
    if ((Get-FileHash -LiteralPath $target -Algorithm MD5).Hash.ToLowerInvariant() -ne $record.md5) { throw "Checksum mismatch: $target" }
}
foreach ($id in $ids) {
    $data = Invoke-RestMethod -Uri "https://api.polyhaven.com/files/$id" -Headers @{'User-Agent'='CampusCommons/1.0'}
    $dest = Join-Path $assetRoot $id
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    $mesh = $data.fbx.'2k'.fbx
    $diff = $data.Diffuse.'2k'.jpg
    $norm = $data.nor_gl.'2k'.jpg
    Save-Verified $mesh (Join-Path $dest "$id.fbx")
    Save-Verified $diff (Join-Path $dest "${id}_diff.jpg")
    Save-Verified $norm (Join-Path $dest "${id}_normal.jpg")
    $manifest += [pscustomobject]@{id=$id;page="https://polyhaven.com/a/$id";license='CC0';mesh=$mesh.url;diffuse=$diff.url;normal=$norm.url}
    Write-Output "READY $id"
}
$floor = Invoke-RestMethod -Uri 'https://api.polyhaven.com/files/wood_floor' -Headers @{'User-Agent'='CampusCommons/1.0'}
$floorDir=Join-Path $assetRoot 'wood_floor'
New-Item -ItemType Directory -Force -Path $floorDir | Out-Null
Save-Verified $floor.Diffuse.'2k'.jpg (Join-Path $floorDir 'wood_floor_diff.jpg')
Save-Verified $floor.nor_gl.'2k'.jpg (Join-Path $floorDir 'wood_floor_normal.jpg')
$manifest += [pscustomobject]@{id='wood_floor';page='https://polyhaven.com/a/wood_floor';license='CC0';diffuse=$floor.Diffuse.'2k'.jpg.url;normal=$floor.nor_gl.'2k'.jpg.url}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $assetRoot 'sources.json') -Encoding UTF8
Write-Output 'POLYHAVEN_DOWNLOADS_OK'
