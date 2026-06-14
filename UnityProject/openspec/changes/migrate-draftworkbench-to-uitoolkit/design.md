## Context

DraftWorkbenchWindow 是项目中最大的 IMGUI EditorWindow（~1763 行），所有 UI 逻辑集中在 `OnGUI()` 中。项目中尚无 UIToolkit 编辑器窗口先例。此变更是纯技术迁移：将渲染层从 IMGUI 切换到 UIToolkit，保持所有功能行为不变。

DraftWorkbench 模块共 10 个文件，仅 `DraftWorkbenchWindow.cs` 依赖 IMGUI。其余 9 个文件（数据模型、业务逻辑、Prefab 操作、AI 调用）与 UI 框架无关，完全不需要修改。

## Goals / Non-Goals

**Goals:**
- 将 DraftWorkbenchWindow 的 UI 渲染从 IMGUI 迁移到 UIToolkit（UXML + USS + C# 元素树）
- 保持所有现有功能行为、用户交互流程和视觉表现一致
- 提取 `StructurePreviewColorUtility` 使结构框颜色逻辑可独立测试
- 现有 15 个 EditMode 测试全部保持绿灯

**Non-Goals:**
- 不修改 DraftOverlayService（操作 Scene GameObject，属于 UGUI 域，与 EditorWindow 渲染无关）
- 不增加新功能、不改变 API、不调整 UI 交互流程
- 不在此变更中修改 AI 生成逻辑或 Prefab 构建逻辑
- 不引入第三方 UIToolkit 扩展库

## Decisions

### 决策 1：结构框预览用 VisualElement 对象池 + 动态 style，而非 IMGUIContainer

**选择**：为每个结构框创建 `VisualElement` 节点，通过 `style.left/top/width/height` 绝对定位，通过 `style.border*` 绘制彩色边框。设计图通过 `style.backgroundImage` 渲染。对象池管理节点数随 `_previewChanges` 动态变化。

**备选方案及淘汰理由**：

| 方案 | 淘汰理由 |
|------|---------|
| IMGUIContainer 嵌入 | 保留 IMGUI 依赖，与"统一 UIToolkit 技术栈"目标矛盾 |
| Painter2D / generateVisualContent | 不支持纹理绘制，API 成熟度不足 |

**缩放实现**：手动模式通过重新计算所有 `VisualElement` 的尺寸和位置实现，不使用 CSS `scale` transform（后者会导致 border-width 视觉不一致）。

**结构框对象池**：维护 `List<VisualElement> _structureBoxPool`。当 `drawableChanges.Count` 变化时，创建不足的节点或回收多余的节点（`RemoveFromHierarchy()` + 保留引用复用）。

### 决策 2：双栏/单栏布局用显示切换，而非重建元素树

**选择**：UXML 中同时定义双栏和单栏两种布局容器。通过 `GeometryChangedEvent` 监听窗口宽度变化，切换两个容器的 `style.display`（Flex/None）。Foldout 等共享控件在 C# 层持有引用，通过 `RemoveFromHierarchy()` + `Add()` 在两种布局间移动，防止状态丢失。

**备选方案及淘汰理由**：

| 方案 | 淘汰理由 |
|------|---------|
| CSS Media Query | UIToolkit 的 Media Query 支持有限，且无法处理 DPI 相关逻辑 |
| 单布局 + flex-wrap | 无法实现双栏/单栏两种完全不同的布局结构 |

**切换阈值**：保持原有逻辑（logical 980px / physical 1400px）。

### 决策 3：USS 样式分离 + C# 内联动态样式混合

**选择**：静态样式（颜色常量、间距、字体大小、Foldout 样式）用 USS 定义。动态样式（结构框颜色、运行时尺寸、显示/隐藏）用 C# 直接设置 `style.*` 属性。结构框预览的颜色常量保留在 C# `static readonly Color` 字段中。

**备选方案及淘汰理由**：

| 方案 | 淘汰理由 |
|------|---------|
| 全 USS | 结构框颜色和动态尺寸无法预定义，必须在运行时计算 |
| 全 C# 内联 | 失去样式分离的维护性优势，代码冗长 |

### 决策 4：异步回调改用 async/await + TaskCompletionSource 模式

**选择**：将 `AiVisionClient.GenerateUiStructure(callback)` 的回调模式包装为 `Task<T>`，在 Window 中使用 `async void` 方法 + `try/catch/finally`。UI 状态更新直接操作元素属性，不需要 `Repaint()`。

```csharp
// 迁移前 (IMGUI)
_onSuccess: response => { _aiResponse = response; Repaint(); }

// 迁移后 (UIToolkit)
var response = await GenerateAsync();
_aiResponse = response;
aiResultTextField.value = response; // 直接更新元素
```

**备选方案及淘汰理由**：

| 方案 | 淘汰理由 |
|------|---------|
| 保持回调 + `MarkDirtyRepaint()` | 回调模式嵌套深，错误处理困难 |

### 决策 5：颜色逻辑提取为独立 Utility 类

**选择**：将 `GetStructurePreviewColor`、`GetStructurePreviewComponentColor`、`GetStructurePreviewChangeStatusColor`、`HasComponentType` 等私有方法提取到新的 `internal static class StructurePreviewColorUtility`。颜色常量也移到该 Utility 类。

**理由**：
- 测试不需要反射 `EditorWindow` 实例
- 颜色逻辑与 UI 渲染解耦
- 遵循单一职责原则

## Risks / Trade-offs

### [风险] 边框在不同 zoom 倍率下的视觉一致性

当 `_structurePreviewZoom < 1.0` 时，2px 边框在极小结构框上可能过粗。当前 IMGUI 版本也存在同样的问题（固定 `StructurePreviewBorderWidth = 2f`），因此视为行为保持而非回归。如果后续需要优化，可在 zoom < 0.5 时将 border-width 降至 1px。

### [风险] UIToolkit ScrollView 嵌套行为

工作流区域有外层 ScrollView，节点列表有 ListView（自带滚动），结构框预览也有 ScrollView。需要验证 Unity 6 UIToolkit 的嵌套 ScrollView 滚轮事件传播行为是否与 IMGUI 一致。如果出现冲突（外层抢走滚轮事件），可在结构框预览的 ScrollView 上注册 `WheelEvent` 并调用 `StopPropagation()`。

### [风险] BackgroundPropertyHelper API 稳定性

`BackgroundPropertyHelper` 是 Unity 6 新增的 API（替代被废弃的 `unityBackgroundScaleMode`），API 文档标注为 `UnityEngine.UIElements` 命名空间下的公共方法。如果在 Unity 6.1 中发生 breaking change，需重新适配。

### [取舍] UXML 运行时加载 vs 编译时嵌入

**选择**：运行时 `AssetDatabase.LoadAssetAtPath<VisualTreeAsset>()` 加载 UXML。对应 USS 用 `StyleSheet` 同样方式加载。

**原因**：支持热修改 UXML/USS 即时预览（不需重编译 C#），适合编辑器工具开发。缺点是首次加载有 IO 开销，但编辑器工具场景下可忽略。

### [取舍] ObjectField 变更检测方式

UIToolkit 的 `ObjectField` 通过 `RegisterValueChangedCallback<ChangeEvent<Object>>` 实现值变更检测，替代 IMGUI 中手动 `if (newPrefab != _prefabAsset)` 比较。回调中执行 `LoadPrefab()` / `UnloadEditableInstance()` 等重量操作。需确保回调不因初始化赋值而错误触发（通过 flag 或首次跳过）。
