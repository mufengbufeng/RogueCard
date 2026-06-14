## ADDED Requirements

### Requirement: Auto Slice step workflow

The Draft Workbench SHALL provide an Editor-only Auto Slice workflow that guides users through design image input, ComfyUI/SAM3 detection, region review, Sprite generation, prefab matching, and safe application.

#### Scenario: User starts Auto Slice from selected inputs
- **WHEN** a user has selected a design image in Draft Workbench
- **THEN** the Auto Slice workflow SHALL allow the user to start smart slicing
- **AND** the workflow SHALL NOT require a target prefab when the user only wants to generate Sprite assets
- **AND** the workflow SHALL require a target prefab before prefab matching or applying Sprite references

#### Scenario: Workflow exposes step state
- **WHEN** Auto Slice is idle, detecting, reviewing regions, generating sprites, reviewing matches, applying, applied, or failed
- **THEN** the workbench SHALL display the current stage and the next primary action
- **AND** the workbench SHALL prevent users from applying Sprite changes before regions and matches have been reviewed

#### Scenario: Existing Draft Workbench flow remains available
- **WHEN** a user uses AI structure generation, Parse & Preview, overlay, or regular Apply to Prefab
- **THEN** those existing workflows SHALL remain usable without requiring ComfyUI configuration

### Requirement: ComfyUI connection and workflow configuration

The Auto Slice workflow SHALL support local ComfyUI connection checks and SAM3 workflow selection without exposing advanced technical settings in the default flow.

#### Scenario: ComfyUI connection succeeds
- **WHEN** the user tests a configured ComfyUI URL and the service responds
- **THEN** the workbench SHALL show the connection as available
- **AND** the workflow SHALL allow detection to proceed

#### Scenario: ComfyUI connection fails
- **WHEN** the configured ComfyUI URL is unreachable or returns an error
- **THEN** the workbench SHALL show a readable error containing the URL and failure reason
- **AND** the workflow SHALL NOT clear existing region, Sprite, or assignment review data

#### Scenario: SAM3 workflow is selected
- **WHEN** the user selects a ComfyUI workflow JSON file
- **THEN** the workbench SHALL persist that workflow selection in an Editor-only configuration asset
- **AND** the workflow SHALL be available the next time Draft Workbench opens

#### Scenario: Advanced SAM3 settings are available but secondary
- **WHEN** the user opens Auto Slice advanced settings
- **THEN** the workbench SHALL expose ComfyUI URL, workflow file, detection preset or prompt, threshold, refine iterations, individual mask mode, bbox expansion, output root, and cache root
- **AND** those settings SHALL NOT be required for the default guided workflow when defaults are valid

### Requirement: Region detection review

The Auto Slice workflow SHALL convert SAM3 outputs into reviewable regions before generating Unity Sprite assets.

#### Scenario: Regions are detected
- **WHEN** ComfyUI/SAM3 returns mask or manifest output for a design image
- **THEN** the workbench SHALL create region records containing region id, marker, label, confidence, bbox, source hash, include state, and optional mask path
- **AND** the workbench SHALL show the detected regions in a review list

#### Scenario: Regions are visualized on the design image
- **WHEN** detected regions have valid bbox data
- **THEN** the workbench SHALL overlay region boxes on the design image preview
- **AND** each region box SHALL indicate selection state or review severity through color or style

#### Scenario: User reviews regions before generation
- **WHEN** regions are available for review
- **THEN** the user SHALL be able to include or ignore each region before generating Sprite assets
- **AND** low-confidence or unknown regions SHALL be marked as needing review

#### Scenario: Region names are editable
- **WHEN** the user edits a region marker
- **THEN** generated Sprite file names and matching tokens SHALL use the updated marker
- **AND** duplicate or invalid file-name characters SHALL be normalized safely

### Requirement: Sprite generation from selected regions

The Auto Slice workflow SHALL generate Unity Sprite assets from approved regions without polluting runtime or cache directories with intermediate ComfyUI data.

#### Scenario: Selected regions generate Sprite assets
- **WHEN** the user confirms selected regions and starts Sprite generation
- **THEN** the workbench SHALL crop each selected region from the source image using bbox and optional mask alpha
- **AND** the workbench SHALL write each final PNG under `Assets/AssetRaw/Image/DraftWorkbenchGenerated/<PrefabName>/<SourceHash>/`
- **AND** each PNG SHALL be imported as a Unity Sprite asset

#### Scenario: Intermediate ComfyUI files are cached outside Assets
- **WHEN** ComfyUI produces raw history, temporary masks, or debug overlays
- **THEN** the workbench SHALL store those intermediate files under `Library/DraftWorkbench/ComfyCache/<SourceHash>/`
- **AND** those intermediate files SHALL NOT be required by prefab runtime references

#### Scenario: Asset-only generation is supported
- **WHEN** the user generates Sprite assets without applying them to a prefab
- **THEN** the workbench SHALL keep generated Sprite assets in the output directory
- **AND** the workflow SHALL report that no prefab was modified

### Requirement: Prefab Sprite matching review

The Auto Slice workflow SHALL match generated Sprite assets to prefab Image nodes through transparent, reviewable assignment records.

#### Scenario: Candidate matches are produced
- **WHEN** generated Sprite assets and previewed UGUI nodes are available
- **THEN** the workbench SHALL produce assignment candidates between Sprites and `UnityEngine.UI.Image` nodes
- **AND** each assignment SHALL include node path, old Sprite reference, new Sprite reference, region marker, confidence, and match reason

#### Scenario: Matching reasons are visible
- **WHEN** the user selects an assignment
- **THEN** the workbench SHALL show why it was suggested, including marker matches, spriteHint matches, bbox IoU, or node-name token matches where available

#### Scenario: Existing Sprite references are protected
- **WHEN** a candidate assignment targets an Image that already has a Sprite
- **THEN** the assignment SHALL be marked as requiring review by default
- **AND** the workbench SHALL NOT overwrite that Sprite unless the user approves the assignment

#### Scenario: Low-confidence matches require review
- **WHEN** an assignment confidence is below the configured auto-approval threshold
- **THEN** the assignment SHALL be marked as requiring review
- **AND** the workbench SHALL NOT apply it until the user approves or changes it

### Requirement: Safe Sprite application

The Auto Slice workflow SHALL apply only approved Sprite assignments and record enough information to revert them safely.

#### Scenario: Approved assignments are applied
- **WHEN** the user applies approved Sprite assignments to a target prefab
- **THEN** the workbench SHALL set `Image.sprite` only on approved target nodes
- **AND** the workbench SHALL save the prefab asset
- **AND** the workbench SHALL create an apply record containing each old and new Sprite reference

#### Scenario: Unapproved assignments are skipped
- **WHEN** the user applies Sprite assignments while some assignments are unapproved
- **THEN** unapproved assignments SHALL NOT modify prefab contents
- **AND** the result report SHALL list how many assignments were applied and skipped

#### Scenario: Revert restores Sprite references
- **WHEN** the user reverts the latest Auto Slice apply record
- **THEN** the workbench SHALL restore each recorded Image node to its old Sprite reference
- **AND** generated Sprite assets SHALL NOT be deleted by default

#### Scenario: Manual changes are protected during Sprite revert
- **WHEN** a recorded Image node no longer has the Sprite that Auto Slice applied
- **THEN** the revert operation SHALL report a conflict
- **AND** the workbench SHALL NOT overwrite the manually changed Sprite without explicit confirmation
