## Context

`DraftWorkbenchWindow` currently renders the entire editor UI inside a single top-level IMGUI `ScrollView`, then places the structure preview inside the same vertical flow as AI settings, input fields, JSON text, node list, apply buttons, and reports. The image preview is further constrained by `StructurePreviewMaxHeight = 360f`, which means the preview never becomes the dominant review surface even when the editor window is wide enough to support it.

That layout worked when the workbench was mainly a form-and-list workflow, but the current branch already treats the design image plus structure boxes as the primary review checkpoint before `Apply to Prefab`. Users now need to keep the preview visible while they inspect JSON output, conflicts, warnings, and generated node names. The layout must also handle portrait mobile mockups without forcing users into a tiny fit-to-strip thumbnail.

This change is intentionally scoped to the editor review experience. It does not change AI request flow, `UiStructureConverter`, `PrefabDraftBuilder`, metadata semantics, or runtime UI behavior.

## Goals / Non-Goals

**Goals:**

- Make the design-image structure preview a persistent main view during DraftWorkbench review.
- Support a wide-window split layout with workflow controls on the left and a large preview pane on the right.
- Provide a readable stacked fallback when the window is too narrow for split review.
- Replace the fixed preview height cap with sizing derived from the available preview pane.
- Support automatic fit plus an optional manual zoom inspection mode for portrait and detail-heavy drafts.
- Keep current apply/revert, overlay, JSON editing, report rendering, and metadata persistence flows intact.

**Non-Goals:**

- Do not create a second dedicated preview `EditorWindow`.
- Do not change AI prompts, JSON parsing, conversion rules, prefab apply behavior, or revert semantics.
- Do not add advanced pan/measurement tooling or a full image viewer feature set.
- Do not alter runtime prefabs, runtime assemblies, or overlay cleanup rules.

## Decisions

### 1. Use a responsive two-pane workbench layout

- **Decision:** Refactor `OnGUI()` so the default wide-window experience is a left workflow pane plus a right preview pane, while narrow widths fall back to a stacked layout in the same window.
- **Rationale:** This matches the user’s stated behavior: the preview should stay open long-term on the right, while controls remain accessible without hiding the image. A responsive fallback preserves usability in smaller docked editor layouts.
- **Alternatives considered:**
  - Keep the existing single-column flow and only raise the max preview height. This improves size slightly but still buries the preview in the scroll stack.
  - Open a separate preview window. This gives more space but increases window management overhead and breaks the “single workbench” review flow.

### 2. Give each pane its own scrolling responsibility

- **Decision:** Remove the current “everything shares one top-level scroll position” model for preview review. The workflow pane keeps its own scroll state; the preview pane uses its own layout/scroll behavior only when the image or zoom level exceeds visible bounds.
- **Rationale:** Independent scrolling prevents the preview from jumping out of view when the user edits JSON, opens reports, or expands foldouts on the control side.
- **Alternatives considered:**
  - Keep one root scroll view. This is simpler but preserves the core problem.
  - Disable scrolling in the preview entirely. This breaks portrait image inspection and any manual zoom mode.

### 3. Derive preview size from pane bounds, not a fixed max height

- **Decision:** Replace the `StructurePreviewMaxHeight`-driven sizing with preview geometry derived from the right pane’s available width and height. Automatic fit remains the default, and a lightweight manual zoom mode can override it for close inspection.
- **Rationale:** The current hard cap is the direct reason the preview remains too small. Preview sizing should respond to the pane the user gives it, not to a constant tuned for the old stacked layout.
- **Alternatives considered:**
  - Raise the constant from 360 to a larger number. This still fails in portrait-heavy cases and remains disconnected from actual pane size.
  - Stretch to fill the pane without respecting aspect ratio. This would distort the draft image and make box validation unreliable.

### 4. Keep node summaries and apply actions in the workflow pane

- **Decision:** The preview pane focuses on image review, preview status, and preview-specific controls (such as color mode and zoom). Node lists, warnings, reports, and Apply/Revert controls stay in the workflow pane.
- **Rationale:** This keeps the right pane visually clean and preserves a single mental model: left side edits and decides, right side validates alignment.
- **Alternatives considered:**
  - Move node list and reports under the preview. This competes with the image for vertical space and weakens the “persistent large preview” goal.

### 5. Make preview math testable via extracted helper logic

- **Decision:** Pull the pane-size/fit calculation into small helper methods or data helpers that can be covered by edit-mode tests, while leaving the final IMGUI drawing code in `DraftWorkbenchWindow`.
- **Rationale:** The visual split itself is best validated manually in the editor, but the scale and aspect-ratio rules are deterministic enough to test.
- **Alternatives considered:**
  - Leave all math inline inside `DrawStructurePreview()`. Faster initially, but harder to verify and maintain.

## Risks / Trade-offs

- **[Risk]** Wide/small pane thresholds feel wrong on different docked editor layouts → **Mitigation:** choose a simple breakpoint and verify both docked and floating window widths manually.
- **[Risk]** Manual zoom makes very large portrait images exceed the pane → **Mitigation:** keep automatic fit as the default and let the preview pane scroll only when zoomed content exceeds bounds.
- **[Risk]** Refactoring `OnGUI()` could accidentally disturb foldout/report behavior → **Mitigation:** keep existing section-drawing methods where possible and move only the layout orchestration plus preview-specific controls.
- **[Trade-off]** This improves inspection but stops short of a full graphics viewer → **Mitigation:** explicitly keep pan/measurement tooling out of scope for this pass.

## Migration Plan

1. Refactor the root IMGUI layout into pane orchestration without changing existing parsing/apply entry points.
2. Extract preview sizing helpers and update `DrawStructurePreview()` to consume pane bounds instead of a fixed maximum height.
3. Add or update editor tests for fit/zoom sizing rules and empty-state selection.
4. Verify wide-window, narrow-window, portrait-image, and empty-preview workflows in the Unity editor before merging.

Rollback is straightforward: revert `DraftWorkbenchWindow` layout and helper changes. No data migration or runtime asset migration is required.

## Open Questions

- The optional manual zoom can be a small preset selector (`Fit` / `100%` / `150%` / `200%`) or a free slider. Presets are simpler; implementation can choose either as long as fit remains the default.
- If future review needs grow beyond zoom, a separate preview window can still be introduced later, but that is intentionally deferred.
