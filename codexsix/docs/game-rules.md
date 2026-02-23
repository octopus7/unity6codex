# Game Rules (MVP)

## Session
- Session has no match-end condition.
- No kill-target or score-to-win rule.
- Server runs continuously until shutdown.

## Core Combat
- HP: `100`
- Bullet damage: `25`
- Fire cooldown: `0.2s` (6 ticks @ 30Hz)
- Bullet speed: `22 u/s`
- Bullet lifetime: `1.5s`
- Respawn delay: `5s`

## Movement and Camera
- Input: WASD movement + mouse aim + left click fire.
- Camera: perspective, fixed pitch `60`, no runtime rotation.
- Base speed: `6 u/s`.

## Coin Loop
- Player carries coins while alive.
- On death by another player, dropped amount is `max(1, carriedCoins)`.
- If carried coins are `0`, at least `1` coin is dropped.
- Drop is split into up to 5 random stacks.
- Pickup is automatic by proximity (`1.2u` radius).
- On death, victim carried coins reset to `0`.
- Coin stacks do not expire by time.
- World coin total cap: `5000`.
  - If exceeded, oldest stacks are removed first.

## Shop
- Shop zone is a safe zone.
  - No shooting inside.
  - No damage inside.
- Portal topology:
  - 2 entry portals from battle area.
  - 1 exit portal from shop to battle area.
- Re-entering shop is only through entry portals.
- Stay limit in shop: none.
- Purchase interaction: stand on item and press fire button.

### Shop items
- `Item 1: Heal50`
  - Cost: `5` coins
  - Effect: `+50 HP` (cap at `100`)
- `Item 2: MoveSpeed+20%`
  - Cost: `8` coins
  - Effect: `+20%` move speed per stack
  - Max stacks: `2`
  - At max stacks, repurchase is rejected

Buff persistence:
- Shop buffs are life-bound.
- On death, speed buff stacks reset.

## World Layout
- Battle bounds: `[-20, 20] x [-20, 20]`
- Shop zone bounds: `x[-6,6], y[22,34]`
- Spawn points: 8 fixed points
- Obstacles: 5 fixed blocks
