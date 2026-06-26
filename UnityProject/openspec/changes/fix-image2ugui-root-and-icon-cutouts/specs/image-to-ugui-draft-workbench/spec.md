## ADDED Requirements

### Requirement: Virtual root prefab apply

The Draft Workbench SHALL treat the JSON root element as a virtual root by default when applying converted UI structure to a target Prefab.

#### Scenario: JSON root is not created under target Prefab root
- **WHEN** a user parses `ui_structure.json` whose root is named `LevelPreviewMenu`
- **AND** the selected target Prefab root is named `RootCanvas`
- **THEN** applying the preview SHALL NOT create a `RootCanvas/LevelPreviewMenu` GameObject by default
- **AND** the JSON root children SHALL be created or updated directly under the target Prefab root according to their converted paths

#### Scenario: Existing target root remains stable
- **WHEN** Draft Workbench applies converted UI nodes using VirtualRoot mode
- **THEN** the target Prefab root GameObject name SHALL remain unchanged
- **AND** existing root-level components such as Canvas, CanvasScaler, GraphicRaycaster, UIView, or ReferenceCollector SHALL NOT be removed by root mapping

#### Scenario: IncludeRootNode compatibility remains available
- **WHEN** a caller explicitly requests IncludeRootNode conversion mode
- **THEN** the workbench MAY create the JSON root as a real child node
- **AND** that behavior SHALL be opt-in rather than the default Draft Workbench mode

### Requirement: Recorded wrapper cleanup

The Draft Workbench SHALL provide a safe cleanup path for wrapper nodes previously created from JSON roots.

#### Scenario: Recorded wrapper can be cleaned
- **WHEN** a top-level wrapper GameObject was created by a prior Draft Workbench apply record
- **AND** its path is present in metadata as a created object
- **THEN** the cleanup action SHALL be allowed to remove that wrapper and its recorded descendants
- **AND** the cleanup SHALL be recorded so the operation can be audited

#### Scenario: Unrecorded wrapper is not automatically deleted
- **WHEN** a top-level GameObject name resembles a JSON root wrapper but no apply record proves Draft Workbench created it
- **THEN** the cleanup action SHALL report it as a candidate
- **AND** the workbench SHALL NOT delete it automatically

### Requirement: Transparent icon generation status in reports

The Draft Workbench SHALL report transparent icon refinement and alpha validation outcomes alongside raw crop and Sprite import state.

#### Scenario: Report includes alpha validation failure
- **WHEN** an icon refinement output is rejected because it has no transparent alpha
- **THEN** the workbench report SHALL show the region id, marker, raw path, refined path, alpha statistics, and failure reason
- **AND** the report SHALL make clear that no final Sprite asset was imported for that region

#### Scenario: Report distinguishes rectangular and transparent assets
- **WHEN** a slicing run includes both rectangular panels and transparent icons
- **THEN** the report SHALL show which regions required alpha validation
- **AND** rectangular regions SHALL NOT be reported as failed merely because they are opaque
