# Network Protocol (TCP, Binary, v1)

## Frame Header
- `uint32 payloadLength` (little-endian)
- `uint16 messageType`
- `uint16 protocolVersion` (`1`)
- `uint32 sequence`

Header size is always 12 bytes.

## Client -> Server

### `1 Hello`
- `string8 nickname` (max 16 bytes)

### `2 InputFrame`
- `uint32 inputSeq`
- `uint32 clientTick`
- `int16 moveX` (`-32767..32767`)
- `int16 moveY` (`-32767..32767`)
- `float aimX`
- `float aimY`
- `byte buttons` (`bit0 = fireHeld`)

### `3 Ping`
- `int64 clientUnixMs`

### `4 ShopPurchaseRequest`
- `byte itemId`
- `uint32 requestSeq`

## Server -> Client

### `101 Welcome`
- `int32 playerId`
- `int32 tickRateHz`
- `int32 snapshotRateHz`
- `int32 maxPlayers`

### `102 Snapshot`
- `uint32 serverTick`
- `uint32 lastProcessedInputSeq`
- `uint16 playerCount`
- `players[playerCount]`:
  - `int32 playerId`
  - `float posX`
  - `float posY`
  - `float aimX`
  - `float aimY`
  - `int16 hp`
  - `int32 carriedCoins`
  - `byte speedBuffStacks`
  - `byte flags` (`bit0=alive`, `bit1=inShop`)
- `uint16 projectileCount`
- `projectiles[projectileCount]`:
  - `int32 projectileId`
  - `int32 ownerPlayerId`
  - `float posX`
  - `float posY`
  - `float dirX`
  - `float dirY`
- `uint16 coinStackCount`
- `coinStacks[coinStackCount]`:
  - `int32 coinStackId`
  - `float posX`
  - `float posY`
  - `int32 amount`
- `byte portalCount`
- `portals[portalCount]`:
  - `byte portalId`
  - `float posX`
  - `float posY`
  - `byte portalType` (`1=Entry`, `2=Exit`)
- `float shopMinX`
- `float shopMinY`
- `float shopMaxX`
- `float shopMaxY`

### `103 EventBatch`
- `uint32 serverTick`
- `uint16 eventCount`
- `events[eventCount]`:
  - `byte eventType`
  - `int32 actorId`
  - `int32 targetId`
  - `int32 value`
  - `int32 extraId`
  - `float posX`
  - `float posY`

### `104 Pong`
- `int64 clientUnixMs`
- `int64 serverUnixMs`

### `199 Error`
- `uint16 errorCode`
- `string8 message` (max 200 bytes)
