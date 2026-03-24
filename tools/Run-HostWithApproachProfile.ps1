$ErrorActionPreference = 'Stop'

param(
    [ValidateSet('CompatibilityPushList', 'CompatibilityNoPushList', 'MinimalClassic')]
    [string]$ApproachProfile = 'CompatibilityPushList'
)

$project = 'C:\Users\Hombr\source\repos\Dofus2.10\src\Dofus210.Host\Dofus210.Host.csproj'
$env:Server__GameApproachProfile = $ApproachProfile

try {
    Write-Host "Starting host with GameApproachProfile=$ApproachProfile"
    dotnet run --project $project
}
finally {
    Remove-Item Env:Server__GameApproachProfile -ErrorAction SilentlyContinue
}
