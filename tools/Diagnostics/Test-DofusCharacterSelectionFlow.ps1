$ErrorActionPreference = 'Stop'

function Encode-Packet {
    param(
        [UInt16]$MessageId,
        [byte[]]$Payload
    )

    if ($null -eq $Payload) {
        $Payload = [byte[]]::new(0)
    }

    $payloadLength = $Payload.Length

    if ($payloadLength -eq 0) {
        $lengthBytes = 0
    }
    elseif ($payloadLength -le 255) {
        $lengthBytes = 1
    }
    elseif ($payloadLength -le 65535) {
        $lengthBytes = 2
    }
    else {
        $lengthBytes = 3
    }

    $headerValue = ($MessageId -shl 2) -bor $lengthBytes
    $buffer = New-Object System.Collections.Generic.List[byte]
    $buffer.Add([byte](($headerValue -shr 8) -band 0xFF))
    $buffer.Add([byte]($headerValue -band 0xFF))

    switch ($lengthBytes) {
        1 {
            $buffer.Add([byte]$payloadLength)
        }
        2 {
            $buffer.Add([byte](($payloadLength -shr 8) -band 0xFF))
            $buffer.Add([byte]($payloadLength -band 0xFF))
        }
        3 {
            $buffer.Add([byte](($payloadLength -shr 16) -band 0xFF))
            $buffer.Add([byte](($payloadLength -shr 8) -band 0xFF))
            $buffer.Add([byte]($payloadLength -band 0xFF))
        }
    }

    foreach ($currentByte in $Payload) {
        $buffer.Add($currentByte)
    }

    return $buffer.ToArray()
}

function Read-Exact {
    param(
        [System.Net.Sockets.NetworkStream]$Stream,
        [int]$Count
    )

    $buffer = New-Object byte[] $Count
    $offset = 0

    while ($offset -lt $Count) {
        $bytesRead = $Stream.Read($buffer, $offset, $Count - $offset)

        if ($bytesRead -le 0) {
            throw 'Socket closed before packet was fully received.'
        }

        $offset += $bytesRead
    }

    return $buffer
}

function Read-Packet {
    param([System.Net.Sockets.NetworkStream]$Stream)

    $header = Read-Exact -Stream $Stream -Count 2
    $headerValue = ((([int]$header[0]) -shl 8) -bor ([int]$header[1]))
    $lengthBytes = $headerValue -band 0x03
    $messageId = $headerValue -shr 2
    $payloadLength = 0

    if ($lengthBytes -gt 0) {
        $lengthBuffer = Read-Exact -Stream $Stream -Count $lengthBytes

        foreach ($currentByte in $lengthBuffer) {
            $payloadLength = (($payloadLength -shl 8) -bor ([int]$currentByte))
        }
    }

    if ($payloadLength -gt 0) {
        $payload = Read-Exact -Stream $Stream -Count $payloadLength
    }
    else {
        $payload = [byte[]]::new(0)
    }

    [pscustomobject]@{
        MessageId = $messageId
        PayloadLength = $payloadLength
        Payload = $payload
    }
}

function Add-Utf {
    param(
        [System.Collections.Generic.List[byte]]$Buffer,
        [string]$Value
    )

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
    $Buffer.Add([byte](($bytes.Length -shr 8) -band 0xFF))
    $Buffer.Add([byte]($bytes.Length -band 0xFF))

    foreach ($currentByte in $bytes) {
        $Buffer.Add($currentByte)
    }
}

function Add-Int {
    param(
        [System.Collections.Generic.List[byte]]$Buffer,
        [int]$Value
    )

    $Buffer.Add([byte](($Value -shr 24) -band 0xFF))
    $Buffer.Add([byte](($Value -shr 16) -band 0xFF))
    $Buffer.Add([byte](($Value -shr 8) -band 0xFF))
    $Buffer.Add([byte]($Value -band 0xFF))
}

function Add-VarLong {
    param(
        [System.Collections.Generic.List[byte]]$Buffer,
        [Int64]$Value
    )

    if ($Value -lt 0) {
        throw 'Negative values are not supported in this test helper.'
    }

    $currentValue = [UInt64]$Value

    while ($currentValue -ge 0x80) {
        $Buffer.Add([byte](($currentValue -band 0x7F) -bor 0x80))
        $currentValue = $currentValue -shr 7
    }

    $Buffer.Add([byte]$currentValue)
}

function Add-Double {
    param(
        [System.Collections.Generic.List[byte]]$Buffer,
        [double]$Value
    )

    $bytes = [BitConverter]::GetBytes($Value)
    [Array]::Reverse($bytes)

    foreach ($currentByte in $bytes) {
        $Buffer.Add($currentByte)
    }
}

function Read-UtfFromBytes {
    param(
        [byte[]]$Bytes,
        [ref]$Offset
    )

    $length = ((([int]$Bytes[$Offset.Value]) -shl 8) -bor ([int]$Bytes[$Offset.Value + 1]))
    $Offset.Value += 2

    if ($length -eq 0) {
        return ''
    }

    $value = [System.Text.Encoding]::UTF8.GetString($Bytes, $Offset.Value, $length)
    $Offset.Value += $length
    return $value
}

function Read-UnsignedShortFromBytes {
    param(
        [byte[]]$Bytes,
        [ref]$Offset
    )

    $value = ((([int]$Bytes[$Offset.Value]) -shl 8) -bor ([int]$Bytes[$Offset.Value + 1]))
    $Offset.Value += 2
    return $value
}

function Read-IntFromBytes {
    param(
        [byte[]]$Bytes,
        [ref]$Offset
    )

    $value =
        ((([int]$Bytes[$Offset.Value]) -shl 24) -bor
        (([int]$Bytes[$Offset.Value + 1]) -shl 16) -bor
        (([int]$Bytes[$Offset.Value + 2]) -shl 8) -bor
        ([int]$Bytes[$Offset.Value + 3]))

    $Offset.Value += 4
    return $value
}

function Read-DoubleFromBytes {
    param(
        [byte[]]$Bytes,
        [ref]$Offset
    )

    $buffer = New-Object byte[] 8
    [Array]::Copy($Bytes, $Offset.Value, $buffer, 0, 8)
    [Array]::Reverse($buffer)
    $Offset.Value += 8
    return [BitConverter]::ToDouble($buffer, 0)
}

function Read-VarIntFromBytes {
    param(
        [byte[]]$Bytes,
        [ref]$Offset
    )

    $result = 0
    $shift = 0

    while ($true) {
        $currentByte = [int]$Bytes[$Offset.Value]
        $Offset.Value += 1
        $result = $result -bor (($currentByte -band 0x7F) -shl $shift)

        if (($currentByte -band 0x80) -eq 0) {
            return $result
        }

        $shift += 7
    }
}

function Read-VarLongFromBytes {
    param(
        [byte[]]$Bytes,
        [ref]$Offset
    )

    [Int64]$result = 0
    $shift = 0

    while ($true) {
        $currentByte = [int]$Bytes[$Offset.Value]
        $Offset.Value += 1
        $result = $result -bor (([Int64]($currentByte -band 0x7F)) -shl $shift)

        if (($currentByte -band 0x80) -eq 0) {
            return $result
        }

        $shift += 7
    }
}

function Skip-EntityLook {
    param(
        [byte[]]$Bytes,
        [ref]$Offset
    )

    [void](Read-VarIntFromBytes -Bytes $Bytes -Offset $Offset)

    $skinsCount = Read-UnsignedShortFromBytes -Bytes $Bytes -Offset $Offset
    for ($index = 0; $index -lt $skinsCount; $index++) {
        [void](Read-VarIntFromBytes -Bytes $Bytes -Offset $Offset)
    }

    $colorsCount = Read-UnsignedShortFromBytes -Bytes $Bytes -Offset $Offset
    for ($index = 0; $index -lt $colorsCount; $index++) {
        [void](Read-IntFromBytes -Bytes $Bytes -Offset $Offset)
    }

    $scalesCount = Read-UnsignedShortFromBytes -Bytes $Bytes -Offset $Offset
    for ($index = 0; $index -lt $scalesCount; $index++) {
        [void](Read-VarIntFromBytes -Bytes $Bytes -Offset $Offset)
    }

    $subEntitiesCount = Read-UnsignedShortFromBytes -Bytes $Bytes -Offset $Offset
    if ($subEntitiesCount -gt 0) {
        throw 'This smoke test only supports characters without sub-entities.'
    }
}

function Parse-CharactersList {
    param([byte[]]$Payload)

    $offset = 0
    $count = Read-UnsignedShortFromBytes -Bytes $Payload -Offset ([ref]$offset)
    $characters = @()

    for ($index = 0; $index -lt $count; $index++) {
        $typeId = Read-UnsignedShortFromBytes -Bytes $Payload -Offset ([ref]$offset)
        $characterId = Read-VarLongFromBytes -Bytes $Payload -Offset ([ref]$offset)
        $name = Read-UtfFromBytes -Bytes $Payload -Offset ([ref]$offset)
        $level = Read-VarIntFromBytes -Bytes $Payload -Offset ([ref]$offset)
        Skip-EntityLook -Bytes $Payload -Offset ([ref]$offset)
        $breedId = [int]$Payload[$offset]
        $offset += 1
        $sex = [bool]$Payload[$offset]
        $offset += 1

        $characters += [pscustomobject]@{
            TypeId = $typeId
            CharacterId = $characterId
            Name = $name
            Level = $level
            BreedId = $breedId
            Sex = $sex
        }
    }

    $hasStartupActions = [bool]$Payload[$offset]

    [pscustomobject]@{
        Count = $count
        HasStartupActions = $hasStartupActions
        Characters = $characters
    }
}

function New-IdentificationPayload {
    param(
        [string]$Username,
        [string]$Password,
        [string]$Language = 'es'
    )

    $payload = New-Object System.Collections.Generic.List[byte]
    $payload.Add(1)
    $payload.Add(2)
    $payload.Add(10)
    $payload.Add(0)
    $payload.Add(0)
    $payload.Add(1)
    $payload.Add(0)
    $payload.Add(128)
    $payload.Add(0)
    $payload.Add(0)
    $payload.Add(1)
    $payload.Add(1)
    Add-Utf -Buffer $payload -Value $Language

    $credentials = New-Object System.Collections.Generic.List[byte]
    Add-Utf -Buffer $credentials -Value $Username
    Add-Utf -Buffer $credentials -Value $Password

    $payload.Add([byte](($credentials.Count -shr 8) -band 0xFF))
    $payload.Add([byte]($credentials.Count -band 0xFF))

    foreach ($currentByte in $credentials) {
        $payload.Add($currentByte)
    }

    $payload.Add(0)
    $payload.Add(0)

    return $payload.ToArray()
}

function New-AuthenticationTicketPayload {
    param(
        [string]$Language,
        [string]$Ticket
    )

    $payload = New-Object System.Collections.Generic.List[byte]
    Add-Utf -Buffer $payload -Value $Language
    Add-Utf -Buffer $payload -Value $Ticket
    return $payload.ToArray()
}

function New-CharacterCreationPayload {
    param(
        [string]$Name,
        [byte]$BreedId,
        [bool]$Sex,
        [Int16]$CosmeticId = 0
    )

    $payload = New-Object System.Collections.Generic.List[byte]
    Add-Utf -Buffer $payload -Value $Name
    $payload.Add($BreedId)
    $payload.Add([byte]([int]$Sex))

    foreach ($color in @(0, 0, 0, 0, 0)) {
        Add-Int -Buffer $payload -Value $color
    }

    $payload.Add([byte]$CosmeticId)
    return $payload.ToArray()
}

function New-CharacterSelectionPayload {
    param([Int64]$CharacterId)

    $payload = New-Object System.Collections.Generic.List[byte]
    Add-VarLong -Buffer $payload -Value $CharacterId
    return $payload.ToArray()
}

function New-ServerSelectionPayload {
    param([int]$ServerId)

    $payload = New-Object System.Collections.Generic.List[byte]
    $currentValue = [uint32]$ServerId

    while ($currentValue -ge 0x80) {
        $payload.Add([byte](($currentValue -band 0x7F) -bor 0x80))
        $currentValue = $currentValue -shr 7
    }

    $payload.Add([byte]$currentValue)
    return $payload.ToArray()
}

function New-MapInformationsRequestPayload {
    param([double]$MapId)

    $payload = New-Object System.Collections.Generic.List[byte]
    Add-Double -Buffer $payload -Value $MapId
    return $payload.ToArray()
}

function Parse-SelectedServerData {
    param([byte[]]$Payload)

    $offset = 0
    $serverId = Read-VarIntFromBytes -Bytes $Payload -Offset ([ref]$offset)
    $address = Read-UtfFromBytes -Bytes $Payload -Offset ([ref]$offset)
    $portsCount = Read-UnsignedShortFromBytes -Bytes $Payload -Offset ([ref]$offset)

    $ports = @()
    for ($index = 0; $index -lt $portsCount; $index++) {
        $ports += (Read-VarIntFromBytes -Bytes $Payload -Offset ([ref]$offset))
    }

    $offset += 1
    $ticket = Read-UtfFromBytes -Bytes $Payload -Offset ([ref]$offset)

    [pscustomobject]@{
        ServerId = $serverId
        Address = $address
        Port = $ports[0]
        Ticket = $ticket
    }
}

function New-RandomCharacterName {
    $letters = 1..5 | ForEach-Object { [char](Get-Random -Minimum 65 -Maximum 91) }
    return 'Codex' + (-join $letters)
}

function Parse-MapComplementaryInformationsData {
    param([byte[]]$Payload)

    $offset = 0
    $subAreaId = Read-VarIntFromBytes -Bytes $Payload -Offset ([ref]$offset)
    $mapId = [long](Read-DoubleFromBytes -Bytes $Payload -Offset ([ref]$offset))
    $housesCount = Read-UnsignedShortFromBytes -Bytes $Payload -Offset ([ref]$offset)
    $actorsCount = Read-UnsignedShortFromBytes -Bytes $Payload -Offset ([ref]$offset)

    [pscustomobject]@{
        SubAreaId = $subAreaId
        MapId = $mapId
        HousesCount = $housesCount
        ActorsCount = $actorsCount
    }
}

$summary = New-Object System.Collections.Generic.List[string]
$characterName = $null

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
        [void](Read-Packet -Stream $gameStream)

        $charactersListRequest = Encode-Packet -MessageId 150 -Payload ([byte[]]::new(0))
        $gameStream.Write($charactersListRequest, 0, $charactersListRequest.Length)
        $charactersListPacket = Read-Packet -Stream $gameStream
        $charactersList = Parse-CharactersList -Payload $charactersListPacket.Payload

        if ($charactersList.Count -eq 0) {
            $characterName = New-RandomCharacterName
            $creationPayload = New-CharacterCreationPayload -Name $characterName -BreedId 1 -Sex $false
            $creationPacket = Encode-Packet -MessageId 160 -Payload $creationPayload
            $gameStream.Write($creationPacket, 0, $creationPacket.Length)
            $creationResultPacket = Read-Packet -Stream $gameStream
            $charactersListPacket = Read-Packet -Stream $gameStream
            $charactersList = Parse-CharactersList -Payload $charactersListPacket.Payload
            $summary.Add("CreationResultMid=$($creationResultPacket.MessageId) ResultCode=$($creationResultPacket.Payload[0]) CharacterName=$characterName")
        }

        if ($charactersList.Count -eq 0) {
            throw 'No characters are available for selection.'
        }

        $selectedCharacter = $charactersList.Characters[0]
        $selectionPayload = New-CharacterSelectionPayload -CharacterId $selectedCharacter.CharacterId
        $selectionPacket = Encode-Packet -MessageId 152 -Payload $selectionPayload
        $gameStream.Write($selectionPacket, 0, $selectionPacket.Length)

        $bootstrapPackets = @()
        for ($index = 0; $index -lt 6; $index++) {
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

        $mapPackets = @()
        for ($index = 0; $index -lt 3; $index++) {
            $mapPackets += (Read-Packet -Stream $gameStream)
        }

        $mapComplementaryPacket = $mapPackets | Where-Object { $_.MessageId -eq 226 } | Select-Object -First 1
        if ($null -eq $mapComplementaryPacket) {
            throw 'MapComplementaryInformationsData packet was not received.'
        }

        $mapComplementary = Parse-MapComplementaryInformationsData -Payload $mapComplementaryPacket.Payload

        $summary.Add("SelectedCharacterId=$($selectedCharacter.CharacterId) SelectedCharacterName=$($selectedCharacter.Name)")
        $summary.Add("SelectionBootstrapIds=$((($bootstrapPackets | ForEach-Object { $_.MessageId }) -join ','))")
        $summary.Add("SelectionMapId=$mapId")
        $summary.Add("MapBootstrapIds=$((($mapPackets | ForEach-Object { $_.MessageId }) -join ','))")
        $summary.Add("MapActorsCount=$($mapComplementary.ActorsCount) MapSubAreaId=$($mapComplementary.SubAreaId)")
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
