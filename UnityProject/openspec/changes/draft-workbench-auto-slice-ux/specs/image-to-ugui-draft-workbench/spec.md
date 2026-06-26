## MODIFIED Requirements

### Requirement: Apply report

The draft workbench SHALL report the Prefab, Sprite, and binding changes it applies.

#### Scenario: Report lists generated UI changes for any Prefab

- **WHEN** a user applies draft workbench changes to any Prefab
- **THEN** the workbench SHALL produce a report listing created GameObject paths, modified RectTransform properties, added components, and skipped conflicts
- **AND** the report SHALL work identically for new Prefabs and existing Prefabs with existing content

#### Scenario: Report lists Sprite reference changes

- **WHEN** a user applies Auto Slice Sprite assignments to any Prefab
- **THEN** the workbench SHALL produce a report listing each modified Image node path
- **AND** the report SHALL include each old Sprite GUID/path and new Sprite GUID/path
- **AND** the report SHALL include the source region id and marker when available

#### Scenario: Report lists AI conversion warnings

- **WHEN** the converter skipped elements or renamed duplicates during conversion
- **THEN** the report SHALL include a conversion warnings section listing skipped elements and rename operations

#### Scenario: Report lists binding changes

- **WHEN** a user applies ReferenceCollector or UI script binding changes through the workbench
- **THEN** the report SHALL list added ReferenceCollector keys, skipped duplicate keys, missing component warnings, and script field changes

#### Scenario: Report is reviewable after apply

- **WHEN** apply completes
- **THEN** the report SHALL remain available from the workbench metadata or Unity Editor log for later review

### Requirement: Scoped revert

The draft workbench SHALL support reverting changes made by a recorded apply operation without rebuilding the entire Prefab.

#### Scenario: Recorded apply is reverted on any Prefab

- **WHEN** a user reverts the latest recorded apply operation on any Prefab
- **THEN** the workbench SHALL remove or restore only the GameObjects, RectTransform values, ReferenceCollector entries, script fields, and Sprite references recorded in that apply operation

#### Scenario: Recorded Sprite apply is reverted on any Prefab

- **WHEN** a user reverts an Auto Slice apply operation that modified Image.sprite references
- **THEN** the workbench SHALL restore each recorded Image node to its old Sprite reference
- **AND** the workbench SHALL NOT delete generated Sprite assets by default

#### Scenario: Manual changes are protected

- **WHEN** a recorded object, binding, or Sprite reference has been changed after the apply operation
- **THEN** the workbench SHALL report the conflict
- **AND** the workbench SHALL NOT overwrite or delete that changed content without explicit user confirmation

#### Scenario: No apply record exists

- **WHEN** a user requests revert before any apply record exists for the draft
- **THEN** the workbench SHALL make no prefab changes
- **AND** the workbench SHALL report that there is no recorded apply to revert
