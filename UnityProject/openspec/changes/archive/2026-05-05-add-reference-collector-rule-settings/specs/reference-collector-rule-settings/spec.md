## ADDED Requirements

### Requirement: Project-level collection rules
The ReferenceCollector editor tooling SHALL use a project-level rule configuration as the source of truth for automatic collection naming rules.

#### Scenario: Default rules are available
- **WHEN** a project has not created a custom ReferenceCollector rule asset yet
- **THEN** the editor tooling SHALL provide the existing default suffix mappings for automatic collection

#### Scenario: TMP rule can be configured
- **WHEN** the project rule configuration contains suffix `TMP` mapped to `TMPro.TextMeshProUGUI`
- **THEN** automatic collection SHALL collect a child object ending with `TMP` as its TextMeshProUGUI component when that component exists

#### Scenario: Per-prefab rule edits are not available
- **WHEN** a user selects a GameObject with ReferenceCollector in the Inspector
- **THEN** the Inspector SHALL NOT provide controls to create, edit, or delete collection rules for only that selected ReferenceCollector

### Requirement: Rule-based automatic collection
ReferenceCollector automatic collection SHALL match child object names against the configured project rules and collect the matching target component.

#### Scenario: Matching configured suffix collects component
- **WHEN** a child object name ends with an enabled configured suffix and the configured component exists on that child object
- **THEN** automatic collection SHALL add a ReferenceCollector entry using the child object name as the key and the matched component as the referenced object

#### Scenario: Existing key is preserved
- **WHEN** automatic collection finds a child object whose key already exists in ReferenceCollector data
- **THEN** automatic collection SHALL NOT create a duplicate entry for that key

#### Scenario: Missing component is skipped
- **WHEN** a child object name matches a configured suffix but the configured component cannot be found on that child object
- **THEN** automatic collection SHALL skip that child object and report a warning in the Unity Editor console

#### Scenario: GameObject rule collects object
- **WHEN** a child object name matches a configured rule whose target type is `UnityEngine.GameObject`
- **THEN** automatic collection SHALL add the child GameObject itself as the referenced object

### Requirement: UIElements ReferenceCollector Inspector
The ReferenceCollector custom Inspector SHALL be implemented with UIElements while preserving existing ReferenceCollector editing operations.

#### Scenario: Existing manual operations remain available
- **WHEN** a user selects a ReferenceCollector component
- **THEN** the Inspector SHALL provide controls for adding a reference, clearing all references, deleting null references, sorting references, deleting by key, editing existing keys and objects, and removing individual entries

#### Scenario: Automatic collection operations remain available
- **WHEN** a user selects a ReferenceCollector component
- **THEN** the Inspector SHALL provide controls for automatic collection and clearing automatically collected entries

#### Scenario: Project rules are displayed read-only
- **WHEN** a user views the ReferenceCollector Inspector
- **THEN** the Inspector SHALL display a read-only summary of the active project collection rules

#### Scenario: Drag and drop remains supported
- **WHEN** a user drags Unity objects onto the ReferenceCollector Inspector reference area
- **THEN** the Inspector SHALL add dropped objects as ReferenceCollector entries using each object name as the key

### Requirement: Shared rule use by editor tools
ReferenceCollector editor tools SHALL avoid maintaining separate automatic collection and script generation rule definitions when both tools need suffix-to-component mapping.

#### Scenario: Script generation can resolve collected component types
- **WHEN** a ReferenceCollector entry was collected through a configured component rule
- **THEN** the script generation tooling SHALL be able to resolve the corresponding component type without requiring a second divergent rule definition
