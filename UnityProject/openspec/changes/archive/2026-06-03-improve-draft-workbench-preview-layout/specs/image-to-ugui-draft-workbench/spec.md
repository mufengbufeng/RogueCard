## ADDED Requirements

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
