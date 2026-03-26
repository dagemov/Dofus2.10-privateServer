$ErrorActionPreference = 'Stop'

param(
    [ValidateSet('CompatibilityPushList', 'CompatibilityNoPushList', 'MinimalClassic', 'GinyPushList', 'GinyNoPushList', 'GinyAckPushList', 'GinyAckNoPushList')]
    [string]$ApproachProfile = 'MinimalClassic',
    [bool]$SendGameProtocolRequired = $false
)

$project = 'C:\Users\Hombr\source\repos\Dofus2.10\src\Dofus210.Host\Dofus210.Host.csproj'
$env:Server__GameApproachProfile = $ApproachProfile
$env:Server__GameSendProtocolRequiredOnConnect = if ($SendGameProtocolRequired) { 'true' } else { 'false' }

try {
    Write-Host "Starting host with GameApproachProfile=$ApproachProfile GameSendProtocolRequiredOnConnect=$SendGameProtocolRequired"
    dotnet run --project $project
}
finally {
    Remove-Item Env:Server__GameApproachProfile -ErrorAction SilentlyContinue
    Remove-Item Env:Server__GameSendProtocolRequiredOnConnect -ErrorAction SilentlyContinue
}
