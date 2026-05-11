---
name: tdd-development
description: Enforce a standard test-driven development workflow. Use when implementing OpenSpec changes, applying tasks, adding a new feature, modifying existing behavior, or fixing behavior that should be covered by tests.
---

# TDD Development

Use this skill whenever code behavior is being added or changed. The default workflow is Red, Green, Refactor.

## Required Triggers

Follow this workflow for:
- OpenSpec apply implementation work
- New features
- Existing feature modifications
- Bug fixes with observable behavior
- Refactors that intentionally preserve behavior

Only skip test-first work when it is genuinely impractical. If skipping, state the reason before changing production code and add the nearest feasible automated or manual verification.

## Standard Loop

For each behavior-sized task:

1. Identify the expected behavior from the spec, task, bug report, or user request.
2. Find the closest existing test style and test runner for that area.
3. Write or update a focused failing test before production code.
4. Run the smallest relevant test target and confirm the failure is for the expected reason.
5. Implement the smallest production change that can make the test pass.
6. Run the focused test target again until it passes.
7. Refactor only while tests are green.
8. Re-run the affected tests after refactoring.
9. Mark implementation tasks complete only after the related tests or explicit verification pass.

## Unity Guidance

- Prefer EditMode tests for pure C#, services, data transforms, validators, state machines, and view-model behavior.
- Use PlayMode tests only when behavior depends on scene lifecycle, GameObject interaction, coroutines over frames, physics, UI Toolkit runtime behavior, or other runtime integration.
- Keep gameplay logic testable outside MonoBehaviour lifecycle when a small seam is reasonable.
- If a behavior depends on assets, scenes, or generated Unity files, use the narrowest stable fixture and avoid broad scene loading unless the behavior requires it.

## OpenSpec Artifact Guidance

When creating or updating OpenSpec tasks for implementation:
- Include test-first tasks before production implementation tasks.
- Make each test task describe the observable behavior being protected.
- Include a final verification task that names the targeted test command or Unity Test Runner mode.
- Do not write tasks that only say "add tests"; tie tests to requirements or scenarios.

## Blockers

If test-first is blocked:
- Explain the blocker clearly before production edits.
- Add a characterization test at the nearest feasible boundary when possible.
- If no automated path exists, add a manual verification task and call out the remaining test debt.
