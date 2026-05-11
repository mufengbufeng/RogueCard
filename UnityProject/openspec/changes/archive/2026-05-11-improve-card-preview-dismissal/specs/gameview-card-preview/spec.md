## ADDED Requirements

### Requirement: Active card preview must dismiss on non-card battle panel clicks

When a card preview is active, the hand fan UI SHALL allow the player to dismiss it by clicking any non-card area in the battle panel. Non-card dismissal SHALL call `CardPreviewController.ExitPreview()` and SHALL consume the pointer event so the same gesture does not activate another non-card control.

Card roots and their descendants SHALL be excluded from non-card dismissal. Clicking a card while preview is active SHALL continue to use `TogglePreview(handIdx, source)` semantics: the same card closes the preview, and a different card replaces the preview.

#### Scenario: Clicking battle panel backdrop closes active preview

- **WHEN** card A is currently previewed and the player clicks a battle panel area that is not any hand card
- **THEN** the preview for card A SHALL be removed
- **AND** the pointer event SHALL be consumed

#### Scenario: Clicking the same card still closes through toggle

- **WHEN** card A is currently previewed and the player clicks card A
- **THEN** non-card dismissal SHALL NOT run for that pointer event
- **AND** `TogglePreview(handA, sourceA)` SHALL close the preview

#### Scenario: Clicking a different card switches preview

- **WHEN** card A is currently previewed and the player clicks card B
- **THEN** non-card dismissal SHALL NOT run for that pointer event
- **AND** `TogglePreview(handB, sourceB)` SHALL remove card A's preview and create card B's preview

#### Scenario: Clicking a non-card control does not activate it while dismissing preview

- **WHEN** card A is currently previewed and the player clicks a non-card battle control such as the end-turn button
- **THEN** the preview SHALL close
- **AND** the control SHALL NOT receive the same pointer gesture as an activation
