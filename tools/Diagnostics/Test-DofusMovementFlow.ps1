$ErrorActionPreference = 'Stop'

$selectionScriptPath = 'C:\Users\Hombr\source\repos\Dofus2.10\tools\Diagnostics\Test-DofusCharacterSelectionFlow.ps1'
$selectionScriptLines = Get-Content -Path $selectionScriptPath
$bootstrapIndex = [Array]::IndexOf($selectionScriptLines, '$summary = New-Object System.Collections.Generic.List[string]')

if ($bootstrapIndex -lt 0) {
    throw 'The movement diagnostics could not locate the reusable helper section in Test-DofusCharacterSelectionFlow.ps1.'
}

$helperBootstrap = $selectionScriptLines[0..($bootstrapIndex - 1)] -join [Environment]::NewLine
Invoke-Expression $helperBootstrap

function New-GameMapMovementRequestPayload {
    param(
        [UInt16[]]$KeyMovements,
        [double]$MapId
    )

    $payload = New-Object System.Collections.Generic.List[byte]
    $payload.Add([byte](($KeyMovements.Length -shr 8) -band 0xFF))
    $payload.Add([byte]($KeyMovements.Length -band 0xFF))

    foreach ($keyMovement in $KeyMovements) {
        $payload.Add([byte](($keyMovement -shr 8) -band 0xFF))
        $payload.Add([byte]($keyMovement -band 0xFF))
    }

    Add-Double -Buffer $payload -Value $MapId
    return $payload.ToArray()
}

function Parse-FirstActorPositionFromMapComplementaryInformationsData {
    param([byte[]]$Payload)

    $offset = 0
    [void](Read-VarIntFromBytes -Bytes $Payload -Offset ([ref]$offset))
    [void](Read-DoubleFromBytes -Bytes $Payload -Offset ([ref]$offset))
    [void](Read-UnsignedShortFromBytes -Bytes $Payload -Offset ([ref]$offset))
    $actorsCount = Read-UnsignedShortFromBytes -Bytes $Payload -Offset ([ref]$offset)

    if ($actorsCount -lt 1) {
        throw 'MapComplementaryInformationsData does not contain any actor.'
    }

    [void](Read-UnsignedShortFromBytes -Bytes $Payload -Offset ([ref]$offset))
    [void](Read-DoubleFromBytes -Bytes $Payload -Offset ([ref]$offset))
    [void](Read-UnsignedShortFromBytes -Bytes $Payload -Offset ([ref]$offset))
    $cellId = [int16](Read-UnsignedShortFromBytes -Bytes $Payload -Offset ([ref]$offset))
    $direction = [byte]$Payload[$offset]

    [pscustomobject]@{
        CellId = $cellId
        Direction = $direction
    }
}

$summary = New-Object System.Collections.Generic.List[string]

$authClient = [System.Net.Sockets.TcpClient]::new()
$authClient.ReceiveTimeout = 5000
$authClient.SendTimeout = 5000
$authClient.NoDelay = $true
$authClient.Connect('127.0.0.1', 5555)

try {
    $authStream = $authClient.GetStream()

    $pingPacket = Encode-Packet -MessageId 182 -Payload ([byte[]](1))
    $authStream.Write($pingPacket, 0, $pingPacket.Length)
    [void](Read-Exact -Stream $authStream -Count 2342)

    $identificationPayload = New-IdentificationPayload -Username 'sebcos1' -Password 'polondrolo3'
    $identificationPacket = Encode-Packet -MessageId 4 -Payload $identificationPayload
    $authStream.Write($identificationPacket, 0, $identificationPacket.Length)

    $selectedServerData = $null

    while ($true) {
        $packet = Read-Packet -Stream $authStream

        if ($packet.MessageId -eq 30) {
            $selectionPacket = Encode-Packet -MessageId 40 -Payload (New-ServerSelectionPayload -ServerId 4001)
            $authStream.Write($selectionPacket, 0, $selectionPacket.Length)
            continue
        }

        if ($packet.MessageId -eq 42) {
            $selectedServerData = Parse-SelectedServerData -Payload $packet.Payload
            break
        }
    }

    if ($null -eq $selectedServerData) {
        throw 'SelectedServerData was not received.'
    }

    $gameClient = [System.Net.Sockets.TcpClient]::new()
    $gameClient.ReceiveTimeout = 5000
    $gameClient.SendTimeout = 5000
    $gameClient.NoDelay = $true
    $gameClient.Connect($selectedServerData.Address, $selectedServerData.Port)

    try {
        $gameStream = $gameClient.GetStream()

        [void](Read-Packet -Stream $gameStream)

        $ticketPayload = New-AuthenticationTicketPayload -Language 'es' -Ticket $selectedServerData.Ticket
        $ticketPacket = Encode-Packet -MessageId 110 -Payload $ticketPayload
        $gameStream.Write($ticketPacket, 0, $ticketPacket.Length)
        $approachPackets = @()
        $charactersListPacket = $null
        for ($index = 0; $index -lt 16; $index++) {
            $packet = Try-ReadPacket -Stream $gameStream
            if ($null -eq $packet) {
                break
            }

            if ($packet.MessageId -eq 151) {
                $charactersListPacket = $packet
                break
            }

            $approachPackets += $packet
        }

        if ($null -eq $charactersListPacket) {
            $charactersListRequest = Encode-Packet -MessageId 150 -Payload ([byte[]]::new(0))
            $gameStream.Write($charactersListRequest, 0, $charactersListRequest.Length)
            $charactersListPacket = Read-Packet -Stream $gameStream
        }

        $charactersList = Parse-CharactersList -Payload $charactersListPacket.Payload

        if ($charactersList.Count -eq 0) {
            throw 'No characters are available for selection.'
        }

        $selectedCharacter = $charactersList.Characters[0]
        $selectionPayload = New-CharacterSelectionPayload -CharacterId $selectedCharacter.CharacterId
        $selectionPacket = Encode-Packet -MessageId 152 -Payload $selectionPayload
        $gameStream.Write($selectionPacket, 0, $selectionPacket.Length)

        $bootstrapPackets = @()
        for ($index = 0; $index -lt 14; $index++) {
            $bootstrapPackets += (Read-Packet -Stream $gameStream)
        }

        $currentMapPacket = $bootstrapPackets | Where-Object { $_.MessageId -eq 220 } | Select-Object -First 1
        if ($null -eq $currentMapPacket) {
            throw 'CurrentMap packet was not received after character selection.'
        }

        $offset = 0
        $mapId = [long](Read-DoubleFromBytes -Bytes $currentMapPacket.Payload -Offset ([ref]$offset))
        $mapRequestPayload = New-MapInformationsRequestPayload -MapId $mapId
        $mapRequestPacket = Encode-Packet -MessageId 225 -Payload $mapRequestPayload
        $gameStream.Write($mapRequestPacket, 0, $mapRequestPacket.Length)

        $mapRequestPackets = @()
        for ($index = 0; $index -lt 8; $index++) {
            $packet = Try-ReadPacket -Stream $gameStream
            if ($null -eq $packet) {
                break
            }

            $mapRequestPackets += $packet
        }

        $initialMapPacket = $mapRequestPackets | Where-Object { $_.MessageId -eq 226 } | Select-Object -Last 1
        if ($null -eq $initialMapPacket) {
            throw 'MapComplementaryInformationsData packet was not received after the map request.'
        }

        $targetCellId = 301
        $movementPayload = New-GameMapMovementRequestPayload -KeyMovements ([UInt16[]]@($targetCellId)) -MapId $mapId
        $movementPacket = Encode-Packet -MessageId 3376 -Payload $movementPayload
        $gameStream.Write($movementPacket, 0, $movementPacket.Length)

        $movementRefreshPackets = @()
        for ($index = 0; $index -lt 8; $index++) {
            $packet = Try-ReadPacket -Stream $gameStream
            if ($null -eq $packet) {
                break
            }

            $movementRefreshPackets += $packet
        }

        $movementMapPacket = $movementRefreshPackets | Where-Object { $_.MessageId -eq 226 } | Select-Object -Last 1
        if ($null -eq $movementMapPacket) {
            throw 'MapComplementaryInformationsData packet was not received after the movement request.'
        }

        $position = Parse-FirstActorPositionFromMapComplementaryInformationsData -Payload $movementMapPacket.Payload
        $sqlResult = sqlcmd -S 'DAGEMOV\SQLEXPRESS' -d 'Dofus2.10' -h -1 -W -Q "SET NOCOUNT ON; SELECT TOP 1 CellId, Direction FROM CharacterPositions WHERE CharacterId = $($selectedCharacter.CharacterId);" | Where-Object { $_ -and $_.Trim() }

        if ($position.CellId -ne $targetCellId) {
            throw "The refreshed actor cell was $($position.CellId) instead of $targetCellId."
        }

        $summary.Add("GameApproachIds=$((($approachPackets | ForEach-Object { $_.MessageId }) -join ','))")
        $summary.Add("SelectedCharacterId=$($selectedCharacter.CharacterId)")
        $summary.Add("MovementRefreshIds=$((($movementRefreshPackets | ForEach-Object { $_.MessageId }) -join ','))")
        $summary.Add("RefreshedCellId=$($position.CellId) RefreshedDirection=$($position.Direction)")
        $summary.Add("PersistedPosition=$(($sqlResult -join ' | '))")
    }
    finally {
        if ($gameStream) {
            $gameStream.Dispose()
        }

        $gameClient.Dispose()
    }
}
finally {
    if ($authStream) {
        $authStream.Dispose()
    }

    $authClient.Dispose()
}

$summary
