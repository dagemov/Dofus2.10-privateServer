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
        Hex = Convert-ToHex -Bytes $payload
    }
}

function Read-AvailablePacket {
    param([System.Net.Sockets.NetworkStream]$Stream)

    if (-not $Stream.DataAvailable) {
        return $null
    }

    return Read-Packet -Stream $Stream
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

function Convert-ToHex {
    param([byte[]]$Bytes)

    if ($null -eq $Bytes -or $Bytes.Length -eq 0) {
        return ''
    }

    return ([System.BitConverter]::ToString($Bytes)).Replace('-', '')
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

    $canCreate = $Payload[$offset] -ne 0
    $offset += 1
    $ticketLength = Read-VarIntFromBytes -Bytes $Payload -Offset ([ref]$offset)
    $ticketBytes = $Payload[$offset..($offset + $ticketLength - 1)]
    $offset += $ticketLength
    $ticket = [System.Text.Encoding]::ASCII.GetString($ticketBytes)

    [pscustomobject]@{
        ServerId = $serverId
        Address = $address
        Ports = $ports
        CanCreate = $canCreate
        Ticket = $ticket
        TicketBytes = $ticketBytes
        TicketHex = Convert-ToHex -Bytes $ticketBytes
    }
}

function Parse-ServersListPayload {
    param([byte[]]$Payload)

    $offset = 0
    $serversCount = ((([int]$Payload[$offset]) -shl 8) -bor ([int]$Payload[$offset + 1]))
    $offset += 2
    $servers = @()

    for ($index = 0; $index -lt $serversCount; $index++) {
        $flags = [int]$Payload[$offset]
        $offset += 1
        $serverId = Read-VarIntFromBytes -Bytes $Payload -Offset ([ref]$offset)
        $type = [int]$Payload[$offset]
        $offset += 1
        $status = [int]$Payload[$offset]
        $offset += 1
        $completion = [int]$Payload[$offset]
        $offset += 1
        $charactersCount = [int]$Payload[$offset]
        $offset += 1
        $characterSlots = [int]$Payload[$offset]
        $offset += 1
        $date = [System.BitConverter]::ToDouble($Payload, $offset)
        $offset += 8
        $isSelectable = (($flags -band 0x02) -ne 0)
        $isMonoAccount = (($flags -band 0x01) -ne 0)

        $servers += [pscustomobject]@{
            ServerId = $serverId
            Type = $type
            Status = $status
            Completion = $completion
            IsSelectable = $isSelectable
            IsMonoAccount = $isMonoAccount
            CharactersCount = $charactersCount
            CharacterSlots = $characterSlots
            Date = $date
        }
    }

    $canCreateNewCharacter = $false
    if ($offset -lt $Payload.Length) {
        $canCreateNewCharacter = $Payload[$offset] -ne 0
    }

    [pscustomobject]@{
        Servers = $servers
        CanCreateNewCharacter = $canCreateNewCharacter
    }
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

$client = [System.Net.Sockets.TcpClient]::new()
$client.ReceiveTimeout = 3000
$client.SendTimeout = 3000
$client.NoDelay = $true
$client.Connect('127.0.0.1', 5555)

try {
    $stream = $client.GetStream()

    $pingPacket = Encode-Packet -MessageId 182 -Payload ([byte[]](1))
    $stream.Write($pingPacket, 0, $pingPacket.Length)

    $bootstrapMain = Read-Exact -Stream $stream -Count 2342
    "BOOT raw[0] bytes={0} head={1}" -f $bootstrapMain.Length, (Convert-ToHex -Bytes $bootstrapMain[0..31])

    Start-Sleep -Milliseconds 150
    $bootstrapFollowUp = Read-AvailablePacket -Stream $stream

    if ($null -ne $bootstrapFollowUp) {
        "BOOT packet mid={0} len={1} hex={2}" -f $bootstrapFollowUp.MessageId, $bootstrapFollowUp.PayloadLength, $bootstrapFollowUp.Hex
    }

    $identificationPayload = New-IdentificationPayload -Username 'sebcos1' -Password 'polondrolo3'
    $identificationPacket = Encode-Packet -MessageId 4 -Payload $identificationPayload
    $stream.Write($identificationPacket, 0, $identificationPacket.Length)

    $selectedServerData = $null

    while ($true) {
        $packet = Read-Packet -Stream $stream
        "RESP mid={0} len={1} hex={2}" -f $packet.MessageId, $packet.PayloadLength, $packet.Hex

        if ($packet.MessageId -eq 30) {
            $serversList = Parse-ServersListPayload -Payload $packet.Payload
            $selectionTarget = $serversList.Servers |
                Where-Object { $_.CharactersCount -gt 0 } |
                Select-Object -First 1

            if ($null -eq $selectionTarget) {
                $selectionTarget = $serversList.Servers | Select-Object -First 1
            }

            "AUTH servers: {0} canCreate={1}" -f (($serversList.Servers | ForEach-Object { "id=$($_.ServerId)/type=$($_.Type)/chars=$($_.CharactersCount)/$($_.CharacterSlots)/status=$($_.Status)/selectable=$($_.IsSelectable)/mono=$($_.IsMonoAccount)" }) -join ', '), $serversList.CanCreateNewCharacter

            $selectionPayload = New-ServerSelectionPayload -ServerId $selectionTarget.ServerId
            $selectionPacket = Encode-Packet -MessageId 40 -Payload $selectionPayload
            $stream.Write($selectionPacket, 0, $selectionPacket.Length)
            "AUTH sent server selection for server {0}" -f $selectionTarget.ServerId
            continue
        }

        if ($packet.MessageId -eq 50) {
            $statusUpdate = Parse-ServersListPayload -Payload (@(0,1) + $packet.Payload + @(0))
            "AUTH status update: {0}" -f (($statusUpdate.Servers | ForEach-Object { "id=$($_.ServerId)/type=$($_.Type)/chars=$($_.CharactersCount)/$($_.CharacterSlots)/status=$($_.Status)/selectable=$($_.IsSelectable)/mono=$($_.IsMonoAccount)" }) -join ', ')
            continue
        }

        if ($packet.MessageId -eq 42 -and $packet.PayloadLength -gt 0) {
            $selectedServerData = Parse-SelectedServerData -Payload $packet.Payload
            "AUTH selected server data: serverId={0} address={1} port={2} ticket={3}" -f `
                $selectedServerData.ServerId, `
                $selectedServerData.Address, `
                $selectedServerData.Ports[0], `
                $selectedServerData.Ticket
            break
        }
    }

    if ($null -eq $selectedServerData) {
        throw 'SelectedServerData was not received.'
    }

    $gameClient = [System.Net.Sockets.TcpClient]::new()
    $gameClient.ReceiveTimeout = 3000
    $gameClient.SendTimeout = 3000
    $gameClient.NoDelay = $true
    $gameClient.Connect($selectedServerData.Address, $selectedServerData.Ports[0])

    try {
        $gameStream = $gameClient.GetStream()
        $firstGamePacket = Read-Packet -Stream $gameStream
        "GAME mid={0} len={1} hex={2}" -f $firstGamePacket.MessageId, $firstGamePacket.PayloadLength, $firstGamePacket.Hex

        if ($firstGamePacket.MessageId -eq 1) {
            $helloGame = Read-Packet -Stream $gameStream
            "GAME mid={0} len={1} hex={2}" -f $helloGame.MessageId, $helloGame.PayloadLength, $helloGame.Hex
        }
        else {
            $helloGame = $firstGamePacket
        }

        $ticketPayload = New-AuthenticationTicketPayload -Language 'es' -Ticket $selectedServerData.Ticket
        $ticketPacket = Encode-Packet -MessageId 110 -Payload $ticketPayload
        $gameStream.Write($ticketPacket, 0, $ticketPacket.Length)

        $ticketResponse = Read-Packet -Stream $gameStream
        "GAME mid={0} len={1} hex={2}" -f $ticketResponse.MessageId, $ticketResponse.PayloadLength, $ticketResponse.Hex

        $charactersListRequest = Encode-Packet -MessageId 150 -Payload ([byte[]]::new(0))
        $gameStream.Write($charactersListRequest, 0, $charactersListRequest.Length)

        $charactersList = Read-Packet -Stream $gameStream
        "GAME mid={0} len={1} hex={2}" -f $charactersList.MessageId, $charactersList.PayloadLength, $charactersList.Hex
    }
    finally {
        if ($gameStream) {
            $gameStream.Dispose()
        }

        $gameClient.Dispose()
    }
}
catch {
    "END $($_.Exception.Message)"
}
finally {
    if ($stream) {
        $stream.Dispose()
    }

    $client.Dispose()
}
