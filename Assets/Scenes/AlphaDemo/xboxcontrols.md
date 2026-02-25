# Pond_Level_1 Controls (Current) + Xbox Plan

Scene: `Assets/Scenes/AlphaDemo/Pond_Level_1.unity`

## Current control state

## Mouse + keyboard

Aiming / cast target:
- Script: `Assets/Scripts/Fishing/CursorCastTargeting.cs`
- In this scene, targeting updates from mouse when right mouse is held.
- Targeting raycasts against `waterMask = 144` (layers `Water` and `WaterCast`).
- Active target collider reference is `WaterCastTarget` (`waterCollider`).

Fishing actions:
- Script: `Assets/Scripts/Fishing/BobberButtonInput.cs`
- Wired on `BobberController`.
- Keys currently set in scene:
- `UpArrow` = cast
- `DownArrow` = yank
- `H` = toggle tension

Tension directional swing (keyboard):
- Script: `Assets/Scripts/Fishing/BobberArcCaster.cs`
- Keys in scene:
- `W` = swing up
- `A` = swing left
- `D` = swing right
- Only meaningful during `Tension` state.

## Joy-Con

Stick targeting:
- Script: `Assets/Scripts/Input/JslStickInput.cs` (on `InputManager`).
- In scene:
- `useAnyConnectedDevice = true`
- `autoDetectStickSide = true`
- `deviceIndex = 0` fallback
- `CursorCastTargeting` reads this stick input when mouse is not actively driving target.

Cast / yank gestures:
- Script: `Assets/Scripts/Input/JoyConGestureDetector.cs` (on `JoyConFishingMotion`).
- In scene:
- `useAnyConnectedDevice = true`
- `autoMirrorForOtherHand = false`
- cast/yank thresholds are reduced (`lin=0.4`, `gyro=160`) with short timing windows (`0.1s`).
- `onCast` and `onYank` are wired to `BobberArcCaster.Cast` / `BobberArcCaster.Yank`.

Directional swing from motion:
- Script: `Assets/Scripts/Input/JoyConDirectionalSwingInput.cs` (same object).
- In scene:
- `onlyWhenTensionState = true`
- `deviceIndex = 0`
- Drives `BobberArcCaster.SetDirectionalSwingHeld(up,left,right)`.

## Important current behavior notes

1. Mouse and Joy-Con stick are both supported in the same scene.
2. Mouse wins only while right mouse is actively held; otherwise non-zero Joy-Con stick can move the cast target.
3. `JoyConGestureDetector.autoMirrorForOtherHand` is currently `false` in scene data.
4. `JoyConDirectionalSwingInput` currently reads one Joy-Con handle (`deviceIndex`), not both.

## What we need to add Xbox controls

There is currently no Xbox/Gamepad input script in `Assets/Scripts`.

## Recommended implementation path

1. Create a new script, for example `XboxFishingInput`.
2. Read gamepad input via Unity Input System (`UnityEngine.InputSystem.Gamepad`).
3. Wire it to existing fishing APIs instead of rewriting mechanics:
- Trigger `BobberArcCaster.Cast()`
- Trigger `BobberArcCaster.Yank()`
- Trigger `BobberArcCaster.ToggleTension()`
- Feed `BobberArcCaster.SetDirectionalSwingHeld(up,left,right)` for tension swing
- Feed cast targeting stick into `CursorCastTargeting` (or extend `CursorCastTargeting` to accept gamepad stick source)

## Suggested Xbox mapping (initial)

- Left stick: move cast marker on water (same role as Joy-Con stick)
- `RT` press: cast
- `LT` press: yank
- `A`: toggle tension
- D-pad up/left/right (or right stick cardinal): directional tension swing

## Scene wiring needed after script exists

1. Add new GameObject (for example `XboxInputManager`) in `Pond_Level_1`.
2. Add `XboxFishingInput` component.
3. Assign references:
- `BobberArcCaster` (`BobberController`)
- `CursorCastTargeting` (`Targeting`)
4. Ensure Input System package is available and active (project currently uses `activeInputHandler: 2`, so this is already compatible).

## Potential pitfalls

1. Duplicate triggers if keyboard/Joy-Con/Xbox all fire same actions simultaneously.
2. Stick deadzone tuning (avoid slow drift of cast marker).
3. Trigger threshold tuning (avoid accidental cast/yank from analog trigger noise).
4. Priority rules when mouse, Joy-Con stick, and Xbox stick all provide input in same frame.

## Validation checklist for Xbox

1. Move cast marker with Xbox stick only.
2. Cast/yank work from Xbox only.
3. Tension toggle + directional swing work from Xbox only.
4. Mixed-input behavior is predictable (mouse + Joy-Con + Xbox together).
5. No action spam from held buttons/triggers (edge-triggered where intended).
