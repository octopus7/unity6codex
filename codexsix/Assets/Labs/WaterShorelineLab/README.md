# WaterShorelineLab

`Assets/Labs/WaterShorelineLab` is a fully isolated shoreline-water experiment.

- Scene: `Assets/Labs/WaterShorelineLab/Scenes/WaterShorelineLabScene.unity`
- Shader: `Assets/Labs/WaterShorelineLab/Runtime/Shaders/WaterShorelineLab.shader`
- Materials:
  - `Assets/Labs/WaterShorelineLab/Materials/WaterShorelineLab.mat`
  - `Assets/Labs/WaterShorelineLab/Materials/ShorelineGround.mat`
- Breakup Texture:
  - `Assets/Labs/WaterShorelineLab/Textures/ShorelineBreakup.png`
- Script: `Assets/Labs/WaterShorelineLab/Runtime/Scripts/ShorelineDepthCameraMode.cs`
  - `Assets/Labs/WaterShorelineLab/Runtime/Scripts/ProceduralBasinMesh.cs`

The scene is intentionally minimal: basin mesh, cylinder pillar, and water plane.
The camera script forces `DepthTextureMode.Depth` so shoreline depth intersection works without touching other scenes.
