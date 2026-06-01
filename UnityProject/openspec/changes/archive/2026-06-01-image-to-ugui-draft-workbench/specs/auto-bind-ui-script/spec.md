## ADDED Requirements

### Requirement: Draft workbench ReferenceCollector binding plan

The UI script auto-binding tooling SHALL support generating a ReferenceCollector binding plan for GameView nodes proposed or created by the draft workbench.

#### Scenario: Binding plan uses project rules

- **WHEN** the draft workbench asks for bindings for generated or selected GameView UGUI nodes
- **THEN** the auto-binding tooling SHALL resolve ReferenceCollector keys and component types from the project ReferenceCollector rule configuration
- **AND** it SHALL NOT use a separate suffix-to-component rule table

#### Scenario: Duplicate key is reported

- **WHEN** a proposed binding key already exists in the target ReferenceCollector
- **THEN** the binding plan SHALL mark the entry as skipped or conflicting
- **AND** the binding plan SHALL include the existing key in its report

#### Scenario: Missing component is reported

- **WHEN** a proposed node name matches a configured rule but the required component is missing
- **THEN** the binding plan SHALL skip that node
- **AND** the binding plan SHALL report the missing component type

### Requirement: Draft workbench script field update

The UI script auto-binding tooling SHALL update generated UI script fields for draft workbench bindings through the existing text rewriting path.

#### Scenario: New binding requires script field

- **WHEN** an applied draft workbench binding introduces a ReferenceCollector key that needs a corresponding UI script field
- **THEN** the auto-binding tooling SHALL use the existing UI script binder text rewriter to add the field
- **AND** the generated field type SHALL match the component type resolved from the ReferenceCollector rule configuration

#### Scenario: Existing script field is preserved

- **WHEN** the required UI script field already exists with a compatible type
- **THEN** the auto-binding tooling SHALL preserve the existing field
- **AND** it SHALL NOT emit a duplicate field for the same ReferenceCollector key

#### Scenario: Incompatible script field is reported

- **WHEN** the required UI script field already exists with an incompatible type
- **THEN** the auto-binding tooling SHALL report a conflict
- **AND** it SHALL NOT overwrite the existing field without explicit user confirmation

### Requirement: Draft workbench binding report

The UI script auto-binding tooling SHALL return a structured binding report to the draft workbench after preview or apply.

#### Scenario: Preview report is generated

- **WHEN** the draft workbench previews binding changes
- **THEN** the auto-binding tooling SHALL return the proposed ReferenceCollector entries, script field changes, skipped entries, and conflicts without modifying files

#### Scenario: Apply report is generated

- **WHEN** the draft workbench applies binding changes
- **THEN** the auto-binding tooling SHALL return the applied ReferenceCollector entries, script field changes, skipped entries, warnings, and conflicts
