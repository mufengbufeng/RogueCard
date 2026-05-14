## ADDED Requirements

### Requirement: Active card preview must dismiss when hand card drag starts

When a card preview is active and the player starts dragging any hand card, the hand fan UI SHALL dismiss the active preview during the same pointer move that transitions the hand interaction into `Dragging`.

The preview SHALL NOT dismiss merely because a hand card received `PointerDown`. A hand card gesture that remains within `HandFanLayoutOptions.DragThreshold` and resolves as a click SHALL continue to use existing `TogglePreview(handIdx, source)` semantics.

#### Scenario: Pointer move crossing drag threshold closes active preview

- **WHEN** card A is currently previewed and the player presses a hand card then moves farther than `DragThreshold`
- **THEN** the hand interaction SHALL enter `Dragging`
- **AND** the active preview SHALL be removed during that drag-start pointer move

#### Scenario: Pointer down alone keeps preview until gesture is classified

- **WHEN** card A is currently previewed and the player presses a hand card without moving beyond `DragThreshold`
- **THEN** the active preview SHALL remain visible
- **AND** no drag ghost SHALL be created

#### Scenario: Threshold-limited click still uses preview toggle semantics

- **WHEN** card A is currently previewed and the player clicks a hand card with movement less than or equal to `DragThreshold`
- **THEN** the gesture SHALL be handled as a card click
- **AND** preview behavior SHALL be determined by `TogglePreview(handIdx, source)`
