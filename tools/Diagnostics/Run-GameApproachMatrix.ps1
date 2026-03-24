$ErrorActionPreference = 'Stop'

$project = 'C:\Users\Hombr\source\repos\Dofus2.10\src\Dofus210.Host\Dofus210.Host.csproj'
$testScript = 'C:\Users\Hombr\source\repos\Dofus2.10\tools\Diagnostics\Test-DofusCharacterSelectionFlow.ps1'
$profiles = @(
    'CompatibilityPushList',
    'CompatibilityNoPushList',
    'MinimalClassic'
)

dotnet build 'C:\Users\Hombr\source\repos\Dofus2.10\Dofus2.10.sln' -nodeReuse:false | Out-Null

$results = @()

foreach ($profile in $profiles) {
    $outLogPath = "C:\Users\Hombr\source\repos\Dofus2.10\runtime\host-selection-$profile.out.log"
    $errLogPath = "C:\Users\Hombr\source\repos\Dofus2.10\runtime\host-selection-$profile.err.log"

    if (Test-Path $outLogPath) {
        Remove-Item $outLogPath -Force
    }

    if (Test-Path $errLogPath) {
        Remove-Item $errLogPath -Force
    }

    $env:Server__GameApproachProfile = $profile

    try {
        $hostProcess = Start-Process dotnet `
            -ArgumentList @('run', '--no-build', '--project', $project) `
            -PassThru `
            -WindowStyle Hidden `
            -RedirectStandardOutput $outLogPath `
            -RedirectStandardError $errLogPath

        Start-Sleep -Seconds 8

        try {
            $testOutput = & $testScript 2>&1
            $results += [pscustomobject]@{
                Profile = $profile
                Success = $true
                Summary = ($testOutput -join "`n")
            }
        }
        catch {
            $results += [pscustomobject]@{
                Profile = $profile
                Success = $false
                Summary = $_.Exception.Message
            }
        }
        finally {
            if ($hostProcess -and -not $hostProcess.HasExited) {
                Stop-Process -Id $hostProcess.Id -Force
                Wait-Process -Id $hostProcess.Id -ErrorAction SilentlyContinue
            }
        }
    }
    finally {
        Remove-Item Env:Server__GameApproachProfile -ErrorAction SilentlyContinue
    }
}

$results | Format-Table -AutoSize
