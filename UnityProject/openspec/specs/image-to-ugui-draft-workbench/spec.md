# image-to-ugui-draft-workbench Specification

## Purpose

定义编辑器内从静态 UI 草图图像构建 GameView UGUI 层级的 draft workbench 能力，支持预览、应用、回滚和绑定报告。

## Requirements

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

The draft workbench SHALL report the Prefab and binding changes it applies.

#### Scenario: Report lists generated UI changes for any Prefab

- **WHEN** a user applies draft workbench changes to any Prefab
- **THEN** the workbench SHALL produce a report listing created GameObject paths, modified RectTransform properties, added components, and skipped conflicts
- **AND** the report SHALL work identically for new Prefabs and existing Prefabs with existing content

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
- **THEN** the workbench SHALL remove or restore only the GameObjects, RectTransform values, ReferenceCollector entries, and script fields recorded in that apply operation

#### Scenario: Manual changes are protected

- **WHEN** a recorded object or binding has been changed after the apply operation
- **THEN** the workbench SHALL report the conflict
- **AND** the workbench SHALL NOT overwrite or delete that changed content without explicit user confirmation

#### Scenario: No apply record exists

- **WHEN** a user requests revert before any apply record exists for the draft
- **THEN** the workbench SHALL make no prefab changes
- **AND** the workbench SHALL report that there is no recorded apply to revert

### Requirement: Persistent structure preview review layout

The draft workbench SHALL provide a persistent design-image review area for inspecting generated structure bounds before the user applies prefab changes.

#### Scenario: Wide window keeps a large adjacent preview

- **WHEN** a user opens the draft workbench in a window wide enough for split review
- **THEN** the workbench SHALL keep workflow controls in one pane
- **AND** it SHALL display the design image and generated structure boxes in a larger adjacent pane
- **AND** the preview pane SHALL remain visible while the user reviews JSON, node summaries, warnings, or apply actions

#### Scenario: Narrow window falls back to a readable stacked layout

- **WHEN** the draft workbench window is too narrow for a practical split view
- **THEN** the workbench SHALL fall back to a stacked layout that preserves access to the preview, workflow controls, and apply actions in one window
- **AND** the fallback SHALL NOT require opening a second EditorWindow to inspect the preview

### Requirement: Adaptive structure preview sizing

The draft workbench SHALL size the structure preview from the available preview pane bounds instead of a fixed maximum preview height.

#### Scenario: Portrait and landscape drafts scale from available space

- **WHEN** the selected design image or source canvas is significantly taller or wider than the workbench window
- **THEN** the preview SHALL scale to fit the available review pane while preserving image aspect ratio by default
- **AND** the workbench SHALL support an optional manual zoom inspection mode in addition to automatic fit

#### Scenario: Empty preview state is explicit

- **WHEN** no design image is selected or the current preview data has no drawable source bounds
- **THEN** the preview pane SHALL show an explicit empty state instead of stale or misleading structure graphics
- **AND** the empty state SHALL leave the rest of the workbench usable for configuration or parsing work
