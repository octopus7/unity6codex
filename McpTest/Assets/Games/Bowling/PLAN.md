# Bowling Game Plan

## Goal

Create a self-contained single-player bowling game in a new Unity scene and path, without modifying the existing sample gameplay scene.

## Scope

- 10-frame bowling flow
- Official strike, spare, and 10th-frame bonus scoring
- Aim, power, and hook controls
- Physics-based bowling ball and pin interaction
- Runtime-generated lane, gutters, pins, ball, and HUD
- Restartable game loop

## Paths

- Scene: `Assets/Games/Bowling/Scenes/BowlingGame.unity`
- Runtime code: `Assets/Games/Bowling/Scripts/Runtime/`
- Tests: `Assets/Games/Bowling/Tests/EditMode/`

## Architecture

### Runtime

- `BowlingSceneBootstrap`
  - Activates the bowling game only in the bowling scene.
- `BowlingGameController`
  - Builds the lane and HUD at runtime.
  - Handles input, throw flow, frame progression, and restart.
- `BowlingPin`
  - Tracks standing and settled pin state.
- `BowlingScoreCalculator`
  - Produces frame marks and cumulative score totals.

### Validation

- `BowlingScoreCalculatorTests`
  - Covers perfect game, all spares, and mixed-score scenarios.

## Implementation Phases

1. Create isolated bowling folder structure and scene.
2. Build lane, gutters, walls, deck, lighting, camera, and HUD.
3. Spawn bowling ball and 10-pin rack at runtime.
4. Implement player controls for aim, power, hook, and throw.
5. Detect standing pins and manage frame transitions.
6. Implement official score calculation and edit mode tests.
7. Add build settings entry and verify play mode behavior.

## Current Status

### Completed

- New bowling scene created under `Assets/Games/Bowling/Scenes/`.
- Runtime world generation is working.
- Ball, lane, gutters, 10 pins, and HUD are generated in play mode.
- 10-frame progression and restart loop are implemented.
- Score calculator tests are passing.
- Bowling scene added to build settings.
- Existing runtime MCP demo is skipped when the bowling scene is active.

### Deferred / Nice To Have

- Persisted mesh assets instead of primitive-only runtime geometry
- Audio and pinfall feedback
- Better camera transitions and replay moments
- Stronger material polish and environment art
- Menus, title screen, and results summary polish
- Save/load for best score or session history

## Controls

- `A / D` or `Left / Right`: move aim line
- `W / S` or `Up / Down`: adjust throw power
- `Q / E`: change hook
- `Space / Enter / Left Click`: throw
- `R`: restart game

## Verification Notes

- Edit mode score tests pass.
- Play mode verification confirmed lane, ball, pins, and HUD creation.
- Bowling scene runs without the earlier font exception after switching to `LegacyRuntime.ttf`.
