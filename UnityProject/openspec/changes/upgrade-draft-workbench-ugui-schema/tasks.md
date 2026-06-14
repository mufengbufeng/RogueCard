## 1. Schema DTOs and Compatibility

- [ ] [tdd] 1.1 Add tests that deserialize `fixed` and `stretch` `rect` blocks with Unity-native fields and default missing optional values.
- [ ] [tdd] 1.2 Extend `UiStructureSchema` DTOs to include `rect`, `layout`, `layoutElement`, `safeArea`, `scroll`, `contentSizeFitter`, and `aspectRatioFitter` blocks.
- [ ] [tdd] 1.3 Add legacy payload tests for old `position`, `align`, `vAlign`, and `offset` fields loading without hard failure.
- [ ] [tdd] 1.4 Implement schema-version or normalization markers and warning data needed to distinguish upgraded legacy payloads from native rect-centric payloads.

## 2. Rect Conversion Semantics

- [ ] [tdd] 2.1 Add converter tests for `fixed` rect nodes mapping `anchorPreset`, `anchoredPosition`, `sizeDelta`, and `pivot` into descriptor RectTransform intent.
- [ ] [tdd] 2.2 Add converter tests for `stretch` rect nodes mapping `anchorPreset`, `inset`, optional single-axis `sizeDelta`, and `pivot` into descriptor RectTransform intent.
- [ ] [tdd] 2.3 Implement anchor preset normalization and deterministic `anchorMin` / `anchorMax` conversion for all supported presets.
- [ ] [tdd] 2.4 Implement legacy rect fallback conversion with explicit upgrade warnings for old pixel-box schema fields.

## 3. Layout and Advanced UI Semantics

- [ ] [tdd] 3.1 Add converter tests for `horizontal`, `vertical`, and `grid` layout container blocks preserving padding, spacing, child alignment, and grid constraints.
- [ ] [tdd] 3.2 Add converter tests for layout child nodes producing `LayoutElement` sizing hints instead of free manual placement.
- [ ] [tdd] 3.3 Add converter tests for `safeArea`, `scroll`, `ContentSizeFitter`, and `AspectRatioFitter` blocks preserving component intent and warning on invalid combinations.
- [ ] [tdd] 3.4 Extend `UguiNodeDescriptor` and conversion output to carry rect, layout group, layout element, safe-area, scroll-view, and fitter component intent deterministically.

## 4. Validation, Warnings, and Stable Output

- [ ] [tdd] 4.1 Add tests for duplicate sibling names producing stable renamed paths and conversion warnings.
- [ ] [tdd] 4.2 Add tests for unknown node types and unsupported field combinations continuing conversion where safe while reporting deterministic warnings.
- [ ] [tdd] 4.3 Add repeated-conversion tests proving descriptor order, hierarchy paths, renamed nodes, and warnings stay stable for identical JSON input.
- [ ] [tdd] 4.4 Implement or update conversion report models so preview, metadata, and apply can consume the same validation and warning set.

## 5. AI Prompt, Parsing, and Metadata

- [ ] [tdd] 5.1 Add tests that the AI request prompt describes the rect-centric schema and no longer centers the contract on `position`, `align`, or `vAlign`.
- [ ] [tdd] 5.2 Update `AiServiceConfig` and `AiVisionClient` prompt/response handling to request and parse the new rect-centric schema.
- [ ] [tdd] 5.3 Add draft metadata tests proving raw AI JSON, schema version or normalization marker, converted descriptors, and validation messages are persisted and reloaded.
- [ ] [tdd] 5.4 Implement metadata compatibility so older `.draft.meta.asset` sessions load with safe defaults and visible upgrade warnings.

## 6. Workbench Preview and Prefab Apply

- [ ] [tdd] 6.1 Add workbench tests proving preview and apply use the same normalized converted descriptor set for unchanged JSON.
- [ ] [tdd] 6.2 Update `DraftWorkbenchWindow` review UI to surface schema validation errors and conversion warnings before apply.
- [ ] [tdd] 6.3 Add prefab builder tests for applying fixed rects, stretch rects, layout groups, layout elements, fitters, safe-area intent, scroll-view structure, and grid layout settings.
- [ ] [tdd] 6.4 Update `PrefabDraftBuilder` to write Unity-native RectTransform and component configuration without adding runtime-only draft helper dependencies.

## 7. Verification and Rollout

- [ ] [static] 7.1 Ensure all new editor-only code stays under editor assemblies and does not add runtime HotFix or YooAsset bundle dependencies.
- [ ] [repl] 7.2 Run the closest Unity editor test suite or available batchmode test command covering DraftWorkbench and converter tests.
- [ ] [repl] 7.3 Run OpenSpec strict validation for `upgrade-draft-workbench-ugui-schema` after implementation updates.
- [ ] [manual] 7.4 Manually review a representative AI JSON payload in the workbench to confirm warnings, preview bounds, and prefab apply behavior are understandable.
