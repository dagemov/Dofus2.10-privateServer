$ErrorActionPreference = 'Stop'

$project = 'C:\Users\Hombr\source\repos\Dofus2.10\src\Dofus210.Host\Dofus210.Host.csproj'
$probeScript = 'C:\Users\Hombr\source\repos\Dofus2.10\tools\Diagnostics\Test-DofusAuthPreSelectionFlow.ps1'
$runtimeDirectory = 'C:\Users\Hombr\source\repos\Dofus2.10\runtime\auth-bootstrap-compare'
$profiles = @('CapturedAnkaline', 'MinimalControlled', 'NoCaptured')

if (-not (Test-Path $runtimeDirectory)) {
    New-Item -ItemType Directory -Path $runtimeDirectory | Out-Null
}

dotnet build 'C:\Users\Hombr\source\repos\Dofus2.10\Dofus2.10.sln' -nologo -nodeReuse:false | Out-Host

foreach ($profile in $profiles) {
    $safeProfile = $profile.ToLowerInvariant()
    $outLogPath = Join-Path $runtimeDirectory "$safeProfile.host.out.log"
    $errLogPath = Join-Path $runtimeDirectory "$safeProfile.host.err.log"
    $probeLogPath = Join-Path $runtimeDirectory "$safeProfile.probe.log"

    foreach ($path in @($outLogPath, $errLogPath, $probeLogPath)) {
        if (Test-Path $path) {
            Remove-Item $path -Force
        }
    }

    Write-Host "=== PROFILE $profile ==="

    $hostProcess = Start-Process dotnet `
        -ArgumentList @(
            'run',
            '--no-build',
            '--project',
            $project,
            '--',
            "--Server:AuthBootstrapProfile=$profile"
        ) `
        -PassThru `
        -WindowStyle Hidden `
        -RedirectStandardOutput $outLogPath `
        -RedirectStandardError $errLogPath

    Start-Sleep -Seconds 8

    try {
        & $probeScript | Tee-Object -FilePath $probeLogPath
    }
    finally {
        if ($hostProcess -and -not $hostProcess.HasExited) {
            Stop-Process -Id $hostProcess.Id -Force
            Wait-Process -Id $hostProcess.Id -ErrorAction SilentlyContinue
        }
    }

    Write-Host 'HOST OUT TAIL:'
    if (Test-Path $outLogPath) {
        Get-Content -Path $outLogPath | Select-Object -Last 30
    }

    Write-Host 'HOST ERR TAIL:'
    if (Test-Path $errLogPath) {
        Get-Content -Path $errLogPath | Select-Object -Last 20
    }

    Write-Host ''
}
