# image-to-ugui-draft-workbench Specification

## Purpose

定义编辑器内从静态 UI 草图图像构建 GameView UGUI 层级的 draft workbench 能力，支持预览、应用、回滚和绑定报告。

## Requirements

### Requirement: Editor-only draft workbench

The project SHALL provide a Unity Editor-only workbench for using a static UI draft image as a GameView UGUI construction reference.

#### Scenario: Workbench opens for GameView prefab

- **WHEN** a user opens the draft workbench for the GameView prefab
- **THEN** the workbench SHALL let the user select a local image asset or imported Texture2D as the draft reference
- **AND** the workbench SHALL associate the draft with the target GameView prefab

#### Scenario: Workbench has no runtime dependency

- **WHEN** the player build or HotFix runtime assemblies are compiled
- **THEN** the draft workbench code SHALL NOT be included in runtime assemblies
- **AND** runtime GameView code SHALL NOT depend on draft workbench types

### Requirement: Draft metadata persistence

The draft workbench SHALL persist editor-only metadata needed to reproduce a draft preview session.

#### Scenario: Draft metadata is saved

- **WHEN** a user creates or updates a draft preview for GameView
- **THEN** the workbench SHALL save the source image reference, target prefab reference, source image size, canvas reference resolution, overlay opacity, fit mode, and last apply record reference

#### Scenario: Draft metadata is reloaded

- **WHEN** a user reopens the draft workbench for a GameView prefab that has saved draft metadata
- **THEN** the workbench SHALL restore the draft image and preview settings from the saved metadata

#### Scenario: Runtime asset pollution is avoided

- **WHEN** draft metadata is saved
- **THEN** the metadata SHALL be stored as an editor-only asset or sidecar
- **AND** the metadata SHALL NOT require inclusion in YooAsset runtime bundles

### Requirement: Draft preview overlay

The draft workbench SHALL provide a preview overlay that helps align UGUI elements to the draft image without permanently changing runtime UI.

#### Scenario: Preview overlay is shown

- **WHEN** a user enables draft preview for GameView
- **THEN** the workbench SHALL display the draft image aligned to the GameView canvas using the saved fit mode and opacity

#### Scenario: Preview overlay can be adjusted

- **WHEN** a user changes preview opacity or fit mode
- **THEN** the overlay SHALL update in the editor preview
- **AND** the updated setting SHALL be persisted in draft metadata

#### Scenario: Preview objects are cleaned before apply completes

- **WHEN** the user applies approved UGUI changes
- **THEN** draft overlay objects SHALL be removed from the saved runtime GameView prefab or marked EditorOnly so they cannot affect play mode UI behavior

### Requirement: Apply report

The draft workbench SHALL report the GameView prefab and binding changes it applies.

#### Scenario: Report lists generated UI changes

- **WHEN** a user applies draft workbench changes
- **THEN** the workbench SHALL produce a report listing created GameObject paths, modified RectTransform properties, added components, and skipped conflicts

#### Scenario: Report lists binding changes

- **WHEN** a user applies ReferenceCollector or UI script binding changes through the workbench
- **THEN** the report SHALL list added ReferenceCollector keys, skipped duplicate keys, missing component warnings, and script field changes

#### Scenario: Report is reviewable after apply

- **WHEN** apply completes
- **THEN** the report SHALL remain available from the workbench metadata or Unity Editor log for later review

### Requirement: Scoped revert

The draft workbench SHALL support reverting changes made by a recorded apply operation without rebuilding the entire GameView prefab.

#### Scenario: Recorded apply is reverted

- **WHEN** a user reverts the latest recorded apply operation
- **THEN** the workbench SHALL remove or restore only the GameObjects, RectTransform values, ReferenceCollector entries, and script fields recorded in that apply operation

#### Scenario: Manual changes are protected

- **WHEN** a recorded object or binding has been changed after the apply operation
- **THEN** the workbench SHALL report the conflict
- **AND** the workbench SHALL NOT overwrite or delete that changed content without explicit user confirmation

#### Scenario: No apply record exists

- **WHEN** a user requests revert before any apply record exists for the draft
- **THEN** the workbench SHALL make no prefab changes
- **AND** the workbench SHALL report that there is no recorded apply to revert
