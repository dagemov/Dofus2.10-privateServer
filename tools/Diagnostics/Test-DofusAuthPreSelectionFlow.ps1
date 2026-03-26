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
        1 { $buffer.Add([byte]$payloadLength) }
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

function Convert-ToHex {
    param([byte[]]$Bytes)

    if ($null -eq $Bytes -or $Bytes.Length -eq 0) {
        return ''
    }

    return ([System.BitConverter]::ToString($Bytes)).Replace('-', '')
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

function Wait-ForPacketAvailability {
    param(
        [System.Net.Sockets.NetworkStream]$Stream,
        [int]$TimeoutMs = 1200
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)

    while ([DateTime]::UtcNow -lt $deadline) {
        if ($Stream.DataAvailable) {
            return $true
        }

        Start-Sleep -Milliseconds 25
    }

    return $Stream.DataAvailable
}

function Read-PacketsUntilIdle {
    param(
        [System.Net.Sockets.NetworkStream]$Stream,
        [int]$InitialTimeoutMs = 1200,
        [int]$IdleTimeoutMs = 300
    )

    $packets = @()

    if (-not (Wait-ForPacketAvailability -Stream $Stream -TimeoutMs $InitialTimeoutMs)) {
        return $packets
    }

    while ($true) {
        $packets += Read-Packet -Stream $Stream

        $idleDeadline = [DateTime]::UtcNow.AddMilliseconds($IdleTimeoutMs)
        while (-not $Stream.DataAvailable -and [DateTime]::UtcNow -lt $idleDeadline) {
            Start-Sleep -Milliseconds 20
        }

        if (-not $Stream.DataAvailable) {
            break
        }
    }

    return $packets
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

function Parse-ServersListPayload {
    param([byte[]]$Payload)

    $offset = 0
    $serversCount = ((([int]$Payload[$offset]) -shl 8) -bor ([int]$Payload[$offset + 1]))
    $offset += 2
    $servers = @()

    for ($index = 0; $index -lt $serversCount; $index++) {
        $flags = [int]$Payload[$offset]
        $offset += 1
        $serverId = ((([int]$Payload[$offset]) -shl 8) -bor ([int]$Payload[$offset + 1]))
        $offset += 2
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

        $servers += [pscustomobject]@{
            ServerId = $serverId
            IsMonoAccount = (($flags -band 0x01) -ne 0)
            IsSelectable = (($flags -band 0x02) -ne 0)
            Type = $type
            Status = $status
            Completion = $completion
            CharactersCount = $charactersCount
            CharacterSlots = $characterSlots
            Date = $date
        }
    }

    $alreadyConnectedToServerId = 0
    if ($offset -lt $Payload.Length) {
        $alreadyConnectedToServerId = Read-VarIntFromBytes -Bytes $Payload -Offset ([ref]$offset)
    }

    $canCreateNewCharacter = $false
    if ($offset -lt $Payload.Length) {
        $canCreateNewCharacter = $Payload[$offset] -ne 0
    }

    [pscustomobject]@{
        Servers = $servers
        AlreadyConnectedToServerId = $alreadyConnectedToServerId
        CanCreateNewCharacter = $canCreateNewCharacter
    }
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
    $bootstrapPackets = Read-PacketsUntilIdle -Stream $stream -InitialTimeoutMs 1200 -IdleTimeoutMs 300

    "BOOT count={0} ids={1}" -f $bootstrapPackets.Count, (($bootstrapPackets | ForEach-Object { $_.MessageId }) -join ',')
    foreach ($packet in $bootstrapPackets) {
        "BOOT mid={0} len={1} hex={2}" -f $packet.MessageId, $packet.PayloadLength, $packet.Hex
    }

    $identificationPayload = New-IdentificationPayload -Username 'sebcos1' -Password 'polondrolo3'
    $identificationPacket = Encode-Packet -MessageId 4 -Payload $identificationPayload
    $stream.Write($identificationPacket, 0, $identificationPacket.Length)

    $authPackets = Read-PacketsUntilIdle -Stream $stream -InitialTimeoutMs 1500 -IdleTimeoutMs 400
    "AUTH count={0} ids={1}" -f $authPackets.Count, (($authPackets | ForEach-Object { $_.MessageId }) -join ',')

    foreach ($packet in $authPackets) {
        "AUTH mid={0} len={1} hex={2}" -f $packet.MessageId, $packet.PayloadLength, $packet.Hex

        if ($packet.MessageId -eq 30) {
            $serversList = Parse-ServersListPayload -Payload $packet.Payload
            "AUTH servers alreadyConnectedToServerId={0} canCreate={1}" -f $serversList.AlreadyConnectedToServerId, $serversList.CanCreateNewCharacter
            foreach ($server in $serversList.Servers) {
                "AUTH server id={0} selectable={1} mono={2} type={3} status={4} completion={5} chars={6}/{7} date={8}" -f `
                    $server.ServerId, `
                    $server.IsSelectable, `
                    $server.IsMonoAccount, `
                    $server.Type, `
                    $server.Status, `
                    $server.Completion, `
                    $server.CharactersCount, `
                    $server.CharacterSlots, `
                    $server.Date
            }
        }
    }
}
finally {
    if ($stream) {
        $stream.Dispose()
    }

    $client.Dispose()
}
