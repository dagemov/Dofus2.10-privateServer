[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ClientRoot = (Join-Path $PSScriptRoot '..\..\client\Ankaline210\Dofus'),

    [Parameter(Mandatory = $false)]
    [int]$ServerId = 4001,

    [Parameter(Mandatory = $false)]
    [int]$NameTranslationId = 8693,

    [Parameter(Mandatory = $false)]
    [int]$CommentTranslationId = 8693
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-BigEndianInt32 {
    param(
        [byte[]]$Bytes,
        [int]$Offset
    )

    return (([int]$Bytes[$Offset]) -shl 24) -bor
        (([int]$Bytes[$Offset + 1]) -shl 16) -bor
        (([int]$Bytes[$Offset + 2]) -shl 8) -bor
        ([int]$Bytes[$Offset + 3])
}

function Write-BigEndianInt32 {
    param(
        [byte[]]$Bytes,
        [int]$Offset,
        [int]$Value
    )

    $Bytes[$Offset] = [byte](($Value -shr 24) -band 0xFF)
    $Bytes[$Offset + 1] = [byte](($Value -shr 16) -band 0xFF)
    $Bytes[$Offset + 2] = [byte](($Value -shr 8) -band 0xFF)
    $Bytes[$Offset + 3] = [byte]($Value -band 0xFF)
}

function Find-ServerObjectOffset {
    param(
        [byte[]]$Bytes,
        [int]$ExpectedServerId
    )

    $matches = New-Object System.Collections.Generic.List[int]

    for ($index = 0; $index -le $Bytes.Length - 8; $index++) {
        $candidateId = Read-BigEndianInt32 -Bytes $Bytes -Offset $index

        if ($candidateId -ne $ExpectedServerId) {
            continue
        }

        $objectOffset = Read-BigEndianInt32 -Bytes $Bytes -Offset ($index + 4)

        if ($objectOffset -lt 0 -or $objectOffset + 16 -gt $Bytes.Length) {
            continue
        }

        $fieldId = Read-BigEndianInt32 -Bytes $Bytes -Offset ($objectOffset + 4)

        if ($fieldId -eq $ExpectedServerId) {
            $matches.Add($objectOffset)
        }
    }

    $uniqueMatches = @($matches | Select-Object -Unique)

    if ($uniqueMatches.Count -eq 0) {
        throw "No se encontro la entrada D2O para el servidor id $ExpectedServerId."
    }

    if ($uniqueMatches.Count -gt 1) {
        throw "Se encontraron varias entradas D2O para el servidor id ${ExpectedServerId}: $($uniqueMatches -join ', ')."
    }

    return [int]$uniqueMatches[0]
}

$serversFile = Join-Path $ClientRoot 'data\common\Servers.d2o'

if (-not (Test-Path -LiteralPath $serversFile)) {
    throw "No existe el archivo esperado: $serversFile"
}

$backupFile = "$serversFile.bak"

if (-not (Test-Path -LiteralPath $backupFile)) {
    Copy-Item -LiteralPath $serversFile -Destination $backupFile
}

$bytes = [System.IO.File]::ReadAllBytes($serversFile)
$objectOffset = Find-ServerObjectOffset -Bytes $bytes -ExpectedServerId $ServerId

$nameOffset = $objectOffset + 8
$commentOffset = $objectOffset + 12

$previousNameTranslationId = Read-BigEndianInt32 -Bytes $bytes -Offset $nameOffset
$previousCommentTranslationId = Read-BigEndianInt32 -Bytes $bytes -Offset $commentOffset

Write-BigEndianInt32 -Bytes $bytes -Offset $nameOffset -Value $NameTranslationId
Write-BigEndianInt32 -Bytes $bytes -Offset $commentOffset -Value $CommentTranslationId

[System.IO.File]::WriteAllBytes($serversFile, $bytes)

$verifiedNameTranslationId = Read-BigEndianInt32 -Bytes $bytes -Offset $nameOffset
$verifiedCommentTranslationId = Read-BigEndianInt32 -Bytes $bytes -Offset $commentOffset

Write-Host "Servers.d2o parcheado correctamente."
Write-Host "Ruta: $serversFile"
Write-Host "ServerId: $ServerId"
Write-Host "ObjectOffset: 0x$('{0:X}' -f $objectOffset)"
Write-Host "NameTranslationId: $previousNameTranslationId -> $verifiedNameTranslationId"
Write-Host "CommentTranslationId: $previousCommentTranslationId -> $verifiedCommentTranslationId"
