## MODIFIED Requirements

### Requirement: Editor-only draft workbench

The project SHALL provide a Unity Editor-only workbench for converting a UI design image into UGUI node hierarchy on any Prefab, powered by AI vision analysis.

#### Scenario: Workbench opens for any Prefab

- **WHEN** a user opens the draft workbench
- **THEN** the workbench SHALL let the user select any Prefab asset as the target
- **AND** the workbench SHALL let the user select a local image as the design reference
- **AND** the workbench SHALL read the target Prefab's Canvas reference resolution for coordinate normalization

#### Scenario: Workbench provides AI generation workflow

- **WHEN** a user has configured AI settings and selected a design image
- **THEN** the workbench SHALL provide a button to send the image to the AI Vision API
- **AND** the workbench SHALL display the returned ui_structure.json for review and editing
- **AND** the workbench SHALL provide a button to convert the JSON to UGUI nodes and preview the result

#### Scenario: Workbench has no runtime dependency

- **WHEN** the player build or HotFix runtime assemblies are compiled
- **THEN** the draft workbench code SHALL NOT be included in runtime assemblies
- **AND** runtime code SHALL NOT depend on draft workbench types

### Requirement: Draft metadata persistence

The draft workbench SHALL persist editor-only metadata needed to reproduce a draft session, including AI generation results.

#### Scenario: Draft metadata includes AI generation result

- **WHEN** a user completes an AI generation
- **THEN** the workbench SHALL save the source image reference, target prefab reference, canvas reference resolution, the raw AI response JSON, and the converted UguiNodeDescriptor list

#### Scenario: Draft metadata is reloaded

- **WHEN** a user reopens the draft workbench for a Prefab that has saved draft metadata
- **THEN** the workbench SHALL restore the design image, AI response JSON, and preview settings

### Requirement: Apply report

The draft workbench SHALL report the Prefab and binding changes it applies.

#### Scenario: Report lists generated UI changes for any Prefab

- **WHEN** a user applies draft workbench changes to any Prefab
- **THEN** the workbench SHALL produce a report listing created GameObject paths, modified RectTransform properties, added components, and skipped conflicts
- **AND** the report SHALL work identically for new Prefabs and existing Prefabs with existing content

#### Scenario: Report lists AI conversion warnings

- **WHEN** the converter skipped elements or renamed duplicates during conversion
- **THEN** the report SHALL include a conversion warnings section listing skipped elements and rename operations

### Requirement: Scoped revert

The draft workbench SHALL support reverting changes made by a recorded apply operation without rebuilding the entire Prefab.

#### Scenario: Recorded apply is reverted on any Prefab

- **WHEN** a user reverts the latest recorded apply operation on any Prefab
- **THEN** the workbench SHALL remove or restore only the GameObjects, RectTransform values, ReferenceCollector entries, and script fields recorded in that apply operation

#### Scenario: Manual changes are protected

- **WHEN** a recorded object or binding has been changed after the apply operation
- **THEN** the workbench SHALL report the conflict
- **AND** the workbench SHALL NOT overwrite or delete that changed content without explicit user confirmation
