# Terrain Mesh Movement Lab

This lab is isolated from existing gameplay and network code.

## Isolation Scope

All assets and scripts are limited to:

- `Assets/Labs/TerrainMeshMovementLab`

Existing scenes (`Assets/Scenes/*`) and existing gameplay scripts are not modified.

## Features

- Deterministic generation from world seed + absolute world grid coordinates
- Per-chunk height PNG storage (8-bit grayscale height)
- Mesh reconstruction from saved height PNG
- Physical terrain generation for adjacent chunk grid (default `3x3`) with player-chunk recentering
- Player-centered minimap window with automatic neighbor chunk sampling near boundaries
- Water plane with depth-based color gradient and animated shoreline loop (Built-in shader)
- Runtime performance graph overlay (FPS / frame-time)
- Editor NxN stitched top-down height viewer

## Menus

- `Tools > Terrain Lab > Create Terrain Movement Scene (Safe)`
- `Tools > Terrain Lab > Height Map Viewer`
- `Tools > Terrain Lab > Open README`

## Quick Start

1. In Unity, run `Tools > Terrain Lab > Create Terrain Movement Scene (Safe)`.
2. Enter Play mode.
3. Controls:
   - Move: `WASD` / arrow keys
   - Jump: `Space`
   - Regenerate: `R`
   - Toggle performance graph: `F3`

## Generated Heightmap Path

- `Assets/Labs/TerrainMeshMovementLab/Generated/Heightmaps`

File name pattern:

- `height_s{seed}_cx{x}_cz{z}_v{verts}_p1.png`

## Notes

- Seam continuity is guaranteed by absolute world-grid sampling.
- Normals are computed from padded height samples using central difference.
- This lab is for local feature testing and does not modify Build Settings.
