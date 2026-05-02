---
description: Document learnings and prepare to compound an opence change.
---

$ARGUMENTS
<!-- OPENCE:START -->
**Guardrails**
- Favor straightforward, minimal implementations first and add complexity only when it is requested or clearly required.
- Keep changes tightly scoped to the requested outcome.
- Refer to `opence/AGENTS.md` (located inside the `opence/` directory—run `ls opence` or `opence update` if you don't see it) if you need additional opence conventions or clarifications.
- **Stage boundary**: This stage ends when documentation and skill checkpoint are complete. Do NOT automatically proceed to the archive stage or execute `opence archive`.

**Steps**
1. Determine the change ID:
   - If this prompt already includes a specific change ID (for example inside a `<ChangeId>` block populated by slash-command arguments), use that value after trimming whitespace.
   - If the conversation references a change loosely, run `opence list` to surface likely IDs, share the relevant candidates, and confirm which one the user intends.
   - Otherwise, ask the user which change to compound and wait for a confirmed change ID before proceeding.
2. Create a documentation entry under `docs/solutions/` summarizing the problem, root cause, and fix.
3. Skill memory checkpoint — Before creating a skill, answer these four questions:

   **Q1: What problem does this solve?**
   - Describe the specific problem or repeated workflow
   - Verify existing skills don't already solve it (`opence skill list`)
   - If an existing skill can be extended, prefer that over creating a new one

   **Q2: Who uses it and when?**
   - Define target user scenarios
   - List 3-5 specific trigger phrases users would naturally say
   - Example triggers: "how does this work?", "explain the auth flow", "review this PR"

   **Q3: How should the description be written?**
   - Include action verbs (Explains, Creates, Reviews, Validates)
   - Include keywords users naturally say
   - List specific trigger scenarios
   - Good: `"Explains code with visual diagrams. Use when explaining how code works or when user asks 'how does this work?'"`
   - Bad: `"Helps with development"` (too vague), `"Fix login CSS on Safari 15.4"` (too specific)

   **Q4: Is it worth creating?**
   - Estimate usage frequency over next 6 months
   - If fewer than 3 uses expected, write to `docs/solutions/` instead
   - Consider maintenance burden vs. value

   If all four questions are satisfactorily answered, proceed:
   - Consult the `opence-skill-creator` skill for structure, naming, and best practices
   - Use `opence skill add <skill-name> --description "..."` to create new skills
   - After creation, edit the SKILL.md file to add detailed instructions
   - Move extensive documentation to `references/` and reusable code to `scripts/`
4. After documentation and any skill updates are complete, consult the `opence-archive` skill to finalize the change. The skill provides guidance on pre-archive verification, running `opence archive <change-id>`, and post-archive verification.

**Reference**
- Use `opence list` to confirm change IDs before documenting.
- Use `opence skill list` to see existing skills and avoid duplicates.
- Keep documentation concise and focused on future reuse.

**Stage Complete**
When documentation and skill checkpoint are complete, report: "Compound phase complete. When ready, invoke `/opence-archive` to proceed."
<!-- OPENCE:END -->
