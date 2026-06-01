## 1. Editor Workbench

- [x] 1.1 Define draft workbench metadata models for source image GUID, target prefab GUID, canvas reference resolution, overlay opacity, fit mode, and apply records.
- [x] 1.2 Add a Unity Editor entry point for opening the GameView draft workbench from the menu or prefab tooling.
- [x] 1.3 Implement image import/selection flow that creates a preview overlay for GameView without adding runtime dependencies.
- [x] 1.4 Persist draft metadata as an editor-only asset or sidecar that can be reviewed and reused across editor sessions.

## 2. GameView Prefab Generation

- [x] 2.1 Extract or expose reusable GameView prefab builder operations for creating known UGUI nodes from editor tooling.
- [x] 2.2 Add preview mode that can show proposed hierarchy and RectTransform changes without saving the prefab.
- [x] 2.3 Add apply mode that writes approved GameView UGUI hierarchy changes and records every created or modified object path.
- [x] 2.4 Ensure draft overlays and temporary preview-only objects are removed or marked EditorOnly before runtime prefab validation.

## 3. ReferenceCollector Binding

- [x] 3.1 Build a binding plan from generated/selected UGUI nodes using the project ReferenceCollector rule configuration.
- [x] 3.2 Apply ReferenceCollector entries through the shared rule service and skip duplicate or conflicting keys with warnings.
- [x] 3.3 Reuse the UI script binder text rewriter when script fields must be added for new collected references.
- [x] 3.4 Emit a binding report listing added references, skipped references, script field changes, and conflicts.

## 4. Revert and Safety

- [x] 4.1 Implement revert for the latest apply record, scoped to objects and bindings recorded by the workbench.
- [x] 4.2 Detect when a recorded object or binding has been manually changed after apply and require manual resolution instead of overwriting it.
- [x] 4.3 Ensure workbench apply/revert does not touch unrelated GameView runtime controller logic or resource loading configuration.

## 5. Tests and Validation

- [x] 5.1 Add EditMode tests for draft metadata serialization and reload.
- [x] 5.2 Add EditMode tests for preview overlay creation and cleanup.
- [x] 5.3 Add EditMode tests for GameView builder reuse, generated node naming, and RectTransform defaults.
- [x] 5.4 Add EditMode tests for ReferenceCollector binding plans, duplicate-key handling, and missing-component warnings.
- [x] 5.5 Add EditMode tests for apply report and scoped revert behavior.
- [x] 5.6 Run `openspec validate image-to-ugui-draft-workbench --strict`.
- [x] 5.7 Run the relevant Unity EditMode test subset for GameView prefab composition, ReferenceCollector rules, and UI script binding.
