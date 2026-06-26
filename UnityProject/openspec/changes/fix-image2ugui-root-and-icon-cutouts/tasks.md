## 1. Root Mapping

- [x] [tdd] 1.1 Add `UiStructureConversionOptions` with `VirtualRoot`, `IncludeRootNode`, and `MapRootToPrefabRoot` root modes.
- [x] [tdd] 1.2 Update `UiStructureConverter` so Draft Workbench defaults to `VirtualRoot` and does not emit a descriptor for the JSON root.
- [x] [tdd] 1.3 Preserve JSON root bounds as the coordinate parent for direct children when using `VirtualRoot`.
- [x] [tdd] 1.4 Keep `IncludeRootNode` compatibility behavior covered by converter tests.
- [x] [tdd] 1.5 Preserve asset intent, alpha intent, padding, square-canvas, marker, sprite hint, and region id metadata on converted descriptors.

## 2. Prefab Apply And Cleanup

- [x] [tdd] 2.1 Update Draft Workbench preview/apply code to pass `VirtualRoot` conversion options for JSON-driven structure import.
- [x] [tdd] 2.2 Ensure applying JSON whose root is `LevelPreviewMenu` to a `RootCanvas` prefab does not create `RootCanvas/LevelPreviewMenu`.
- [x] [tdd] 2.3 Ensure `VirtualRoot` apply leaves the target prefab root name and root-level components unchanged.
- [x] [tdd] 2.4 Add recorded wrapper cleanup discovery using Draft Workbench metadata/apply records and top-level object paths.
- [x] [tdd] 2.5 Implement recorded wrapper cleanup execution and audit/report output, while leaving unrecorded candidates untouched.

## 3. Region Intent And Raw Crop

- [x] [tdd] 3.1 Extend region and visuals models with `assetKind`, `alphaMode`, padding, square-canvas, and `requiresTransparentAlpha`.
- [x] [tdd] 3.2 Implement asset-kind and alpha-mode inference from JSON fields first, then marker, sprite hint, node name, component type, and area heuristics.
- [x] [tdd] 3.3 Classify icons and decorations as `TransparentForeground` with transparent alpha validation required.
- [x] [tdd] 3.4 Classify backgrounds, preview images, panels, bars, frames, and button plates as `OpaqueRect` unless JSON explicitly overrides them.
- [x] [tdd] 3.5 Fix JSON-driven raw crop planning so icon and decoration crops expand by configured fixed and percent padding before clamping.
- [x] [tdd] 3.6 Add square transparent canvas output for transparent icon crops without changing original UI placement bounds.

## 4. Transparent Refinement

- [x] [tdd] 4.1 Replace transparent icon passthrough refinement with configurable BiRefNet/RMBG-style workflow selection.
- [x] [tdd] 4.2 Keep passthrough or cleanup refinement available only for `OpaqueRect` regions and report it as non-background-removal unless alpha proves otherwise.
- [x] [tdd] 4.3 Add Sam2 fallback workflow support that can pass JSON-derived bbox or center-point prompt geometry through the workflow adapter.
- [x] [tdd] 4.4 Record refinement attempts, selected workflow, fallback reason, raw path, refined path, dimensions, and failure reason per region.
- [x] [repl] 4.5 Run a local ComfyUI dry-run or adapter-level mock command for the configured transparent workflow and confirm request/response shape.

## 5. Alpha Gate And Sprite Import

- [x] [tdd] 5.1 Add `DraftAlphaValidationService` to compute min/max alpha, transparent ratio, border opaque ratio, and non-opaque pixel ratio.
- [x] [tdd] 5.2 Reject transparent icon outputs that contain no transparent pixels or exceed configured border contamination thresholds.
- [x] [tdd] 5.3 Mark rejected transparent outputs as `NeedsReview` or `Failed` and keep raw/refined cache paths available for inspection.
- [x] [tdd] 5.4 Prevent failed transparent outputs from being copied to `Assets/AssetRaw/Image/DraftWorkbenchGenerated/<PrefabName>/<SourceHash>/`.
- [x] [tdd] 5.5 Import only approved refined outputs as Single UI Sprites with transparency settings preserved.
- [x] [tdd] 5.6 Allow `OpaqueRect` regions to import without transparent alpha while still recording refinement status and dimensions.

## 6. UI, Reports, And Verification

- [ ] [manual] 6.1 Update Draft Workbench UI to show asset kind, alpha mode, refinement workflow, alpha validation status, and review/failure reasons.
- [x] [tdd] 6.2 Update generation reports to distinguish rectangular regions from transparent icon regions and include alpha statistics on failures.
- [ ] [doc] 6.3 Update Draft Workbench documentation with the `VirtualRoot` default, recorded wrapper cleanup rules, and transparent icon workflow requirements.
- [x] [repl] 6.4 Run the Unity compile check after code changes.
- [x] [repl] 6.5 Run the closest DraftWorkbench EditMode tests and add any missing focused tests before marking the change complete.
- [ ] [manual] 6.6 Manually verify `Assets/Test/TestImg.jpg` produces no root wrapper and that icon/decoration outputs either contain transparency or are blocked for review.
