---
name: openspec-apply-change
description: Implement tasks from an OpenSpec change. Use when the user wants to start implementing, continue implementation, or work through tasks.
license: MIT
compatibility: Requires openspec CLI.
metadata:
  author: openspec
  version: "1.0"
  generatedBy: "1.3.1"
---

Implement tasks from an OpenSpec change.

**Input**: Optionally specify a change name. If omitted, check if it can be inferred from conversation context. If vague or ambiguous you MUST prompt for available changes.

**Steps**

1. **Select the change**

   If a name is provided, use it. Otherwise:
   - Infer from conversation context if the user mentioned a change
   - Auto-select if only one active change exists
   - If ambiguous, run `openspec list --json` to get available changes and use the **AskUserQuestion tool** to let the user select

   Always announce: "Using change: <name>" and how to override.

2. **Check status to understand the schema**
   ```bash
   openspec status --change "<name>" --json
   ```
   Parse the JSON to understand:
   - `schemaName`: The workflow being used (e.g., "spec-driven")
   - `planningHome`, `changeRoot`, and `actionContext`: planning scope and edit constraints
   - Which artifact contains the tasks (typically "tasks" for spec-driven, check status for others)

3. **Get apply instructions**

   ```bash
   openspec instructions apply --change "<name>" --json
   ```

   This returns:
   - `contextFiles`: artifact ID -> array of concrete file paths (varies by schema)
   - Progress (total, complete, remaining)
   - `tasks`: each entry includes `description`, `done`, `feedbackType`, `defaulted`, and optionally `invalidRaw`
   - Dynamic instruction based on current state

   **Handle states:**
   - If `state: "blocked"` (missing artifacts): show message, suggest using openspec-continue-change
   - If `state: "all_done"`: congratulate, suggest archive
   - Otherwise: proceed to implementation

   **Workspace guard:** If status JSON reports `actionContext.mode: "workspace-planning"` and `allowedEditRoots` is empty, explain that full workspace apply is not supported in this slice. Treat linked repos and folders as read-only context, ask the user to select an affected area through an explicit implementation workflow, and STOP before editing files.

   **Invalid feedback-type guard:** If any task entry has `feedbackType: null` (i.e. carries an invalid annotation in tasks.md), STOP immediately. Report each offending task with its `invalidRaw` value. Tell the user the valid values are: `tdd`, `repl`, `static`, `doc`, `manual`. Do not begin work until the annotations are corrected.

4. **Read context files**

   Read every file path listed under `contextFiles` from the apply instructions output.
   The files depend on the schema being used:
   - **spec-driven**: proposal, specs, design, tasks
   - Other schemas: follow the contextFiles from CLI output

5. **Show current progress**

   Display:
   - Schema being used
   - Progress: "N/M tasks complete"
   - Remaining tasks overview, including each task's `feedbackType`
   - Dynamic instruction from CLI
   - For every task with `defaulted: true`, emit a one-line warning:
     `Warning: task "<description>" had no [type] annotation; defaulting to [static].`

6. **Implement tasks (loop until done, blocked, or stuck)**

   For each pending task, switch on its `feedbackType` and run the matching feedback loop. Do NOT use a uniform loop for every task — different types demand different evidence of completion.

   **6a. `tdd` — red → green → refactor**

   1. Write or extend a test that captures the task's behavior.
   2. Run the test. **Confirm it fails with the expected error.**
      - If the test passes on the first run before any implementation, STOP and report. Either the test is wrong or the change is already done. Do NOT mark the task complete.
   3. Implement until the test passes.
   4. Run the closest relevant test suite. Confirm no regressions were introduced.
   5. Refactor if helpful; tests still pass.
   6. Mark the task `- [x]`.

   **6b. `repl` — runnable command demonstrates the change**

   1. Identify a concrete command that demonstrates the behavior. Prefer one named in the task body; otherwise pick the closest CLI/script invocation that exercises the change.
   2. Implement.
   3. Run the command. Confirm the output matches the task's stated expectation.
   4. Mark the task `- [x]`.

   **6c. `static` — refactor / typing / no behavior change**

   1. Implement.
   2. Run the project's typecheck (`tsc` or equivalent). Confirm pass.
   3. Run lint where applicable. Confirm pass.
   4. Run the closest relevant test suite. Confirm no regressions.
   5. Mark the task `- [x]` only when all three signals are clean.

   **6d. `doc` — documentation / comments / README / changelog**

   1. Write or edit the documentation.
   2. Report to the user what was changed and where.
   3. Leave the task's checkbox `- [ ]` (UNCHECKED). The user marks it complete after review.
   4. Continue to the next task without blocking the run.

   **6e. `manual` — UI / copy / anything needing human eyeball**

   1. Make the change to the best of your ability.
   2. Report to the user with specifically what to inspect and why.
   3. Leave the task's checkbox `- [ ]` (UNCHECKED). The user marks it complete after verifying.
   4. Continue to the next task without blocking the run.

   **Stuck detection (`tdd` and `repl` only):**

   If three consecutive iterations of the loop for the same task fail to reach the success signal:
   - Do NOT mark the task complete.
   - Append an indented bullet under that task in `tasks.md` summarizing what was attempted, e.g.
     ```
     - [ ] [tdd] 4.2 Foo behaves like bar
       - stuck after 3 attempts: tests still failing on edge case X; tried approaches Y, Z
     ```
   - STOP the apply session.
   - Recommend the user inspect the artifacts or run a diagnose workflow (when available) before retrying.

   **Pause if** (separate from stuck):
   - Task is unclear → ask for clarification
   - Implementation reveals a design issue → suggest updating artifacts
   - Unexpected error or blocker encountered → report and wait for guidance
   - User interrupts

7. **On completion, pause, or stuck, show status**

   Display:
   - Tasks completed this session
   - Tasks **deferred** (`doc` and `manual` tasks left unchecked for user action) — list each with what the user needs to do
   - Overall progress: "N/M tasks complete"
   - If all done: suggest archive (openspec-archive-change)
   - If paused or stuck: explain why and wait for guidance

**Output During Implementation**

```
## Implementing: <change-name> (schema: <schema-name>)

Working on task 3/7 [tdd]: <task description>
[...write test → confirm red → implement → green...]
✓ Task complete

Working on task 4/7 [doc]: <task description>
[...write doc...]
↳ Doc updated at <path>; left unchecked for your review

Working on task 5/7 [static]: <task description>
[...implement → tsc → lint → tests...]
✓ Task complete
```

**Output On Completion**

```
## Implementation Complete

**Change:** <change-name>
**Schema:** <schema-name>
**Progress:** 7/7 tasks complete ✓

### Completed This Session
- [x] [tdd] Task 1
- [x] [static] Task 2
...

### Deferred (awaiting your action)
- [ ] [doc] Task 4 — please review <path>
- [ ] [manual] Task 6 — please verify <what>

All non-deferred tasks complete!
```

**Output On Stuck**

```
## Implementation Stuck

**Change:** <change-name>
**Schema:** <schema-name>
**Progress:** 4/7 tasks complete; stuck on task 5

### Stuck Task
- [ ] [tdd] 5.x <description>
  - 3 attempts; test still failing on <symptom>

**Recommendation:**
- Inspect <relevant context paths>
- Or run a diagnose workflow (when available)

Do NOT retry without a new hypothesis.
```

**Output On Pause (Issue Encountered)**

```
## Implementation Paused

**Change:** <change-name>
**Schema:** <schema-name>
**Progress:** 4/7 tasks complete

### Issue Encountered
<description of the issue>

**Options:**
1. <option 1>
2. <option 2>
3. Other approach

What would you like to do?
```

**Guardrails**
- Branch on each task's `feedbackType` — do NOT run a uniform loop
- For `tdd` tasks, red-before-green is mandatory; a test that passes immediately is a stop signal, not a success signal
- For `doc` and `manual` tasks, NEVER auto-mark the checkbox; leave it unchecked and report what the user needs to do
- After three failed iterations on the same `tdd` or `repl` task, STOP — do not keep trying
- Warn once per defaulted task at the start of step 5
- Stop and report if any task carries an invalid `feedbackType` (`null` with `invalidRaw` set)
- Always read context files before starting (from the apply instructions output)
- Keep code changes minimal and scoped to each task
- Update task checkbox immediately after completing each non-deferred task
- Use `contextFiles` from CLI output, don't assume specific file names

**Fluid Workflow Integration**

This skill supports the "actions on a change" model:

- **Can be invoked anytime**: Before all artifacts are done (if tasks exist), after partial implementation, interleaved with other actions
- **Allows artifact updates**: If implementation reveals design issues, suggest updating artifacts - not phase-locked, work fluidly
