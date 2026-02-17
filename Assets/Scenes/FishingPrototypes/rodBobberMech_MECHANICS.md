# rodBobberMech Rod + Bobber Mechanics

Scene reference: `Assets/Scenes/FishingPrototypes/rodBobberMech.unity`

## Core flow

1. `CursorCastTargeting` moves `CastMarker` on the water surface.
2. `BobberArcCaster.Cast()` moves bobber from hang point to marker in an arc.
3. `BobberArcCaster` enters `Landed`, so bobber water motion is applied.
4. `BobberArcCaster.Yank()` retracts bobber to hang point.
5. `RodLineVisualizer` continuously draws line from rod tip to bobber, changing sag/sway by motion/state.

## Key scripts

| Script | File | Typical host object | Role |
|---|---|---|---|
| `BobberArcCaster` | `Assets/Scripts/Fishing/BobberArcCaster.cs` | `BobberController` | Cast/yank state machine and bobber movement. |
| `RodLineVisualizer` | `Assets/Scripts/Fishing/RodLineVisualizer.cs` | `RodRig` | Visual fishing line (sag, sway, water clamp). |
| `BobberIdleSway` | `Assets/Scripts/Fishing/BobberIdleSway.cs` | Bobber object | Idle hanging sway and landed water drift. |
| `CursorCastTargeting` | `Assets/Scripts/Fishing/CursorCastTargeting.cs` | `Targeting` | Computes cast target from mouse or Joy-Con stick. |
| `BobberButtonInput` | `Assets/Scripts/Fishing/BobberButtonInput.cs` | `BobberController` | Keyboard test trigger (`C` cast, `Y` yank). |
| `JslStickInput` | `Assets/Scripts/Input/JslStickInput.cs` | `InputManager` | Joy-Con stick data source for targeting. |
| `JoyConGestureDetector` | `Assets/Scripts/Input/JoyConGestureDetector.cs` | `JoyConGestures` | Gesture-triggered cast/yank events. |

## Required GameObjects and hierarchy

Use this minimal structure in a new scene:

1. `RodRig`
2. `RodTip` (child of `RodRig`)
3. `BobberHangPoint` (child of `RodTip`)
4. `BobberController`
5. `Bobber`
6. `CastMarker`
7. `Targeting`
8. `Water`
9. `Main Camera`

## Required component wiring

### `RodRig`
- Components: `LineRenderer`, `RodLineVisualizer`
- `RodLineVisualizer.rodTip` -> `RodTip`
- `RodLineVisualizer.bobber` -> `Bobber` transform
- `RodLineVisualizer.waterSurface` -> `Water` transform

### `BobberController`
- Component: `BobberArcCaster`
- Optional component: `BobberButtonInput`
- `BobberArcCaster.rodTip` -> `RodTip`
- `BobberArcCaster.bobber` -> `Bobber` transform
- `BobberArcCaster.bobberHangPoint` -> `BobberHangPoint`
- `BobberArcCaster.targetMarker` -> `CastMarker`
- If using keyboard testing: `BobberButtonInput.caster` -> `BobberArcCaster`

### `Bobber`
- Component: `BobberIdleSway`
- `BobberIdleSway.hangPoint` -> `BobberHangPoint`
- `BobberIdleSway.bobberArcCaster` -> `BobberArcCaster`
- Add visual mesh/collider as needed

### `Targeting`
- Component: `CursorCastTargeting`
- `cam` -> `Main Camera`
- `castMarker` -> `CastMarker`
- `waterCollider` -> collider on `Water`
- `waterMask` must include water layer
- `jslInput` only needed when `useJoyCon = true`

### `Water`
- Must have collider (MeshCollider or BoxCollider)
- Must be on a layer included by `CursorCastTargeting.waterMask`
- In `rodBobberMech`, water is layer index `4` (mask bit `16`)

## Starter values from rodBobberMech

Use these to match current feel quickly:

- `BobberArcCaster.castDuration = 0.75`
- `BobberArcCaster.arcHeight = 3`
- `BobberArcCaster.yankDuration = 0.25`
- `RodLineVisualizer.segments = 20`
- `RodLineVisualizer.slack = 0.08`
- `RodLineVisualizer.lineSwayAmplitude = 0.04`
- `RodLineVisualizer.lineSwaySpeed = 0.7`
- `CursorCastTargeting.maxDistance = 30`

## Input options

### Mouse + keyboard (currently active in this scene)
- `CursorCastTargeting.useJoyCon = false`
- Hold right mouse to position target
- `BobberButtonInput`: `C` cast, `Y` yank

### Joy-Con
- Add `InputManager` with `JslStickInput`
- Set `CursorCastTargeting.useJoyCon = true`
- Set `CursorCastTargeting.jslInput` to the `JslStickInput` component
- Add `JoyConGestures` with `JoyConGestureDetector`
- Wire `onCast` -> `BobberArcCaster.Cast`
- Wire `onYank` -> `BobberArcCaster.Yank`

## Quick recreate checklist

1. Build object hierarchy (`RodRig` -> `RodTip` -> `BobberHangPoint`).
2. Add bobber object with `BobberIdleSway`.
3. Add `BobberController` with `BobberArcCaster` and wire four transform refs.
4. Add `LineRenderer` + `RodLineVisualizer` on `RodRig` and wire refs.
5. Add `CastMarker` and `Targeting` with `CursorCastTargeting`.
6. Ensure water collider + layer mask are correct.
7. Choose one input path (keyboard or Joy-Con).
8. Test transitions: idle -> cast -> landed -> yank.

## Scene-specific note

`rodBobberMech` includes an inactive legacy `Bobber` object; the active bobber used by `BobberArcCaster` is the bobber prefab instance reference.

## Alpha Demo integration guidance (no code changes required)

Yes, you can bring these mechanics into `Assets/Scenes/AlphaDemo/Pond_Level_1.unity`.

### Main things to watch out for

1. Water setup
- `CursorCastTargeting` needs a valid `waterCollider`.
- `CursorCastTargeting.waterMask` must include the layer used by your Alpha Demo water.

2. Camera reference
- `CursorCastTargeting.cam` must point to the active gameplay camera in Alpha Demo.

3. Input conflicts
- If you keep keyboard test input (`BobberButtonInput`), make sure `C`/`Y` do not conflict with existing controls.
- If using Joy-Con, only then enable `useJoyCon` and wire `JslStickInput`.

4. Reference breakage after copy/paste
- Recheck all transform refs after duplicating objects between scenes:
- `rodTip`, `bobber`, `bobberHangPoint`, `targetMarker`, `waterSurface`, `waterCollider`.

5. Scale and tuning mismatch
- New environment scale/water height may require retuning:
- `arcHeight`, `castDuration`, `yankDuration`, line `slack`, sway amplitudes/speeds, and targeting `maxDistance`.

6. Manager/system duplication
- If you also import prototype manager objects, avoid duplicate managers/systems in one scene (for example duplicate `EventSystem` or overlapping gameplay managers).

### Suggested migration order (lowest risk)

1. Bring only core rod mechanics first (`RodRig`, `RodTip`, `BobberHangPoint`, `BobberController`, `Bobber`, `CastMarker`, `Targeting`).
2. Wire all references and confirm idle/cast/yank behavior works.
3. Verify water raycast and line rendering in Alpha Demo's water area.
4. Tune movement/sway values for Alpha Demo scale.
5. Add optional systems (fish/pond managers, Joy-Con gesture path) after core behavior is stable.



