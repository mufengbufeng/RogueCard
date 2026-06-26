## Why

Designing UGUI screens from a reference image is currently a manual, error-prone process: developers have to interpret layout, create prefab hierarchy by hand, and then wire references into scripts after the fact. A small in-editor workbench can turn sketches or screenshots into editable UGUI draft prefabs, reducing iteration time while keeping generated output reviewable and under developer control.

## What Changes

- Add an editor-only image-to-UGUI draft workbench that accepts a local reference image and produces a draft UGUI prefab hierarchy.
- Generate draft RectTransform layout, visual placeholder components, labels, and binder-friendly object names suitable for later refinement in Unity.
- Support a preview/review step before writing assets so users can inspect the generated hierarchy and choose an output prefab path.
- Integrate generated drafts with the existing UGUI prefab composition and binding conventions instead of introducing a separate runtime UI framework.
- Keep generation deterministic for the same image analysis payload and settings; any optional AI/image interpretation remains an editor-time input, not runtime behavior.

## Capabilities

### New Capabilities
- `image-to-ugui-draft-workbench`: Covers editor-time conversion of a reference image into an editable UGUI draft prefab and related review workflow.

### Modified Capabilities
- `gameview-ugui-prefab-composition`: Generated draft prefabs must conform to existing GameView/UGUI prefab composition conventions when targeting game views.
- `auto-bind-ui-script`: Generated object names and optional binding metadata must be compatible with the existing automatic UI script binding workflow.

## Impact

- Affects Unity editor tooling under `Assets/GameScripts/Editor` and/or `Assets/EF/EFEditor/Editor`.
- Produces editor-generated prefab assets under the project asset tree, likely in an explicit draft/output folder chosen by the user.
- May add editor-only model classes for image layout analysis results, UGUI hierarchy generation, and workbench state.
- Does not change runtime UI loading behavior, battle logic, or player-facing gameplay systems.
