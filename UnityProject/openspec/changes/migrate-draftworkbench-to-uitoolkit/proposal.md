## Why

DraftWorkbench 当前使用 IMGUI（OnGUI）实现编辑器窗口 UI，所有布局逻辑、控件创建、样式管理集中在一个 ~1763 行的 `DraftWorkbenchWindow.cs` 中。IMGUI 的立即模式渲染模型导致代码难以维护：控件状态散落在字段中、布局与逻辑耦合、自定义绘制需要大量低级 API。迁移到 UIToolkit（Unity 现代编辑器 UI 框架）可获得声明式布局（UXML）、样式分离（USS）、保留模式的元素树、以及更好的可测试性。

## What Changes

- **新增** `DraftWorkbenchWindow.uxml`：声明式 UI 布局，分离结构定义
- **新增** `DraftWorkbenchWindow.uss`：样式表，分离视觉样式
- **新增** `StructurePreviewColorUtility.cs`：从 Window 中提取结构框颜色逻辑，支持独立单元测试
- **重写** `DraftWorkbenchWindow.cs`：用 `CreateGUI()` + UIToolkit 元素树替代 `OnGUI()`，保留所有业务方法不变
- **更新** `DraftWorkbenchTests.cs`：2 个颜色相关测试改为测试 `StructurePreviewColorUtility`，不再反射 Window 实例

## Capabilities

### New Capabilities

无。此变更是纯实现技术迁移，不引入新功能。

### Modified Capabilities

- `image-to-ugui-draft-workbench`：**无需求变更**。所有 Requirement 和 Scenario 保持完全不变。此变更仅影响 editor 窗口的渲染技术栈，不改变任何功能行为、API 契约或用户可见行为。

## Impact

- **受影响的代码**：仅 `DraftWorkbenchWindow.cs`（IMGUI → UIToolkit）、`DraftWorkbenchTests.cs`（2 个测试适配）
- **新增文件**：`DraftWorkbenchWindow.uxml`、`DraftWorkbenchWindow.uss`、`StructurePreviewColorUtility.cs`
- **不受影响的代码**：`DraftWorkbenchModels.cs`、`DraftWorkbenchMetadata.cs`、`PrefabDraftBuilder.cs`、`DraftOverlayService.cs`、`DraftBindingService.cs`、`UiStructureSchema.cs`、`UiStructureConverter.cs`、`AiVisionClient.cs`、`AiServiceConfig.cs`（全部无 IMGUI 依赖，无需修改）
- **依赖**：Unity 6 内置 UIToolkit（`UnityEngine.UIElements`），无额外 package 依赖
- **风险**：结构框预览的自定义绘制（设计图 + 结构框描边）是迁移难点，需用 `VisualElement` + 动态 `style` 属性替代原有 `GUI.DrawTexture` / `EditorGUI.DrawRect` 低级 API
