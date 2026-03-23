$ErrorActionPreference = 'Stop'

$project = 'C:\Users\Hombr\source\repos\Dofus2.10\src\Dofus210.Host\Dofus210.Host.csproj'
$outLogPath = 'C:\Users\Hombr\source\repos\Dofus2.10\runtime\host-selection-validation.out.log'
$errLogPath = 'C:\Users\Hombr\source\repos\Dofus2.10\runtime\host-selection-validation.err.log'

if (Test-Path $outLogPath) {
    Remove-Item $outLogPath -Force
}

if (Test-Path $errLogPath) {
    Remove-Item $errLogPath -Force
}

$hostProcess = Start-Process dotnet `
    -ArgumentList @('run', '--project', $project) `
    -PassThru `
    -WindowStyle Hidden `
    -RedirectStandardOutput $outLogPath `
    -RedirectStandardError $errLogPath

Start-Sleep -Seconds 8

try {
    & 'C:\Users\Hombr\source\repos\Dofus2.10\tools\Diagnostics\Test-DofusCharacterSelectionFlow.ps1'
}
finally {
    if ($hostProcess -and -not $hostProcess.HasExited) {
        Stop-Process -Id $hostProcess.Id -Force
    }
}

'OUT:'
if (Test-Path $outLogPath) {
    Get-Content -Path $outLogPath | Select-Object -Last 30
}

'ERR:'
if (Test-Path $errLogPath) {
    Get-Content -Path $errLogPath | Select-Object -Last 30
}
