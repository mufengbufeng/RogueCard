## ADDED Requirements

### Requirement: JSON-driven region extraction

The Draft Workbench SHALL use `ui_structure.json` or converted preview descriptors as the default source for Auto Slice regions.

#### Scenario: Extract regions from preview descriptors
- **WHEN** a user has parsed or generated `ui_structure.json` and preview descriptors are available
- **THEN** the workbench SHALL create Auto Slice region records from eligible descriptors with valid source bounds
- **AND** each region SHALL include a stable region id, marker, node path, source bounds, source canvas size, include state, and review state
- **AND** ComfyUI SHALL NOT be required for this extraction step

#### Scenario: Extract regions from editable JSON without an existing preview
- **WHEN** a user has a design image and editable `ui_structure.json` text but no current preview descriptors
- **THEN** the workbench SHALL convert the JSON through the existing UI structure converter before extracting regions
- **AND** invalid JSON SHALL produce a readable error without creating raw PNG files

#### Scenario: Eligible visual elements are selected
- **WHEN** descriptors contain a mix of image, button, rect, text, container, overlay, or unknown elements
- **THEN** the workbench SHALL include descriptors that map to `UnityEngine.UI.Image` and have valid source bounds by default
- **AND** the workbench SHALL skip text-only, layout-only, unknown, or boundless descriptors by default
- **AND** skipped descriptors SHALL be reported with a reason

#### Scenario: Source canvas differs from texture size
- **WHEN** descriptor source bounds are expressed in a source canvas size that differs from the selected design image pixel size
- **THEN** the workbench SHALL scale source bounds into texture pixel coordinates before cropping
- **AND** the workbench SHALL report the applied scale
- **AND** aspect ratio mismatch SHALL be marked as requiring review

### Requirement: Raw PNG generation from JSON regions

The Draft Workbench SHALL generate raw PNG crops from selected JSON-driven regions using Unity Editor scripts before invoking ComfyUI.

#### Scenario: Generate raw PNG files
- **WHEN** the user confirms selected JSON-driven regions
- **THEN** the workbench SHALL crop each included region from the selected design image using the region pixel bounds
- **AND** the workbench SHALL write one raw PNG per region to a workbench cache directory
- **AND** each raw PNG record SHALL preserve region id, marker, node path, original bounds, crop bounds, raw path, and generation status

#### Scenario: Raw generation works without ComfyUI
- **WHEN** ComfyUI is not configured, unreachable, or disabled
- **THEN** raw PNG generation SHALL still be available
- **AND** existing raw region review data SHALL NOT be cleared by a ComfyUI connection failure

#### Scenario: User reviews raw regions before refinement
- **WHEN** raw regions or raw PNG files are available
- **THEN** the user SHALL be able to include or ignore each region before ComfyUI refinement
- **AND** marker edits SHALL update the generated output naming used by later refined PNG and Sprite import steps

### Requirement: ComfyUI crop refinement

The Draft Workbench SHALL send selected raw PNG crops to ComfyUI as refinement inputs and SHALL NOT use ComfyUI as the default bbox detector.

#### Scenario: Refine selected raw crops
- **WHEN** the user starts ComfyUI refinement for selected raw crops
- **THEN** the workbench SHALL upload or reference each raw PNG as an individual ComfyUI input
- **AND** the workbench SHALL request a refined PNG output for each selected raw crop
- **AND** each refinement result SHALL record raw path, refined path, source region id, marker, output size, and status

#### Scenario: ComfyUI does not decide region bounds by default
- **WHEN** the default JSON-driven Auto Slice workflow runs
- **THEN** ComfyUI SHALL NOT be invoked before JSON region extraction or raw PNG crop generation
- **AND** any ComfyUI output SHALL be associated with an existing JSON-driven region

#### Scenario: Refinement output size differs from raw crop
- **WHEN** a refined PNG has a different width or height than its raw PNG input
- **THEN** the workbench SHALL keep the original JSON region bounds as the UI placement source
- **AND** the refinement record SHALL be marked as requiring review

#### Scenario: Refinement fails for some crops
- **WHEN** ComfyUI fails for one or more raw crops
- **THEN** the workbench SHALL keep successful refinement results
- **AND** failed crops SHALL retain their raw PNG records and readable error messages
- **AND** the workbench SHALL NOT delete generated raw PNG files

### Requirement: Sprite import from refined PNG

The Draft Workbench SHALL import refined PNG outputs as Unity UI Sprite assets.

#### Scenario: Import refined PNG as UI Sprite
- **WHEN** a refined PNG exists for a selected region
- **THEN** the workbench SHALL write or move the final PNG under `Assets/AssetRaw/Image/DraftWorkbenchGenerated/<PrefabName>/<SourceHash>/`
- **AND** the workbench SHALL import it as a Single Sprite suitable for UGUI Image usage
- **AND** the generated Sprite record SHALL include region id, marker, source bounds, refined path, Sprite asset path, and Sprite GUID

#### Scenario: Asset-only generation does not require a target prefab
- **WHEN** the user generates raw and refined Sprite assets without selecting a target prefab
- **THEN** the workbench SHALL complete Sprite asset generation
- **AND** the workbench SHALL report that no prefab was modified

#### Scenario: Prefab matching remains review-based
- **WHEN** generated refined Sprites and preview descriptors are available
- **THEN** the workbench MAY create Sprite-to-Image assignment candidates using existing matching services
- **AND** applying assignments SHALL still require approved matches before prefab Sprite references are modified

### Requirement: Two-stage Auto Slice workflow state

The Draft Workbench SHALL expose Auto Slice as a two-stage workflow: JSON raw crop generation followed by ComfyUI refinement and Sprite import.

#### Scenario: Workbench displays JSON raw crop stage
- **WHEN** a design image and JSON or preview descriptors are available
- **THEN** the workbench SHALL display a primary action for generating raw PNG crops from JSON
- **AND** the action SHALL be enabled without a ComfyUI connection

#### Scenario: Workbench displays ComfyUI refinement stage
- **WHEN** selected raw PNG crops are available
- **THEN** the workbench SHALL display a primary action for generating refined PNG outputs through ComfyUI
- **AND** the workbench SHALL show connection or workflow errors without clearing raw crop state

#### Scenario: Advanced whole-image detection is not the default path
- **WHEN** an advanced SAM3 or manifest/mask whole-image detection capability exists
- **THEN** it SHALL be visually separated from the default JSON-driven workflow
- **AND** the default primary action SHALL NOT run whole-image SAM3 detection before raw crop generation
