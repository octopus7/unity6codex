# VoxelVillage Monster Invader Visual Spec

## Purpose

This document fixes the first-pass visual direction and prefab hierarchy for the squid-type village invasion tracker.

- Working name: `묵행 추적자`
- Role: village invasion event pursuer, not a direct combat boss
- Camera read: low, wide, slippery silhouette during pursuit; tall silhouette only during threat telegraphs
- Runtime target: procedural transform animation, not skinned mesh animation

## Scale Rules

Resident scale is the reference baseline.

- Villager full height reference: about `1.8m`
- Idle body apex: villager height x `2.4 ~ 2.8`
- Threat pose apex: villager height x `3.7 ~ 4.0`
- Mantle width: about `2.6 ~ 3.0m`
- Full leg span: about `7.5 ~ 9.0m`
- Recommended gameplay footprint: `2 x 2` grid cells for the core body
- Recommended threat radius: reads like `3 x 3` cells when legs and tentacles are fully spread

Design rule:
Do not keep the monster at full height all the time. The normal read should be low and broad. The tall `4x` silhouette is reserved for alert, strike, and ink-cast poses.

## Visual Direction

### Silhouette

- Broad forward mantle with a slightly dropped front edge
- Six walking limbs arranged in a crab-like ring for stable side-stepping
- Two longer attack tentacles mounted forward under the mantle
- Rear profile should feel heavy and wet, not spiky or skeletal
- The underside should remain visibly brighter than the mantle shell so attack poses read clearly from the game camera

### Shape Language

- Mantle: smooth wedge / dome mass
- Walk limbs: angular segmented struts with chunky joints
- Attack tentacles: smoother and longer than walk limbs
- Mouth core: compact beak cluster hidden under the front underside
- Eye placement: left and right forward quarters, not centered

### Surface Rules

- Keep the mantle readable with broad voxel planes, not noisy micro-detail
- Favor stepped bevels over thin horns or decorative spikes
- Wetness should come from color contrast and a limited highlight band, not high gloss everywhere
- Keep thin one-voxel filaments below `10%` of the visible silhouette

## Color Palette

Use a restrained wet-night palette.

- Mantle shell: near-black teal
- Shadow shell: deep blue-black
- Underside: pale desaturated cyan-gray
- Vein accent: muted algae green
- Alert accent: cold mint glow, used only in eyes and pulse seams
- Ink effect: dense black with faint cyan fringe

Suggested palette values for the first pass:

- `MantleBase`: `#1B2830`
- `MantleShadow`: `#11181E`
- `Underside`: `#A8B9BF`
- `VeinAccent`: `#4C7C6D`
- `AlertGlow`: `#A9F3D6`
- `InkCore`: `#0B0E12`

## Pose Language By State

### Dormant

- Body low and folded
- Legs tucked closer to the mantle
- Tentacles relaxed and almost hidden
- Eyes dim or off

### Search

- Mantle gently sways left and right
- Front tentacles test the ground ahead
- Rear legs remain planted longer than front legs

### Pursuit

- Body lowers by about `10%`
- Side legs extend wider than front legs
- Movement read should feel like a lateral cut-off, not a straight charge

### Threaten

- Body rises by about `18%`
- Front tentacles lift high enough to create the temporary `4x` height read
- Eye glow and seam glow turn on

### Strike

- Body pauses for a short, readable brace
- Support legs lock outward
- One or both attack tentacles extend fast on a clean line

### Ink Cast

- Mantle inflates backward and upward before firing
- The underside becomes more visible during the cast
- Ink burst origin should be easy to identify from the camera

### Retreat

- Body collapses down again
- Tentacles fold inward
- Rearward legs dominate the motion silhouette

## Module Breakdown

Build the monster from reusable rigid voxel modules.

- `MantleCore`
- `MantleUnderside`
- `EyeCluster_L`
- `EyeCluster_R`
- `MouthCore`
- `Leg_FL`
- `Leg_FR`
- `Leg_ML`
- `Leg_MR`
- `Leg_RL`
- `Leg_RR`
- `Tentacle_Attack_L`
- `Tentacle_Attack_R`

Each walk leg should be split into:

- `Hip`
- `Upper`
- `Lower`
- `Tip`

Each attack tentacle should be split into:

- `Base`
- `Mid`
- `Tip`

## Prefab Hierarchy

Recommended root prefab:

```text
VV_Threat_MukhaengTracker_Root
  LocomotionRoot
    BodyPivot
      MantleRoot
        MantleCoreVisual
        MantleUndersideVisual
        EyeCluster_L
        EyeCluster_R
        MouthCore
      LegRing
        Leg_FL
          Leg_FL_Hip
            Leg_FL_UpperVisual
            Leg_FL_Knee
              Leg_FL_LowerVisual
              Leg_FL_Ankle
                Leg_FL_TipVisual
                Leg_FL_FootTarget
        Leg_FR
          Leg_FR_Hip
            Leg_FR_UpperVisual
            Leg_FR_Knee
              Leg_FR_LowerVisual
              Leg_FR_Ankle
                Leg_FR_TipVisual
                Leg_FR_FootTarget
        Leg_ML
          Leg_ML_Hip
            Leg_ML_UpperVisual
            Leg_ML_Knee
              Leg_ML_LowerVisual
              Leg_ML_Ankle
                Leg_ML_TipVisual
                Leg_ML_FootTarget
        Leg_MR
          Leg_MR_Hip
            Leg_MR_UpperVisual
            Leg_MR_Knee
              Leg_MR_LowerVisual
              Leg_MR_Ankle
                Leg_MR_TipVisual
                Leg_MR_FootTarget
        Leg_RL
          Leg_RL_Hip
            Leg_RL_UpperVisual
            Leg_RL_Knee
              Leg_RL_LowerVisual
              Leg_RL_Ankle
                Leg_RL_TipVisual
                Leg_RL_FootTarget
        Leg_RR
          Leg_RR_Hip
            Leg_RR_UpperVisual
            Leg_RR_Knee
              Leg_RR_LowerVisual
              Leg_RR_Ankle
                Leg_RR_TipVisual
                Leg_RR_FootTarget
      AttackTentacles
        Tentacle_Attack_L
          Tentacle_Attack_L_Base
            Tentacle_Attack_L_Mid
              Tentacle_Attack_L_Tip
                Tentacle_Attack_L_HitOrigin
        Tentacle_Attack_R
          Tentacle_Attack_R_Base
            Tentacle_Attack_R_Mid
              Tentacle_Attack_R_Tip
                Tentacle_Attack_R_HitOrigin
  Sensors
    VisionOrigin
    TargetOrigin
    ThreatCenter
    AudioOrigin
    InkCastOrigin
    RetreatAnchor
  Gameplay
    BodyBlocker
    ThreatCollider_Close
    ThreatCollider_Tentacle
    OccupancyBounds
  FX
    EyeGlow_L
    EyeGlow_R
    InkBurstFxOrigin
    GroundRippleOrigin
```

## Responsibility Split

- `VV_Threat_MukhaengTracker_Root`: world position, state machine, occupancy, target selection
- `LocomotionRoot`: step cycle translation and vertical weight shifts
- `BodyPivot`: facing, bank, rise, and threat pose offsets
- `MantleRoot`: central visual mass and state-driven squash / stretch offsets
- `Leg_*`: procedural walking only; do not attach game rules here
- `Sensors`: stable marker transforms for AI, camera, sound, and VFX
- `Gameplay`: colliders and hit volumes only
- `FX`: cosmetic anchors only

## Leg Layout

Use six walking legs and two attack tentacles.

- `Leg_FL`, `Leg_FR`: front support pair, widest readable threat stance
- `Leg_ML`, `Leg_MR`: primary locomotion pair, strongest lateral stepping read
- `Leg_RL`, `Leg_RR`: rear push pair, strongest retreat read
- `Tentacle_Attack_L`, `Tentacle_Attack_R`: fast strike pair, not used for normal walking

Recommended placement rule:

- Body center sits above the middle pair
- Front pair should be slightly forward and wider
- Rear pair should be slightly longer and lower
- Attack tentacles should emerge from the front underside, not from the top silhouette

## Mesh And Material Budget

Target a controlled first-pass budget.

- Material groups: `3` max
- Group 1: mantle shell and walk limbs
- Group 2: underside and mouth core
- Group 3: eyes, glow seams, ink emissive details
- Keep the first-pass visible mesh module count under `20`
- Reuse mirrored leg modules where possible

## Folder And Naming Proposal

When implementation starts, keep the threat content isolated under a dedicated slice.

```text
Assets/Games/VoxelVillage/
  Art/
    Threats/
      MukhaengTracker/
        Prefabs/
          VV_Threat_MukhaengTracker.prefab
        Meshes/
        Materials/
        Textures/
  Scripts/
    Runtime/
      Threats/
```

## First Implementation Notes

- Use rigid child transforms, not skinned bones
- Drive motion through a custom runtime controller
- Step targets can be procedural and snapped to walkable ground
- The body should lag slightly behind the middle-leg stepping rhythm
- Attack tentacles should use their own extend / retract timing separate from the walk cycle
- If the monster enters narrow village paths, compress width by pose first before shrinking gameplay footprint

## Non-Goals For Pass One

- Full IK solver
- Physics-based tentacle simulation
- Destructible limbs
- High-frequency idle noise
- Complex shader-driven slime animation

## Approval Anchor

Current approved read:

- Normal read: about `2.5x` villager height
- Threat pose read: about `4x` villager height
- Emotional read: low and wide during pursuit, tall only during telegraph
- Role read: night-time village invasion pursuer
