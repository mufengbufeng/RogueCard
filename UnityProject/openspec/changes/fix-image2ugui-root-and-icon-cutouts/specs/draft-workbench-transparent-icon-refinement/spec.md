## ADDED Requirements

### Requirement: Asset-kind aware JSON crop planning

The Draft Workbench SHALL classify JSON-driven visual regions by asset intent before raw crop generation.

#### Scenario: Icon region receives transparent foreground intent
- **WHEN** a JSON-driven region name, marker, or sprite hint identifies an icon or decoration
- **THEN** the workbench SHALL mark the region as `TransparentForeground`
- **AND** the region SHALL require transparent alpha validation before final Sprite import
- **AND** the region SHALL use icon/decoration padding rules rather than raw bbox-only cropping

#### Scenario: Background and panel regions remain rectangular
- **WHEN** a JSON-driven region represents a background, level preview image, panel, bar, frame, or button plate
- **THEN** the workbench SHALL mark the region as `OpaqueRect` unless JSON explicitly requests transparent foreground output
- **AND** the region SHALL NOT require transparent alpha validation

#### Scenario: JSON-provided intent overrides heuristics
- **WHEN** the JSON includes explicit asset kind, alpha mode, crop padding, or square-canvas metadata
- **THEN** the workbench SHALL prefer those values over name or spriteHint heuristics
- **AND** invalid values SHALL be ignored with a readable conversion warning

### Requirement: Padded raw crop generation for transparent icons

The Draft Workbench SHALL expand icon and decoration crops beyond their JSON source bounds before ComfyUI refinement.

#### Scenario: Icon crop includes configured padding
- **WHEN** an included icon or decoration region has valid source bounds
- **THEN** the raw crop bounds SHALL include configured fixed and percent padding
- **AND** the expanded crop SHALL be clamped to the source image bounds
- **AND** the region record SHALL preserve both original source bounds and expanded crop bounds

#### Scenario: Icon crop may be placed on transparent square canvas
- **WHEN** a transparent icon region is configured to use square output
- **THEN** the raw crop output SHALL be placed on a square transparent canvas without changing the original UI placement bounds
- **AND** the region record SHALL retain enough offset metadata to explain the added transparent padding

### Requirement: Configurable transparent background workflow

The Draft Workbench SHALL use a configurable ComfyUI workflow for transparent icon refinement rather than passthrough image saving.

#### Scenario: BiRefNet workflow is used as default transparent refinement
- **WHEN** the user starts refinement for a `TransparentForeground` region
- **THEN** the workbench SHALL submit the raw crop to the configured transparent background workflow
- **AND** the default transparent workflow SHALL be treated as a BiRefNet/RMBG-style background removal pass
- **AND** the resulting refined PNG SHALL be expected to contain an alpha channel

#### Scenario: Sam2 fallback uses JSON-derived prompt geometry
- **WHEN** the default transparent workflow fails alpha validation and a Sam2 fallback workflow is configured
- **THEN** the workbench SHALL submit the same raw crop to the Sam2 workflow
- **AND** the request SHALL provide JSON-derived bbox or center-point prompt data when supported by the workflow adapter
- **AND** the fallback output SHALL be validated before final Sprite import

#### Scenario: Rectangular assets may use passthrough refinement
- **WHEN** a region is marked `OpaqueRect`
- **THEN** the workbench MAY use passthrough or cleanup refinement
- **AND** the workbench SHALL NOT describe that result as background-removed unless alpha validation confirms transparency was produced

### Requirement: Alpha validation gate

The Draft Workbench SHALL validate alpha quality before importing transparent icon outputs as final Sprite assets.

#### Scenario: Opaque output is rejected for transparent icon
- **WHEN** a `TransparentForeground` refined PNG contains no transparent pixels
- **THEN** the region SHALL be marked as failed or requiring review
- **AND** the workbench SHALL NOT copy that PNG to the final Sprite output directory
- **AND** the report SHALL explain that the background removal workflow produced an opaque result

#### Scenario: Border contamination requires review
- **WHEN** a `TransparentForeground` refined PNG has excessive opaque pixels on its border
- **THEN** the region SHALL be marked as requiring review
- **AND** the report SHALL include alpha statistics that justify the review state

#### Scenario: Rectangular region bypasses alpha gate
- **WHEN** a region is marked `OpaqueRect`
- **THEN** the workbench SHALL allow final Sprite import without transparent alpha
- **AND** the generated Sprite record SHALL still include refinement status and output dimensions

### Requirement: Final Sprite import only consumes approved refined outputs

The Draft Workbench SHALL import only successful or explicitly approved refined outputs into the final Unity Sprite asset directory.

#### Scenario: Valid transparent icon imports as UI Sprite
- **WHEN** a `TransparentForeground` refined PNG passes alpha validation
- **THEN** the workbench SHALL copy it under `Assets/AssetRaw/Image/DraftWorkbenchGenerated/<PrefabName>/<SourceHash>/`
- **AND** the workbench SHALL import it as a Single UI Sprite with transparency settings preserved
- **AND** the generated Sprite record SHALL include alpha validation status

#### Scenario: Failed transparent icon remains in cache
- **WHEN** a transparent icon refinement fails or does not pass alpha validation
- **THEN** raw and refined PNG paths SHALL remain available in cache for inspection
- **AND** no final Sprite asset SHALL be created for that failed output unless the user explicitly overrides review
