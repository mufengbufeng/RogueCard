## Why

The draft workbench currently speaks a design-box schema that centers on `position`, `align`, and `vAlign`, while the downstream prefab workflow increasingly depends on Unity UGUI’s native RectTransform semantics. We need a single contract that lets AI output, schema parsing, preview rendering, descriptor conversion, and prefab apply all agree on the same layout language before the tool grows further.

## What Changes

- Upgrade `ui_structure.json` from the old pixel-box model to a rect-centric schema built around Unity-native layout semantics.
- Define `rect` as the core layout contract, with explicit `fixed` and `stretch` modes.
- Standardize fixed-node data on `anchorPreset`, `anchoredPosition`, `sizeDelta`, and `pivot`.
- Standardize stretch-node data on `anchorPreset` plus `inset`, with optional single-axis `sizeDelta` where Unity semantics require it.
- Add structured support for `layout`, `layoutElement`, `ContentSizeFitter`, `AspectRatioFitter`, `SafeArea`, `ScrollView`, and `GridLayoutGroup`-style output.
- Require the AI prompt, schema DTOs, converter, preview, metadata persistence, and prefab apply path to stay synchronized around the new schema.
- Preserve reviewable warnings, deterministic conversion output, and compatibility for older draft metadata and legacy AI responses where feasible.

## Capabilities

### New Capabilities

- `ui-structure-converter`: Defines rect-centric AI schema parsing, validation, legacy fallback handling, and deterministic conversion into Unity-oriented descriptor data.

### Modified Capabilities

- `image-to-ugui-draft-workbench`: Updates the workbench workflow to generate, review, persist, preview, and apply the new rect-centric schema while surfacing validation and conversion warnings to the editor user.

## Impact

- Affected editor code: `Assets/GameScripts/Editor/DraftWorkbench/UiStructureSchema.cs`, `UiStructureConverter.cs`, `AiServiceConfig.cs`, `AiVisionClient.cs`, `DraftWorkbenchModels.cs`, `DraftWorkbenchWindow.cs`, `PrefabDraftBuilder.cs`, and related tests.
- Affected tests: draft workbench editor tests, UI structure converter tests, AI prompt/response parsing tests, and compile-time/editor verification.
- Runtime impact: none expected; all work remains editor-only and MUST NOT add runtime HotFix or YooAsset bundle dependencies.
- Data impact: existing `.draft.meta.asset` data and legacy AI JSON should continue to load with safe defaults or explicit upgrade warnings rather than becoming unreadable.
