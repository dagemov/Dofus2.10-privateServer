[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ClientStateRoot = 'C:\Users\Hombr\AppData\Roaming\Ankaline210',

    [Parameter(Mandatory = $false)]
    [string]$BackupRoot = (Join-Path $PSScriptRoot '..\..\runtime\client-state-backups')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-PathValue {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot $Path))
}

function Stop-ClientProcesses {
    $targets = Get-Process | Where-Object { $_.ProcessName -in 'Dofus', 'SFD', 'DofusMod' }

    foreach ($process in $targets) {
        Stop-Process -Id $process.Id -Force
        Write-Host "Proceso detenido: $($process.ProcessName) ($($process.Id))"
    }
}

function Backup-And-RemoveFile {
    param(
        [string]$SourcePath,
        [string]$DestinationDirectory
    )

    if (-not (Test-Path -LiteralPath $SourcePath)) {
        return
    }

    if (-not (Test-Path -LiteralPath $DestinationDirectory)) {
        New-Item -ItemType Directory -Path $DestinationDirectory -Force | Out-Null
    }

    $destinationPath = Join-Path $DestinationDirectory ([System.IO.Path]::GetFileName($SourcePath))
    Copy-Item -LiteralPath $SourcePath -Destination $destinationPath -Force
    Remove-Item -LiteralPath $SourcePath -Force
    Write-Host "Cache limpiada: $SourcePath"
}

$resolvedClientStateRoot = Resolve-PathValue -Path $ClientStateRoot
$resolvedBackupRoot = Resolve-PathValue -Path $BackupRoot
$backupStamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupDirectory = Join-Path $resolvedBackupRoot $backupStamp

if (-not (Test-Path -LiteralPath $resolvedClientStateRoot)) {
    throw "No existe la ruta del estado del cliente: $resolvedClientStateRoot"
}

Stop-ClientProcesses

$cacheFiles = @(
    'Jerakine_lang.dat',
    'Jerakine_lang_vesrion.dat',
    'lastLangVersion.dat',
    'Berilia_ui_definition.dat',
    'Berilia_ui_css.dat',
    'Berilia_ui_version.dat',
    'clientData.dat',
    'dofus.dat',
    'LoadingScreen.dat',
    'Module_Ankama_Connection.dat',
    'AlmanaxCache.dat'
)

foreach ($fileName in $cacheFiles) {
    $fullPath = Join-Path $resolvedClientStateRoot $fileName
    Backup-And-RemoveFile -SourcePath $fullPath -DestinationDirectory $backupDirectory
}

Write-Host "Reset del estado del cliente completado."
Write-Host "Backup: $backupDirectory"
