[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ClientRoot = (Join-Path $PSScriptRoot '..\..\client\Ankaline210\Dofus'),

    [Parameter(Mandatory = $false)]
    [int]$ServerId = 1,

    [Parameter(Mandatory = $false)]
    [Nullable[int]]$NameTranslationId = $null,

    [Parameter(Mandatory = $false)]
    [Nullable[int]]$CommentTranslationId = $null,

    [Parameter(Mandatory = $false)]
    [string]$Language = $null,

    [Parameter(Mandatory = $false)]
    [Nullable[int]]$PopulationId = $null,

    [Parameter(Mandatory = $false)]
    [Nullable[int]]$GameTypeId = $null,

    [Parameter(Mandatory = $false)]
    [Nullable[int]]$CommunityId = $null,

    [Parameter(Mandatory = $false)]
    [Nullable[int]]$RestrictedToLanguagesCount = $null,

    [Parameter(Mandatory = $false)]
    [Nullable[int]]$MonoAccount = $null
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

function Read-UtfMetadata {
    param(
        [byte[]]$Bytes,
        [int]$Offset
    )

    if ($Offset + 1 -ge $Bytes.Length) {
        throw "No se pudo leer la longitud UTF en el offset $Offset."
    }

    $length = (([int]$Bytes[$Offset]) -shl 8) -bor ([int]$Bytes[$Offset + 1])

    if ($length -lt 0 -or $Offset + 2 + $length -gt $Bytes.Length) {
        throw "La cadena UTF en el offset $Offset esta fuera del rango del archivo."
    }

    $text = [System.Text.Encoding]::ASCII.GetString($Bytes, $Offset + 2, $length)

    return [pscustomobject]@{
        Offset = $Offset
        Length = $length
        Text = $text
        NextOffset = $Offset + 2 + $length
    }
}

function Write-UtfText {
    param(
        [byte[]]$Bytes,
        [int]$Offset,
        [string]$Value,
        [int]$ExpectedLength
    )

    if ($Value.Length -ne $ExpectedLength) {
        throw "No se puede escribir '$Value' porque la longitud esperada es $ExpectedLength y no se reescribe el layout del D2O."
    }

    $encoded = [System.Text.Encoding]::ASCII.GetBytes($Value)

    for ($index = 0; $index -lt $encoded.Length; $index++) {
        $Bytes[$Offset + 2 + $index] = $encoded[$index]
    }
}

function Resolve-MonoAccountOffset {
    param(
        [byte[]]$Bytes,
        [pscustomobject]$Layout,
        [int]$RestrictedToLanguagesCount
    )

    $cursor = $Layout.RestrictedCountOffset + 4

    for ($index = 0; $index -lt $RestrictedToLanguagesCount; $index++) {
        $restrictedLanguage = Read-UtfMetadata -Bytes $Bytes -Offset $cursor
        $cursor = $restrictedLanguage.NextOffset
    }

    return $cursor
}

function Get-ServerLayout {
    param(
        [byte[]]$Bytes,
        [int]$ObjectOffset
    )

    $id = Read-BigEndianInt32 -Bytes $Bytes -Offset ($ObjectOffset + 4)
    $nameTranslationId = Read-BigEndianInt32 -Bytes $Bytes -Offset ($ObjectOffset + 8)
    $commentTranslationId = Read-BigEndianInt32 -Bytes $Bytes -Offset ($ObjectOffset + 12)
    $language = Read-UtfMetadata -Bytes $Bytes -Offset ($ObjectOffset + 24)
    $populationOffset = $language.NextOffset
    $gameTypeOffset = $populationOffset + 4
    $communityOffset = $gameTypeOffset + 4
    $restrictedCountOffset = $communityOffset + 4
    $restrictedToLanguagesCount = Read-BigEndianInt32 -Bytes $Bytes -Offset $restrictedCountOffset
    $cursor = $restrictedCountOffset + 4
    $restrictedLanguages = New-Object System.Collections.Generic.List[string]

    if ($restrictedToLanguagesCount -lt 0 -or $restrictedToLanguagesCount -gt 8) {
        throw "El layout del servidor $id tiene un conteo invalido de idiomas restringidos: $restrictedToLanguagesCount"
    }

    for ($index = 0; $index -lt $restrictedToLanguagesCount; $index++) {
        $restrictedLanguage = Read-UtfMetadata -Bytes $Bytes -Offset $cursor
        $restrictedLanguages.Add($restrictedLanguage.Text)
        $cursor = $restrictedLanguage.NextOffset
    }

    return [pscustomobject]@{
        ObjectOffset = $ObjectOffset
        Id = $id
        NameOffset = $ObjectOffset + 8
        NameTranslationId = $nameTranslationId
        CommentOffset = $ObjectOffset + 12
        CommentTranslationId = $commentTranslationId
        LanguageOffset = $language.Offset
        LanguageLength = $language.Length
        Language = $language.Text
        PopulationOffset = $populationOffset
        PopulationId = Read-BigEndianInt32 -Bytes $Bytes -Offset $populationOffset
        GameTypeOffset = $gameTypeOffset
        GameTypeId = Read-BigEndianInt32 -Bytes $Bytes -Offset $gameTypeOffset
        CommunityOffset = $communityOffset
        CommunityId = Read-BigEndianInt32 -Bytes $Bytes -Offset $communityOffset
        RestrictedCountOffset = $restrictedCountOffset
        RestrictedToLanguagesCount = $restrictedToLanguagesCount
        RestrictedLanguages = [string[]]$restrictedLanguages
        MonoAccountOffset = $cursor
        MonoAccount = Read-BigEndianInt32 -Bytes $Bytes -Offset $cursor
    }
}

function Test-ServerLayoutCandidate {
    param(
        [pscustomobject]$Layout,
        [int]$ExpectedServerId
    )

    if ($Layout.Id -ne $ExpectedServerId) {
        return $false
    }

    if ($Layout.NameTranslationId -lt -1 -or $Layout.CommentTranslationId -lt -1) {
        return $false
    }

    if ($Layout.LanguageLength -lt 0 -or $Layout.LanguageLength -gt 5) {
        return $false
    }

    if ($Layout.Language -and $Layout.Language -notmatch '^[a-z]+$') {
        return $false
    }

    return $true
}

function Find-ServerLayout {
    param(
        [byte[]]$Bytes,
        [int]$ExpectedServerId
    )

    $candidates = New-Object System.Collections.Generic.List[object]

    for ($index = 0; $index -le $Bytes.Length - 8; $index++) {
        $candidateId = Read-BigEndianInt32 -Bytes $Bytes -Offset $index

        if ($candidateId -ne $ExpectedServerId) {
            continue
        }

        $objectOffset = Read-BigEndianInt32 -Bytes $Bytes -Offset ($index + 4)

        if ($objectOffset -lt 0 -or $objectOffset + 24 -ge $Bytes.Length) {
            continue
        }

        try {
            $layout = Get-ServerLayout -Bytes $Bytes -ObjectOffset $objectOffset

            if (Test-ServerLayoutCandidate -Layout $layout -ExpectedServerId $ExpectedServerId) {
                $candidates.Add($layout)
            }
        }
        catch {
            continue
        }
    }

    $uniqueCandidates = @($candidates | Sort-Object ObjectOffset -Unique)

    if ($uniqueCandidates.Count -eq 0) {
        throw "No se encontro la entrada D2O para el servidor id $ExpectedServerId."
    }

    if ($uniqueCandidates.Count -gt 1) {
        throw "Se encontraron varias entradas D2O validas para el servidor id ${ExpectedServerId}: $($uniqueCandidates.ObjectOffset -join ', ')."
    }

    return $uniqueCandidates[0]
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
$layout = Find-ServerLayout -Bytes $bytes -ExpectedServerId $ServerId
$effectiveRestrictedToLanguagesCount = $layout.RestrictedToLanguagesCount

if ($PSBoundParameters.ContainsKey('NameTranslationId') -and $null -ne $NameTranslationId) {
    Write-BigEndianInt32 -Bytes $bytes -Offset $layout.NameOffset -Value ([int]$NameTranslationId)
}

if ($PSBoundParameters.ContainsKey('CommentTranslationId') -and $null -ne $CommentTranslationId) {
    Write-BigEndianInt32 -Bytes $bytes -Offset $layout.CommentOffset -Value ([int]$CommentTranslationId)
}

if ($PSBoundParameters.ContainsKey('Language') -and $null -ne $Language) {
    Write-UtfText -Bytes $bytes -Offset $layout.LanguageOffset -Value $Language -ExpectedLength $layout.LanguageLength
}

if ($PSBoundParameters.ContainsKey('PopulationId') -and $null -ne $PopulationId) {
    Write-BigEndianInt32 -Bytes $bytes -Offset $layout.PopulationOffset -Value ([int]$PopulationId)
}

if ($PSBoundParameters.ContainsKey('GameTypeId') -and $null -ne $GameTypeId) {
    Write-BigEndianInt32 -Bytes $bytes -Offset $layout.GameTypeOffset -Value ([int]$GameTypeId)
}

if ($PSBoundParameters.ContainsKey('CommunityId') -and $null -ne $CommunityId) {
    Write-BigEndianInt32 -Bytes $bytes -Offset $layout.CommunityOffset -Value ([int]$CommunityId)
}

if ($PSBoundParameters.ContainsKey('RestrictedToLanguagesCount') -and $null -ne $RestrictedToLanguagesCount) {
    if ([int]$RestrictedToLanguagesCount -lt 0) {
        throw "RestrictedToLanguagesCount no puede ser negativo."
    }

    if ([int]$RestrictedToLanguagesCount -gt $layout.RestrictedToLanguagesCount) {
        throw "Aumentar el conteo de idiomas restringidos reescribe el layout del D2O y no esta soportado por este script. Valor actual: $($layout.RestrictedToLanguagesCount)."
    }

    Write-BigEndianInt32 -Bytes $bytes -Offset $layout.RestrictedCountOffset -Value ([int]$RestrictedToLanguagesCount)
    $effectiveRestrictedToLanguagesCount = [int]$RestrictedToLanguagesCount
}

$monoAccountOffset = Resolve-MonoAccountOffset `
    -Bytes $bytes `
    -Layout $layout `
    -RestrictedToLanguagesCount $effectiveRestrictedToLanguagesCount

$monoAccountValue = $layout.MonoAccount

if ($PSBoundParameters.ContainsKey('MonoAccount') -and $null -ne $MonoAccount) {
    $monoAccountValue = [int]$MonoAccount
}

if ($monoAccountOffset + 3 -ge $bytes.Length) {
    throw "MonoAccountOffset calculado fuera del rango del archivo."
}

Write-BigEndianInt32 -Bytes $bytes -Offset $monoAccountOffset -Value $monoAccountValue

[System.IO.File]::WriteAllBytes($serversFile, $bytes)

$verifiedLayout = Get-ServerLayout -Bytes $bytes -ObjectOffset $layout.ObjectOffset

Write-Host "Servers.d2o parcheado correctamente."
Write-Host "Ruta: $serversFile"
Write-Host "ServerId: $ServerId"
Write-Host "ObjectOffset: 0x$('{0:X}' -f $layout.ObjectOffset)"
Write-Host "NameTranslationId: $($layout.NameTranslationId) -> $($verifiedLayout.NameTranslationId)"
Write-Host "CommentTranslationId: $($layout.CommentTranslationId) -> $($verifiedLayout.CommentTranslationId)"
Write-Host "Language: $($layout.Language) -> $($verifiedLayout.Language)"
Write-Host "PopulationId: $($layout.PopulationId) -> $($verifiedLayout.PopulationId)"
Write-Host "GameTypeId: $($layout.GameTypeId) -> $($verifiedLayout.GameTypeId)"
Write-Host "CommunityId: $($layout.CommunityId) -> $($verifiedLayout.CommunityId)"
Write-Host "RestrictedToLanguagesCount: $($layout.RestrictedToLanguagesCount) -> $($verifiedLayout.RestrictedToLanguagesCount)"
Write-Host "MonoAccount: $($layout.MonoAccount) -> $($verifiedLayout.MonoAccount)"
