## ADDED Requirements

### Requirement: JSON-driven Auto Slice workbench integration

The draft workbench SHALL integrate JSON-driven slicing as an Editor-only workflow alongside AI structure generation, Parse & Preview, and regular Prefab apply.

#### Scenario: User starts JSON-driven slicing from Draft Workbench
- **WHEN** a user has selected a design image and has editable `ui_structure.json` or current preview descriptors
- **THEN** the workbench SHALL allow the user to generate raw PNG crops from the JSON-derived UI structure
- **AND** the workbench SHALL NOT require a target prefab for raw crop generation, ComfyUI refinement, or Sprite asset import
- **AND** the workbench SHALL require a target prefab only before prefab matching or applying Sprite references

#### Scenario: Existing AI structure workflow remains available
- **WHEN** a user uses AI Vision generation, Parse & Preview, structure overlay, or regular Apply to Prefab
- **THEN** those workflows SHALL remain usable without requiring ComfyUI configuration
- **AND** JSON-driven slicing SHALL NOT replace or block the existing structure generation workflow

#### Scenario: Workbench preview shows JSON raw crop regions
- **WHEN** JSON-driven slicing regions have valid source bounds
- **THEN** the workbench SHALL overlay region boxes on the design image preview
- **AND** the overlay SHALL distinguish included, ignored, selected, and warning states

### Requirement: Auto Slice generation metadata and report

The draft workbench SHALL record enough editor-only metadata to audit JSON-driven raw crop generation, ComfyUI refinement, and Sprite import results.

#### Scenario: Metadata records raw and refined generation
- **WHEN** the user generates raw crops or refined Sprite assets
- **THEN** the workbench SHALL record source image reference, source hash, JSON or descriptor source, region ids, markers, raw PNG paths, refined PNG paths, generated Sprite paths, generated Sprite GUIDs, and generation statuses
- **AND** this metadata SHALL remain Editor-only and SHALL NOT require inclusion in YooAsset runtime bundles

#### Scenario: Report lists asset generation without prefab modification
- **WHEN** the user completes raw crop, refinement, or Sprite import without applying to a prefab
- **THEN** the workbench SHALL report generated asset counts, output folders, failed regions, skipped regions, and whether a prefab was modified
- **AND** the report SHALL explicitly state when no prefab changes were made

#### Scenario: Generation state survives ComfyUI failure
- **WHEN** ComfyUI refinement fails after raw PNG files were generated
- **THEN** the workbench SHALL preserve the raw crop records and review selections
- **AND** the report SHALL show the ComfyUI error separately from raw crop success
