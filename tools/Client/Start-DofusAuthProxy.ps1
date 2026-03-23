[CmdletBinding()]
param(
    [string]$ListenHost = '127.0.0.1',
    [int]$ListenPort = 5555,
    [string]$RemoteHost = '37.59.119.244',
    [int]$RemotePort = 1850,
    [string]$LogPath = '..\..\runtime\auth\auth-proxy.log'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-LogPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot $Path))
}

function Convert-ToProxyAscii {
    param([byte[]]$Buffer, [int]$Count)

    $builder = [System.Text.StringBuilder]::new($Count)

    for ($index = 0; $index -lt $Count; $index++) {
        $currentByte = $Buffer[$index]

        if (($currentByte -ge 32 -and $currentByte -le 126) -or $currentByte -in 9, 10, 13) {
            [void]$builder.Append([char]$currentByte)
            continue
        }

        [void]$builder.Append('.')
    }

    return $builder.ToString().Replace("`r", ' ').Replace("`n", ' ').Trim()
}

function Write-ProxyLog {
    param(
        [string]$Path,
        [string]$Direction,
        [byte[]]$Buffer,
        [int]$Count
    )

    $slice = [byte[]]::new($Count)
    [Array]::Copy($Buffer, $slice, $Count)
    $hex = [System.BitConverter]::ToString($slice).Replace('-', '')
    $ascii = Convert-ToProxyAscii -Buffer $slice -Count $Count
    $line = '{0} | {1} | bytes={2} | ascii={3} | hex={4}' -f (Get-Date).ToString('o'), $Direction, $Count, $ascii, $hex

    Add-Content -Path $Path -Value $line
}

function Relay-AvailableBytes {
    param(
        [string]$LogPathValue,
        [string]$Direction,
        [System.Net.Sockets.TcpClient]$SourceClient,
        [System.Net.Sockets.NetworkStream]$SourceStream,
        [System.Net.Sockets.NetworkStream]$DestinationStream,
        [int]$ReadTimeoutMs
    )

    $buffer = [byte[]]::new(8192)
    $transferred = $false
    $originalTimeout = $SourceStream.ReadTimeout
    $SourceStream.ReadTimeout = $ReadTimeoutMs

    try {
        while ($SourceClient.Available -gt 0) {
            $read = $SourceStream.Read($buffer, 0, $buffer.Length)

            if ($read -le 0) {
                break
            }

            Write-ProxyLog -Path $LogPathValue -Direction $Direction -Buffer $buffer -Count $read
            $DestinationStream.Write($buffer, 0, $read)
            $DestinationStream.Flush()
            $transferred = $true
        }
    }
    catch [System.IO.IOException] {
        if (-not $_.Exception.Message.Contains('timed out', [System.StringComparison]::OrdinalIgnoreCase)) {
            Add-Content -Path $LogPathValue -Value ('{0} | {1} | error={2}' -f (Get-Date).ToString('o'), $Direction, $_.Exception.Message)
        }
    }
    finally {
        $SourceStream.ReadTimeout = $originalTimeout
    }

    return $transferred
}

$resolvedLogPath = Resolve-LogPath -Path $LogPath
$logDirectory = Split-Path -Path $resolvedLogPath -Parent

if (-not (Test-Path -Path $logDirectory)) {
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
}

if (Test-Path -Path $resolvedLogPath) {
    Remove-Item -Path $resolvedLogPath -Force
}

$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Parse($ListenHost), $ListenPort)
$listener.Server.NoDelay = $true
$listener.Start()

Add-Content -Path $resolvedLogPath -Value ('{0} | proxy | listening={1}:{2} | remote={3}:{4}' -f (Get-Date).ToString('o'), $ListenHost, $ListenPort, $RemoteHost, $RemotePort)

try {
    $client = $listener.AcceptTcpClient()
    $client.NoDelay = $true
    Add-Content -Path $resolvedLogPath -Value ('{0} | proxy | accepted-client' -f (Get-Date).ToString('o'))
    $server = [System.Net.Sockets.TcpClient]::new()
    $server.NoDelay = $true
    $server.Connect($RemoteHost, $RemotePort)
    Add-Content -Path $resolvedLogPath -Value ('{0} | proxy | connected-remote' -f (Get-Date).ToString('o'))

    try {
        $clientStream = $client.GetStream()
        $serverStream = $server.GetStream()
        $clientStream.ReadTimeout = 15000

        $firstBuffer = [byte[]]::new(8192)
        $firstRead = $clientStream.Read($firstBuffer, 0, $firstBuffer.Length)

        if ($firstRead -gt 0) {
            Write-ProxyLog -Path $resolvedLogPath -Direction 'C2S' -Buffer $firstBuffer -Count $firstRead
            $serverStream.Write($firstBuffer, 0, $firstRead)
            $serverStream.Flush()
        }

        $idleTicks = 0

        while ($idleTicks -lt 40) {
            $didRelay = $false

            $didRelay = (Relay-AvailableBytes -LogPathValue $resolvedLogPath -Direction 'S2C' -SourceClient $server -SourceStream $serverStream -DestinationStream $clientStream -ReadTimeoutMs 250) -or $didRelay
            $didRelay = (Relay-AvailableBytes -LogPathValue $resolvedLogPath -Direction 'C2S' -SourceClient $client -SourceStream $clientStream -DestinationStream $serverStream -ReadTimeoutMs 250) -or $didRelay

            if ($didRelay) {
                $idleTicks = 0
                continue
            }

            Start-Sleep -Milliseconds 250
            $idleTicks++
        }
    }
    finally {
        $client.Dispose()
        $server.Dispose()
    }
}
catch {
    Add-Content -Path $resolvedLogPath -Value ('{0} | proxy | error={1}' -f (Get-Date).ToString('o'), $_.Exception.Message)
    throw
}
finally {
    $listener.Stop()
    Add-Content -Path $resolvedLogPath -Value ('{0} | proxy | stopped' -f (Get-Date).ToString('o'))
}
