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

function Parse-SelectedServerData {
    param([byte[]]$Payload)

    $offset = 0
    $serverId = Read-VarIntFromBytes -Bytes $Payload -Offset ([ref]$offset)
    $address = Read-UtfFromBytes -Bytes $Payload -Offset ([ref]$offset)
    $portsCount = ((([int]$Payload[$offset]) -shl 8) -bor ([int]$Payload[$offset + 1]))
    $offset += 2

    $ports = @()
    for ($index = 0; $index -lt $portsCount; $index++) {
        $ports += (Read-VarIntFromBytes -Bytes $Payload -Offset ([ref]$offset))
    }

    $offset += 1
    $ticketLength = Read-VarIntFromBytes -Bytes $Payload -Offset ([ref]$offset)
    $ticketBytes = $Payload[$offset..($offset + $ticketLength - 1)]
    $offset += $ticketLength
    $ticket = [System.Text.Encoding]::ASCII.GetString($ticketBytes)

    [pscustomobject]@{
        ServerId = $serverId
        Address = $address
        Port = $ports[0]
        Ticket = $ticket
        TicketBytes = $ticketBytes
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

function New-RandomCharacterName {
    $letters = 1..5 | ForEach-Object { [char](Get-Random -Minimum 65 -Maximum 91) }
    return 'Codex' + (-join $letters)
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

$characterName = New-RandomCharacterName
$summary = New-Object System.Collections.Generic.List[string]

$authClient = [System.Net.Sockets.TcpClient]::new()
$authClient.ReceiveTimeout = 4000
$authClient.SendTimeout = 4000
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
    $gameClient.ReceiveTimeout = 4000
    $gameClient.SendTimeout = 4000
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
        $beforeList = Read-Packet -Stream $gameStream

        $creationPayload = New-CharacterCreationPayload -Name $characterName -BreedId 1 -Sex $false
        $creationPacket = Encode-Packet -MessageId 777 -Payload $creationPayload
        $gameStream.Write($creationPacket, 0, $creationPacket.Length)

        $creationResult = Read-Packet -Stream $gameStream
        $afterList = Read-Packet -Stream $gameStream

        $summary.Add("CharacterName=$characterName")
        $summary.Add("BeforeListMid=$($beforeList.MessageId) BeforeListLen=$($beforeList.PayloadLength)")
        $summary.Add("CreationResultMid=$($creationResult.MessageId) CreationResultLen=$($creationResult.PayloadLength) ResultCode=$($creationResult.Payload[0])")
        $summary.Add("AfterListMid=$($afterList.MessageId) AfterListLen=$($afterList.PayloadLength)")
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
