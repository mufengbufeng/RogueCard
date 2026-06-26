## 1. JSON-driven region extraction

- [x] [tdd] 1.1 Extend Editor-only Auto Slice data models to record JSON region source kind, node path, source canvas size, source bounds, scaled crop bounds, raw PNG path, refined PNG path, Sprite asset path, review state, and generation status.
- [x] [tdd] 1.2 Implement JSON-driven region extraction from current `UguiNodeChange` / `UguiNodeDescriptor` preview data, including eligible Image-component filtering, skipped-node reasons, marker/spriteHint/regionId preservation, and stable region ids.
- [x] [tdd] 1.3 Implement extraction fallback from editable `ui_structure.json` by running the existing converter when preview descriptors are not available, with readable errors for invalid JSON.
- [x] [tdd] 1.4 Implement source-canvas-to-texture pixel coordinate scaling, including aspect-ratio mismatch warnings and requires-review marking.
- [x] [tdd] 1.5 Extend `UiStructureSchema` / `UiStructureConverter` tests to cover `asset`、`marker`、`spriteHint`、`regionId` and descriptor source canvas/bounds preservation for JSON-driven slicing.

## 2. Raw PNG crop generation

- [x] [tdd] 2.1 Implement a raw crop generation service that crops selected JSON-driven regions from the design image into one raw PNG per region without invoking ComfyUI.
- [x] [tdd] 2.2 Ensure raw crop generation records raw path, original bounds, scaled crop bounds, marker, node path, success/failure status, and skipped-region reasons.
- [x] [tdd] 2.3 Ensure raw crop generation remains available when ComfyUI is unreachable and does not clear existing region review data on ComfyUI failures.
- [x] [tdd] 2.4 Add tests for nested/overlapping descriptors, text-only/layout-only skipped descriptors, invalid bounds, and marker edits affecting downstream output names.
- [x] [repl] 2.5 Verify raw PNG files are written under the configured workbench cache directory and do not appear as final Unity project Sprite assets.

## 3. ComfyUI refinement from raw crops

- [x] [tdd] 3.1 Add a ComfyUI crop refinement runner that accepts a raw PNG input and produces a refined PNG output associated with the same JSON-driven region.
- [x] [tdd] 3.2 Separate ComfyUI refinement mode from existing whole-image SAM3 detection mode so the default workflow never calls ComfyUI before JSON region extraction and raw crop generation.
- [x] [tdd] 3.3 Record refinement status per crop, including raw path, refined path, output dimensions, source region id, marker, and readable error messages.
- [x] [tdd] 3.4 Mark refined outputs as requiring review when output dimensions differ from the raw crop while preserving original region bounds for UI placement.
- [x] [repl] 3.5 Verify configured ComfyUI refinement workflow can process at least one raw crop and save its refined output to the expected cache/final-output location.

## 4. Sprite import and existing matching integration

- [x] [tdd] 4.1 Import refined PNG outputs as Single UI Sprite assets under `Assets/AssetRaw/Image/DraftWorkbenchGenerated/<PrefabName>/<SourceHash>/`.
- [x] [tdd] 4.2 Ensure generated Sprite records include region id, marker, source bounds, refined path, Sprite asset path, Sprite GUID, and generation status.
- [x] [tdd] 4.3 Support asset-only generation without a target prefab and report explicitly that no prefab was modified.
- [x] [tdd] 4.4 Reuse existing Sprite assignment matching for generated refined Sprites without changing the approval-before-apply safety behavior.
- [x] [tdd] 4.5 Ensure generated raw/refined/Sprite records are captured in Draft Workbench editor-only metadata or maintenance reports.

## 5. Draft Workbench UI and UX

- [ ] [manual] 5.1 Update the Auto Slice panel to expose two clear stages: “根据 JSON 生成原始切图” and “使用 ComfyUI 生成可用切图”.
- [ ] [manual] 5.2 Show JSON-driven region overlays on the design preview with included, ignored, selected, warning, and failed states.
- [ ] [manual] 5.3 Add region review UI for include/ignore, marker edits, raw crop status, refined output status, and skipped-node reasons.
- [ ] [manual] 5.4 Visually separate any advanced whole-image SAM3/manifest detection entry from the default JSON-driven workflow, or keep it hidden if not ready for MVP.
- [ ] [manual] 5.5 Update status cards, primary-action labels, help boxes, and error copy so ComfyUI failure is shown as a refinement problem rather than a raw crop problem.

## 6. Verification and documentation

- [x] [tdd] 6.1 Add or update EditMode tests for JSON-driven extraction, raw crop generation, coordinate scaling, refinement record handling, Sprite import, and existing assignment integration.
- [x] [repl] 6.2 Run Unity compile verification using the project compile-check workflow or AIBridge when Unity is open.
- [x] [repl] 6.3 Run `dotnet build UnityProject.slnx --no-restore` as a secondary compile signal when applicable.
- [ ] [manual] 6.4 Perform an end-to-end Editor test: select design image, parse JSON, generate raw PNG crops, review regions, run ComfyUI refinement, import Sprites, and confirm no prefab is modified unless matching/apply is explicitly invoked.
- [ ] [doc] 6.5 Document the JSON-driven slicing contract, eligible-node rules, raw/refined output directories, ComfyUI refinement workflow expectations, and troubleshooting steps.
