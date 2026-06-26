## Context

The draft workbench already stores AI response JSON, converts it into `UguiNodeDescriptor` data, previews generated bounds, and applies prefab changes through an editor-only workflow. The missing piece is a stable contract for the AI layout schema: the current model is still centered on `position`, `align`, and `vAlign`, while the real prefab pipeline increasingly depends on Unity UGUI concepts such as anchor presets, stretch insets, pivot, layout groups, fitters, and scroll/container semantics.

This change upgrades the schema itself, not just the prompt wording. The workbench, DTOs, converter, metadata, preview, and apply path all need to describe the same Unity-native layout intent. The result must remain editor-only, preserve older metadata where possible, and keep the preview/apply/revert loop deterministic and reviewable.

## Goals / Non-Goals

**Goals:**

- Define a rect-centric `ui_structure.json` contract that mirrors Unity UGUI layout semantics closely enough to generate understandable prefab output.
- Split node rect behavior into explicit `fixed` and `stretch` modes.
- Keep the converter as a deterministic, testable step that outputs Unity-oriented descriptor data before any prefab mutation occurs.
- Support higher-level Unity layout concepts needed by real UI screens: `layout`, `layoutElement`, `ContentSizeFitter`, `AspectRatioFitter`, `SafeArea`, `ScrollView`, and grid layout semantics.
- Preserve reviewable warnings, preview/apply consistency, and compatibility for existing draft metadata and legacy AI responses.

**Non-Goals:**

- Implement runtime UI generation from AI output.
- Add asset slicing/import automation or infer production sprite bindings automatically.
- Guarantee perfect visual parity with the source design image; the tool provides a high-quality Unity starting point for review and iteration.
- Change runtime UI loading, binding, or HotFix assembly boundaries.

## Decisions

### 1. Make `rect` the canonical layout block

Every non-layout-only node will describe its primary RectTransform intent through a `rect` object rather than through scattered top-level positioning fields. `rect` will carry the mode (`fixed` or `stretch`) and the Unity-native data needed by that mode.

- **Fixed mode** uses `anchorPreset`, `anchoredPosition`, `sizeDelta`, and `pivot`.
- **Stretch mode** uses `anchorPreset`, `inset`, optional single-axis `sizeDelta`, and `pivot`.

This keeps the schema aligned with how Unity users read the Inspector and makes converter math more explicit.

**Alternative considered:** continue with `position/align/vAlign/offset` and translate later. Rejected because it preserves the current semantic drift between prompt language and prefab output.

### 2. Use Unity-native field names in the AI contract

The schema will prefer Unity terms such as `anchoredPosition`, `sizeDelta`, `pivot`, `inset`, `layoutElement`, and fitter/layout names instead of a hybrid vocabulary. The AI prompt will teach these fields directly so the returned JSON is as close as possible to the data the converter needs.

**Alternative considered:** keep AI-friendly aliases and normalize internally. Rejected because it creates one more translation layer and makes editor-side review harder.

### 3. Separate RectTransform intent from component/layout intent

Node layout data will be organized into composable blocks:

- `rect` for RectTransform intent
- `layout` for container semantics (`horizontal`, `vertical`, `grid`)
- `layoutElement` for child sizing hints inside layout containers
- `fitters` for `ContentSizeFitter` / `AspectRatioFitter`
- `safeArea` for safe-area application intent
- `scroll` for ScrollView-specific behavior

This prevents the rect contract from becoming overloaded and lets the converter emit richer descriptor/component intent without guessing from unrelated fields.

**Alternative considered:** flatten all optional fields into one giant node object. Rejected because it obscures which fields belong together and makes partial validation much harder.

### 4. Restrict layout children to size and minor alignment hints

When a parent node declares `layout`, its children will not use free-form manual placement. Child nodes may still carry size-related information and limited alignment/sizing hints that map into `LayoutElement` or cross-axis alignment, but the parent layout container remains authoritative for final placement.

**Alternative considered:** allow full `anchoredPosition` and `inset` on layout children. Rejected because it conflicts with Unity’s own layout behavior and would make preview/apply inconsistency likely.

### 5. Model ScrollView and grid behavior as structured semantics

`ScrollView` and grid layouts are not just ordinary containers. The schema will represent their intent explicitly through `scroll` and `layout.type = grid` blocks so the converter can emit the right component and child-container expectations instead of treating them as generic images or panels.

**Alternative considered:** infer scroll/grid intent from naming conventions or nested containers. Rejected because AI output would become ambiguous and brittle.

### 6. Keep conversion pure; extend descriptor intent instead of mutating prefabs early

`UiStructureConverter` remains a pure conversion/validation stage. It will parse schema blocks, validate unsupported combinations, produce deterministic descriptor/component intent, and attach warnings. Prefab creation and mutation remain in the builder/apply path.

This keeps tests focused and allows the workbench preview, JSON editor, and apply report to all use the same converted result.

**Alternative considered:** build prefabs directly during conversion. Rejected because it mixes validation, preview, and mutation concerns.

### 7. Preserve compatibility through explicit fallback behavior

Legacy draft metadata and older AI responses may still contain top-level `position`, `align`, `vAlign`, or other pre-rect fields. The new DTOs and converter should continue to load those responses when feasible, normalize them into the new descriptor model, and surface upgrade warnings instead of failing silently.

**Alternative considered:** only accept the new schema after the change lands. Rejected because it would make previously saved draft sessions much less useful.

## Risks / Trade-offs

- **[Risk]** The expanded schema becomes too large for stable AI output -> **Mitigation:** organize the prompt around reusable blocks and validate required-vs-optional combinations with focused tests.
- **[Risk]** Unity-native terminology improves fidelity but may increase malformed AI output initially -> **Mitigation:** keep validation messages explicit in the workbench and preserve raw JSON for manual correction.
- **[Risk]** Layout, fitters, and grid/scroll semantics can produce conflicting combinations -> **Mitigation:** document legal combinations in specs, warn on unsupported mixes, and keep converter output deterministic.
- **[Risk]** Backward compatibility with legacy JSON complicates DTOs -> **Mitigation:** isolate fallback handling in schema normalization and mark upgraded data paths with warnings.
- **[Trade-off]** A richer schema gives better prefab intent but increases implementation/test scope -> **Mitigation:** keep prefab mutation logic separate and stage the behavior behind testable converter rules.

## Migration Plan

1. Update the OpenSpec contract so the new schema and workbench behavior are explicit.
2. Extend DTOs and metadata with rect-centric and semantic layout blocks while preserving legacy load paths.
3. Update the AI prompt and response parsing to request and validate the new schema.
4. Extend the converter/descriptors to emit Unity-native rect, layout, fitter, safe-area, and scroll/grid intent.
5. Add and update tests for full-schema, sparse, invalid, and legacy payloads plus preview/apply consistency checks.
6. Run compile/test verification before implementation is declared ready.

Rollback remains editor-only: revert the schema/prompt/converter/metadata changes and keep legacy metadata fields readable. Runtime prefabs and HotFix assemblies are not directly migrated by this design.

## Open Questions

- Which subset of Unity `anchorPreset` names should the first prompt expose directly versus normalize internally?
- Should scroll-related macro structure be represented by a dedicated node type such as `scroll-view`, or by `type: container` plus a `scroll` block?
- How aggressively should the converter attempt automatic asset lookup for image references, given that asset auto-mapping remains out of scope?
