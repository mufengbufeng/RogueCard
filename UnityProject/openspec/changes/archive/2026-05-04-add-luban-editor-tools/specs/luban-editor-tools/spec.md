## ADDED Requirements

### Requirement: Open Luban data directory from Unity menu
The Unity Editor SHALL provide a `Luban/OpenToDataPath` menu item that opens the Luban table directory `Configs/GameConfig/Datas` for the current project checkout.

#### Scenario: Open existing data directory
- **WHEN** the user selects `Luban/OpenToDataPath` in the Unity Editor menu and `Configs/GameConfig/Datas` exists
- **THEN** the system opens that directory in the operating system file explorer

#### Scenario: Report missing data directory
- **WHEN** the user selects `Luban/OpenToDataPath` and `Configs/GameConfig/Datas` does not exist
- **THEN** the system writes an error log to the Unity Console identifying the missing directory

### Requirement: Build Luban data from Unity menu
The Unity Editor SHALL provide a `Luban/BuildData` menu item that invokes the existing Luban export script `Configs/GameConfig/gen_code_bin_to_project.bat` for the current project checkout.

#### Scenario: Run existing export script
- **WHEN** the user selects `Luban/BuildData` and `Configs/GameConfig/gen_code_bin_to_project.bat` exists
- **THEN** the system executes that script with the script directory as its working directory

#### Scenario: Report missing export script
- **WHEN** the user selects `Luban/BuildData` and `Configs/GameConfig/gen_code_bin_to_project.bat` does not exist
- **THEN** the system writes an error log to the Unity Console identifying the missing script

### Requirement: Surface Luban export errors in Unity Console
The Unity Editor SHALL capture Luban export process output and write Unity error logs when the process emits error output, prints Error-related text, or exits with a non-zero code.

#### Scenario: Error text appears in export output
- **WHEN** the Luban export process writes output containing `Error` or `error`
- **THEN** the system writes that output to the Unity Console as an error log

#### Scenario: Export process exits unsuccessfully
- **WHEN** the Luban export process exits with a non-zero exit code
- **THEN** the system writes an error log to the Unity Console containing the exit code

#### Scenario: Export process succeeds
- **WHEN** the Luban export process exits with code 0 and no Error-related output is captured
- **THEN** the system writes a normal completion log to the Unity Console

### Requirement: Refresh Unity assets after export
The Unity Editor SHALL refresh the Unity asset database after the Luban export process finishes so generated assets and code changes are visible to the editor.

#### Scenario: Export process finishes
- **WHEN** the Luban export process finishes regardless of success or failure
- **THEN** the system refreshes the Unity asset database
