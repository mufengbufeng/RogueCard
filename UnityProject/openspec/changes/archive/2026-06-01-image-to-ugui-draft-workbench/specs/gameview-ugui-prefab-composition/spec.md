## ADDED Requirements

### Requirement: Draft workbench builder integration

GameView UGUI prefab composition tooling SHALL expose reusable editor APIs that allow the draft workbench to preview and apply known GameView UI node creation without duplicating hierarchy construction logic.

#### Scenario: Workbench previews known GameView nodes

- **WHEN** the draft workbench requests a preview for known GameView UGUI nodes
- **THEN** the GameView prefab composition tooling SHALL provide the node hierarchy, component types, default RectTransform values, and expected names that would be generated
- **AND** the preview SHALL be available without saving the prefab

#### Scenario: Workbench applies approved node changes

- **WHEN** a user approves generated GameView node changes in the draft workbench
- **THEN** the GameView prefab composition tooling SHALL create or repair those nodes using the same naming and component rules as the normal GameView prefab builder

#### Scenario: Generated nodes remain compatible with GameController

- **WHEN** GameView nodes are created through the draft workbench integration
- **THEN** the resulting prefab SHALL preserve the ReferenceCollector keys and component layout required by the existing GameController and GameView tests

### Requirement: Draft overlay exclusion from runtime prefab composition

GameView runtime prefab composition SHALL exclude draft-only preview overlays and metadata objects.

#### Scenario: Runtime prefab is validated after apply

- **WHEN** the draft workbench apply operation saves GameView prefab changes
- **THEN** the saved runtime prefab SHALL NOT contain active draft overlay objects that participate in GameView layout or input

#### Scenario: EditorOnly overlay is ignored by runtime validation

- **WHEN** a draft overlay object is retained for editor convenience
- **THEN** it SHALL be tagged or otherwise marked as EditorOnly
- **AND** GameView runtime validation SHALL ignore it for gameplay UI composition
