# Pond_Level_1 Scene Guide

This document explains what is in `Assets/Scenes/AlphaDemo/Pond_Level_1.unity`, what scripts are attached, and how the systems interact so someone external can recreate or edit the scene safely.

## 1. Scene Purpose (High Level)

`Pond_Level_1` is a playable fishing setup composed of:
- A camera + sun light.
- A pond/water target area and environment art dressing.
- A fishing rig (rod, cast marker, bobber, line renderer).
- Input bridges for keyboard, Xbox, and Joy-Con (JoyShockLibrary).
- A fish-spawn/catch manager (`PondManager`) attached to the water object.

The core gameplay loop is:
1. Move cast target on water.
2. Cast bobber in an arc.
3. Bobber lands; optional tension mode + directional swing.
4. Yank/retract bobber.
5. `PondManager` checks fish near bobber and removes caught fish.

## 2. Root Hierarchy and Roles

Root GameObjects (4 total):
- `Environment Objects`
- `FishingRodMech`
- `PlayerCamera_TEMP`
- `Sunlight`

### 2.1 Hierarchy (important objects/components)

```text
Environment Objects
  BackgroundTerrain
  Pond
    WaterCastTarget (MeshFilter, MeshRenderer disabled, MeshCollider enabled)
    WaterShader (MeshFilter, MeshRenderer enabled, MeshCollider disabled, PondManager)
  Terrain (Terrain, TerrainCollider)
  TreesRocksPlants (many prefab instances from SimpleNaturePack)

FishingRodMech
  BobberController (BobberArcCaster, BobberButtonInput)
  CastMarker (Transform, MeshFilter, MeshRenderer, SphereCollider)
  InputManager (JslStickInput)
    JoyConFishingMotion (JoyConGestureDetector, JoyConDirectionalSwingInput)
    XboxFishingControls (XboxFishingInput)
  RodRig (LineRenderer, RodLineVisualizer)
    RodBase
    RodTip
      BobberHangPoint
  Targeting (CursorCastTargeting)
  [Prefab instance from Assets/Models/bobber.fbx]
    + added SphereCollider
    + added BobberIdleSway

PlayerCamera_TEMP (Camera, AudioListener, UniversalAdditionalCameraData)
Sunlight (Directional Light, UniversalAdditionalLightData)
```

## 3. Transform/Layout Anchors

Important placed transforms:
- `Environment Objects` local position: `(-545.583, 0, -417)`
- `FishingRodMech` local position: `(8.52007, 8.73136, 3.64826)`
- `PlayerCamera_TEMP` local position: `(8.95, 11.61, 8.481)`, euler approx `(5, 180, 0)`
- `Sunlight` local position: `(10, 27.12, -1.88)`
- `Pond/WaterCastTarget` local position: `(553.92, 9.72, 413.86)`, scale `(14, 0.01, 15)`
- `Pond/WaterShader` local position: `(553.85, 9.71, 414.5)`, scale `(2, 1, 2)`

The environment root offset + pond child offsets place the playable water near the camera and rod rig.

## 4. Layers and Raycast Targeting

From `ProjectSettings/TagManager.asset`:
- Layer 4 = `Water`
- Layer 6 = `Ground`
- Layer 7 = `WaterCast`

Scene object layers:
- `WaterShader` is on layer `Water` (4).
- `WaterCastTarget` is on layer `WaterCast` (7).
- `Terrain` is on layer `Ground` (6).

`CursorCastTargeting.waterMask` is `m_Bits: 144` (layers 4 and 7), so targeting raycasts can hit both visual and cast-target water layers.

## 5. Script Wiring (Scene References)

## 5.1 Core Fishing Scripts

### `BobberArcCaster` (`Assets/Scripts/Fishing/BobberArcCaster.cs`)
Attached to: `FishingRodMech/BobberController`

Key references in scene:
- `rodTip` -> `FishingRodMech/RodRig/RodTip`
- `bobber` -> bobber prefab transform (stripped instance from `Assets/Models/bobber.fbx`)
- `bobberHangPoint` -> `FishingRodMech/RodRig/RodTip/BobberHangPoint`
- `targetMarker` -> `FishingRodMech/CastMarker`
- `rodSwingPivot` -> `FishingRodMech/RodRig/RodBase`

Key tuned values:
- Cast: `castDuration=0.75`, `arcHeight=3`
- Yank: `yankDuration=0.25`
- Directional swing enabled with strong angles: up `-50`, left `-50`, right `50`
- Tension bobbing currently disabled in scene (`tensionBobbingEnabled=0`)

State machine used by multiple systems:
- `Idle -> InFlight -> Landed -> (optional Tension) -> Retracting -> Idle`

### `CursorCastTargeting` (`Assets/Scripts/Fishing/CursorCastTargeting.cs`)
Attached to: `FishingRodMech/Targeting`

References:
- `cam` -> `PlayerCamera_TEMP`
- `castMarker` -> `FishingRodMech/CastMarker`
- `waterCollider` -> `Environment Objects/Pond/WaterCastTarget` mesh collider
- `bobberArcCaster` -> `FishingRodMech/BobberController/BobberArcCaster`
- `jslInput` -> `FishingRodMech/InputManager/JslStickInput`

Behavior:
- Hides marker while caster is not `Idle`.
- Supports mouse-right targeting and stick targeting.
- Keeps last valid target point and restores it when returning to idle.
- Clamps target movement to water collider bounds.

### `RodLineVisualizer` (`Assets/Scripts/Fishing/RodLineVisualizer.cs`)
Attached to: `FishingRodMech/RodRig`

References:
- `rodTip` -> `RodTip`
- `bobber` -> bobber transform
- `bobberArcCaster` -> caster
- `waterSurface` is currently `null` in scene

Behavior:
- Draws sagging line between tip and bobber.
- Uses less slack when bobber moves quickly.
- In `Tension`, forces tight line (zero slack).

Because `waterSurface` is null, water clamping heuristics in this script are not active unless assigned later.

### `BobberIdleSway` (`Assets/Scripts/Fishing/BobberIdleSway.cs`)
Attached to: bobber prefab instance in scene (added component)

References:
- `hangPoint` -> `BobberHangPoint`
- `bobberArcCaster` -> caster

Behavior:
- Idle state: suspended sway around hang point.
- Landed state: water drift/current motion.
- Tension state: locks at tension anchor.

### `PondManager` (`Assets/Scripts/Fishing/PondManager.cs`)
Attached to: `Environment Objects/Pond/WaterShader`

References/values:
- `fishPrefabs`: `Assets/Prefabs/Old/Fish1.prefab`, `Assets/Prefabs/Old/Fish2.prefab`
- `playerBobber`: bobber prefab GameObject in scene
- `radius=6`, `waterlevel=9`, `catchRadius=1.5`

Behavior:
- Spawns 10 fish at start inside radius circle.
- `R`: spawn fish.
- `T`: remove random fish.
- `Space`: attempt catch nearest fish to bobber in 2D XZ radius.

Fish prefabs contain:
- `Rigidbody`
- `CapsuleCollider`
- `FishMovement` (`Assets/Scripts/Old/FishMovement.cs`)

### `BobberButtonInput` (`Assets/Scripts/Fishing/BobberButtonInput.cs`)
Attached to: `FishingRodMech/BobberController`

Scene keys:
- `castKey=273` (UpArrow)
- `yankKey=274` (DownArrow)
- `tensionKey=104` (`H`)

This is a keyboard fallback path into `BobberArcCaster`.

## 5.2 Input Bridge Scripts

### `XboxFishingInput` (`Assets/Scripts/Input/XboxFishingInput.cs`)
Attached to: `FishingRodMech/InputManager/XboxFishingControls`

References:
- `caster` -> `BobberArcCaster`
- `targeting` -> `CursorCastTargeting`

Scene config:
- Single-button cast/yank enabled (`A` toggles behavior by bobber state)
- `B` toggles tension
- Right stick feeds targeting input
- D-pad + left stick feed directional swing during tension only

### `JslStickInput` (`Assets/Scripts/Input/JslStickInput.cs`)
Attached to: `FishingRodMech/InputManager`

Behavior:
- Reads stick input from JoyShockLibrary (`JoyShockLibrary` native plugin).
- Auto-picks connected device/strongest stick if configured.
- Feeds `CursorCastTargeting` (directly referenced there).

### `JoyConGestureDetector` (`Assets/Scripts/Input/JoyConGestureDetector.cs`)
Attached to: `FishingRodMech/InputManager/JoyConFishingMotion`

Uses IMU thresholds to fire UnityEvents:
- `onCast` -> `BobberArcCaster.Cast()`
- `onYank` -> `BobberArcCaster.Yank()`

### `JoyConDirectionalSwingInput` (`Assets/Scripts/Input/JoyConDirectionalSwingInput.cs`)
Attached to: `FishingRodMech/InputManager/JoyConFishingMotion`

Behavior:
- Converts gyro motion to held swing directions (up/left/right).
- Calls `BobberArcCaster.SetDirectionalSwingHeld(...)`.
- Only drives directional swing during tension state (`onlyWhenTensionState=1`).

## 6. How Systems Work Together (Runtime Flow)

1. **Targeting active while idle**
   - `CursorCastTargeting` updates `CastMarker` from mouse/right stick/JSL stick.

2. **Cast trigger paths**
   - Keyboard (`BobberButtonInput`), Xbox (`XboxFishingInput`), or Joy-Con gesture event (`JoyConGestureDetector`) call `BobberArcCaster.Cast()`.

3. **Bobber flight and landing**
   - `BobberArcCaster` animates bobber arc to marker.
   - State transitions to `Landed` on completion.

4. **Visual response while landed**
   - `BobberIdleSway` applies water drift.
   - `RodLineVisualizer` draws/slackens line and can sway line.

5. **Tension mode**
   - Triggered by keyboard/Xbox.
   - `BobberArcCaster` changes rod pose and accepts directional swing input.
   - Directional input can come from Joy-Con gyro and/or Xbox controls.

6. **Yank/retract**
   - Input scripts call `BobberArcCaster.Yank()`.
   - Bobber retracts to hang point, state returns to `Idle`.

7. **Fish interactions**
   - `PondManager` manages spawned fish and catch checks vs bobber position.

## 7. Recreate Checklist (From Scratch)

1. Create a new scene and add four roots: `Environment Objects`, `FishingRodMech`, `PlayerCamera_TEMP`, `Sunlight`.
2. Add URP camera/light additional data components to camera/light (already required in URP projects).
3. Under `Environment Objects/Pond`, add:
   - `WaterCastTarget` plane-like object with **enabled** `MeshCollider`, optional hidden renderer, layer `WaterCast`.
   - `WaterShader` visual water mesh on layer `Water`, add `PondManager`.
4. Add `Terrain` with `Terrain` + `TerrainCollider` and set layer `Ground`.
5. Add vegetation/rocks as prefab instances (SimpleNaturePack assets used in this scene).
6. Build fishing rig under `FishingRodMech`:
   - `RodRig` with `LineRenderer` + `RodLineVisualizer`
   - `RodBase`, `RodTip`, `BobberHangPoint`
   - `CastMarker`
   - `BobberController` with `BobberArcCaster` + `BobberButtonInput`
   - Bobber model instance (from `Assets/Models/bobber.fbx`) with added `SphereCollider` + `BobberIdleSway`
   - `Targeting` with `CursorCastTargeting`
   - `InputManager` object with `JslStickInput`, plus child objects for `JoyCon...` and `XboxFishingInput`
7. Wire references exactly (camera, water collider, bobber, rod tip, hang point, caster refs).
8. Set layer mask in `CursorCastTargeting` to include both `Water` and `WaterCast`.
9. In `PondManager`, assign fish prefabs and bobber object reference.
10. Ensure JoyShockLibrary native plugin is present if Joy-Con input is expected.

## 8. Known Editing Gotchas

- `PondManager.FishCaughtText` is currently null in scene. If fish catch logic triggers UI text without assignment, null reference risk exists.
- `PondManager.gameManager` is null and discovered at runtime (`FindObjectOfType<GameManager>()`).
- `RodLineVisualizer.waterSurface` is null, so water-clamp/surface heuristics are effectively off.
- The bobber is a stripped model prefab instance; some references point to stripped `fileID`s in scene YAML. This is expected for model-prefab-backed objects.
- Targeting depends on water collider bounds, so resizing or replacing `WaterCastTarget` changes castable area immediately.

## 9. Asset/Dependency Notes

Environment prefab source set is mostly:
- `Assets/SimpleNaturePack/Prefabs/*` (trees, rocks, grass, flowers, ground, stumps)
- `Assets/Models/rod.fbx` (rod mesh under rod rig)
- `Assets/Models/bobber.fbx` (bobber model instance)

Input dependencies:
- Unity Input System (`UnityEngine.InputSystem`) for Xbox script.
- JoyShockLibrary native DLL for Joy-Con scripts (`Jsl*` calls).

---

If you want this split into a shorter designer version and a deeper engineering version, duplicate this file and strip sections 5-8 for the designer copy.
