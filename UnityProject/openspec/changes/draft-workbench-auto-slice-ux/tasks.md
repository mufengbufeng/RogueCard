## 1. Auto Slice state model and configuration

- [ ] [tdd] 1.1 Add Editor-only Auto Slice state/data models for stage, region review state, generated Sprite records, Sprite assignment records, and Sprite apply records.
- [ ] [tdd] 1.2 Add `ComfyUiServiceConfig` persistence for URL, workflow file, detection preset/prompt, threshold, refine iterations, individual masks, bbox expansion, output root, and cache root.
- [ ] [repl] 1.3 Verify the default config asset is created and reloads through Unity AssetDatabase without entering runtime assemblies.

## 2. ComfyUI and SAM3 workflow adapter

- [ ] [repl] 2.1 Implement ComfyUI connection check against `/system_stats` with readable failure messages for unreachable URLs.
- [ ] [tdd] 2.2 Implement workflow inspection that can identify or validate LoadImage, SAM3/subgraph, preview/output, and configurable node IDs from a ComfyUI frontend workflow JSON.
- [ ] [tdd] 2.3 Implement workflow patching for uploaded image filename/path, SAM3 prompt/preset, threshold, refine iterations, individual masks, and output prefix.
- [ ] [repl] 2.4 Verify the adapter can process `E:/Documents/ComfyUI/user/default/workflows/Sam3.json` and report the detected nodes or actionable missing outputs.

## 3. Region detection and review

- [ ] [tdd] 3.1 Convert ComfyUI manifest output or mask-only output into normalized Auto Slice region records with id, marker, label, confidence, bbox, mask path, source hash, and include/review state.
- [ ] [tdd] 3.2 Add Unity-side mask alpha bbox calculation fallback for workflows that do not emit manifest JSON.
- [ ] [manual] 3.3 Redesign the Draft Workbench Auto Slice panel into a step-based UX with status card, primary next action, advanced settings, region list, and region detail area.
- [ ] [manual] 3.4 Render detected region overlays on the design image preview with color/style states for selected, ignored, high-confidence, low-confidence, and current selection.
- [ ] [manual] 3.5 Allow users to include/ignore regions and edit markers before Sprite generation.

## 4. Sprite generation

- [ ] [tdd] 4.1 Implement region crop generation from source image plus bbox and optional mask alpha, including pixel/percentage bbox expansion and clamping.
- [ ] [tdd] 4.2 Import generated PNG files as UI Sprite assets under `Assets/AssetRaw/Image/DraftWorkbenchGenerated/<PrefabName>/<SourceHash>/`.
- [ ] [repl] 4.3 Verify generated PNG/Sprite assets appear in Unity AssetDatabase and intermediate ComfyUI files remain under `Library/DraftWorkbench/ComfyCache/<SourceHash>/`.
- [ ] [manual] 4.4 Show generated Sprite count, output folder, and per-region generation results in the Auto Slice UI.

## 5. Prefab matching review

- [ ] [tdd] 5.1 Extend `UiStructureSchema`, `UiStructureConverter`, and descriptor visuals to preserve `asset`, `marker`, `spriteHint`, and `regionId` semantics.
- [ ] [tdd] 5.2 Implement Sprite-to-Image-node candidate matching using marker/spriteHint exact match, bbox IoU, and node-name token overlap.
- [ ] [manual] 5.3 Add assignment review UI showing target node path, old Sprite, suggested Sprite, confidence, match reason, and review/approval state.
- [ ] [tdd] 5.4 Ensure assignments targeting existing Sprite references or low-confidence matches require user approval before apply.

## 6. Safe apply, report, and revert

- [ ] [tdd] 6.1 Apply only approved Sprite assignments to prefab `Image.sprite` references and skip unapproved assignments.
- [ ] [tdd] 6.2 Extend `DraftApplyRecord`, metadata, and maintenance JSON snapshots with old/new Sprite GUID/path, target object path, region id, and marker.
- [ ] [tdd] 6.3 Extend revert to restore recorded old Sprite references and detect conflicts if a Sprite was manually changed after apply.
- [ ] [manual] 6.4 Add apply confirmation and completion summaries that clearly state modified Sprite references, skipped assignments, overwrite risk, and generated assets retained.

## 7. Verification and documentation

- [ ] [repl] 7.1 Run Unity compile verification through AIBridge `compile unity` and confirm Console has no errors.
- [ ] [repl] 7.2 Run `dotnet build UnityProject.slnx --no-restore` as a secondary compile signal and record any non-blocking warnings.
- [ ] [manual] 7.3 Perform an end-to-end manual test with ComfyUI running: select design image, run detection, review regions, generate Sprites, match prefab nodes, apply approved assignments, and revert.
- [ ] [doc] 7.4 Document the expected ComfyUI/SAM3 workflow contract, supported fallback behavior, output directories, and troubleshooting steps for connection/workflow failures.
