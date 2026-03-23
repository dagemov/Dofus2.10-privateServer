param(
    [Parameter(Mandatory = $true)]
    [string]$ClientRoot,

    [string]$ServerHost = "127.0.0.1",

    [string]$AuthPorts = "5555",

    [string]$Language = "es",

    [switch]$AlsoPatchRegConfig,

    [switch]$NoBackup
)

function Get-PrimaryConfigPath {
    param([string]$Root)

    $candidates = @(
        (Join-Path $Root "App\Config.xml"),
        (Join-Path $Root "config.xml")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    return $null
}

function Set-EntryValue {
    param(
        [xml]$Document,
        [System.Xml.XmlElement]$RootNode,
        [string]$Key,
        [string]$Value
    )

    $entry = $RootNode.entry | Where-Object { $_.key -eq $Key } | Select-Object -First 1

    if ($null -eq $entry) {
        $entry = $Document.CreateElement("entry")
        $keyAttribute = $Document.CreateAttribute("key")
        $keyAttribute.Value = $Key
        [void]$entry.Attributes.Append($keyAttribute)
        $entry.InnerText = $Value
        [void]$RootNode.AppendChild($entry)
    }
    else {
        $entry.InnerText = $Value
    }
}

function Update-ConfigFile {
    param(
        [string]$Path
    )

    [xml]$xml = Get-Content $Path

    $rootNode = if ($xml.config) {
        $xml.config
    }
    elseif ($xml.LangFile) {
        $xml.LangFile
    }
    else {
        $null
    }

    if ($null -eq $rootNode) {
        throw "El archivo '$Path' no tiene un nodo raiz compatible (<config> o <LangFile>)."
    }

    if (-not $NoBackup) {
        Copy-Item $Path "$Path.bak" -Force
    }

    Set-EntryValue -Document $xml -RootNode $rootNode -Key "connection.host" -Value $ServerHost
    Set-EntryValue -Document $xml -RootNode $rootNode -Key "connection.port" -Value $AuthPorts
    Set-EntryValue -Document $xml -RootNode $rootNode -Key "lang.current" -Value $Language

    $xml.Save($Path)

    $signatureEntry = $rootNode.entry | Where-Object { $_.key -eq "connection.host.signature" } | Select-Object -First 1

    Write-Host "Cliente actualizado:" -ForegroundColor Green
    Write-Host "  Config.xml : $Path"
    Write-Host "  Host       : $ServerHost"
    Write-Host "  AuthPorts  : $AuthPorts"
    Write-Host "  Language   : $Language"

    if ($null -ne $signatureEntry -and -not [string]::IsNullOrWhiteSpace($signatureEntry.InnerText)) {
        Write-Warning "Se detecto 'connection.host.signature'. Un cliente vanilla puede rechazar hosts no oficiales aunque el Config.xml este cambiado."
        Write-Warning "Para conectar con un emulador 2.10 normalmente hace falta un cliente ya parcheado o compatible."
    }
}

$configPath = Get-PrimaryConfigPath -Root $ClientRoot

if ([string]::IsNullOrWhiteSpace($configPath)) {
    throw "No se encontro App\\Config.xml ni config.xml en '$ClientRoot'."
}

Update-ConfigFile -Path $configPath

if ($AlsoPatchRegConfig) {
    $regConfigPath = Join-Path $ClientRoot "reg\config.xml"

    if (Test-Path $regConfigPath) {
        Update-ConfigFile -Path $regConfigPath
    }
}
