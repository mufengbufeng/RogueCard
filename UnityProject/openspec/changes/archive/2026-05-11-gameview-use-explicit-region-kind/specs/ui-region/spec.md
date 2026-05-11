## ADDED Requirements

### Requirement: Screen Region routing must use UI route semantics
Screens that choose Region content from gameplay or domain state SHALL track the active Region using UI route semantics rather than reusing domain enum values as Region identifiers.

#### Scenario: Multiple gameplay phases share one Region
- **WHEN** gameplay phases `Prepare`, `PlayerTurn`, `MonsterTurn`, and `Check` all require the battle content Region
- **THEN** the screen SHALL map those phases to a UI route representing battle content
- **AND** the screen SHALL NOT represent the active Region as `BattlePhase.PlayerTurn`

#### Scenario: Reward gameplay phase selects reward Region
- **WHEN** gameplay phase is `Reward`
- **THEN** the screen SHALL map the phase to a UI route representing reward content

### Requirement: Screen-owned Region content coordinators must be disposed when leaving their route
When a screen owns an object that coordinates the currently displayed Region content, the screen SHALL dispose that coordinator before replacing the Region with a different route's content.

#### Scenario: Leaving battle content for reward content
- **WHEN** the game screen routes from battle content to reward content
- **THEN** the screen SHALL dispose the active battle content coordinator
- **AND** the disposed battle content coordinator SHALL no longer hold active UI event subscriptions

### Requirement: Screen phase subscriptions must be released on disposal
Screens that subscribe to ViewModel phase changes for Region routing SHALL unsubscribe during screen disposal.

#### Scenario: GameView disposed after subscribing to phase changes
- **WHEN** `GameView` is disposed
- **THEN** it SHALL unsubscribe from `ViewModel.Phase.Changed`
- **AND** later phase changes SHALL NOT invoke the disposed `GameView`
