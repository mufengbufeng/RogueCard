## ADDED Requirements

### Requirement: Rect-centric AI schema review workflow

The draft workbench SHALL request and review a rect-centric Unity UGUI schema when generating AI draft structures.

#### Scenario: AI generation requests Unity-native rect schema

- **WHEN** a user triggers AI generation from the draft workbench
- **THEN** the workbench SHALL request a `ui_structure.json` schema that uses `rect`, `fixed` / `stretch`, `anchorPreset`, `anchoredPosition`, `inset`, `pivot`, `layout`, `layoutElement`, `safeArea`, `scroll`, and fitter semantics
- **AND** the returned JSON SHALL remain editable before conversion

#### Scenario: validation is shown before apply

- **WHEN** an AI response is parsed or converted
- **THEN** the workbench SHALL display validation errors and conversion warnings before the user applies prefab changes
- **AND** blocking validation errors SHALL prevent apply until the reviewed JSON is corrected or regenerated

### Requirement: Rect-centric draft metadata persistence

The draft workbench SHALL persist the information needed to reproduce a rect-centric AI draft session.

#### Scenario: metadata stores normalized schema state

- **WHEN** a user completes AI generation or conversion
- **THEN** the workbench SHALL save the raw AI JSON, schema version or normalization marker, converted descriptors, and validation or conversion messages in draft metadata

#### Scenario: legacy metadata reloads safely

- **WHEN** saved draft metadata predates rect-centric schema fields
- **THEN** the workbench SHALL reload the session with safe defaults
- **AND** it SHALL surface upgrade warnings instead of failing to open the draft session

### Requirement: Preview and apply share the same normalized result

The draft workbench SHALL keep review and application behavior consistent for the same schema input.

#### Scenario: preview and apply use the same descriptor source

- **WHEN** a user previews and then applies the same reviewed JSON
- **THEN** both operations SHALL use the same normalized converted descriptor set
- **AND** the applied prefab changes SHALL match the reviewed layout intent

#### Scenario: repeated conversion remains stable

- **WHEN** an unchanged design image and schema JSON are converted multiple times
- **THEN** descriptor ordering, generated hierarchy names, and preview bounds SHALL remain stable across conversions

### Requirement: Unity-native prefab apply semantics

The draft workbench SHALL apply advanced rect-centric layout intent using Unity-native prefab components and fields.

#### Scenario: advanced layout components are applied

- **WHEN** reviewed schema includes stretch rects, layout groups, `LayoutElement`, fitters, safe area, scroll, or grid semantics
- **THEN** prefab apply SHALL write the corresponding RectTransform and Unity UI component configuration to the prefab
- **AND** it SHALL NOT require runtime-only draft helper components to preserve the result
