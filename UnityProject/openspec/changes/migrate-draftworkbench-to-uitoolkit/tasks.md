## 1. 基础设施：提取 + 新文件

- [x] [static] 1.1 提取 `StructurePreviewColorUtility`：将颜色常量（`StructurePreviewNewColor` 等 7 个 `Color`）、`GetStructurePreviewColor`、`GetStructurePreviewComponentColor`、`GetStructurePreviewChangeStatusColor`、`HasComponentType` 从 `DraftWorkbenchWindow.cs` 移动到新文件 `Assets/GameScripts/Editor/DraftWorkbench/StructurePreviewColorUtility.cs`
- [x] [static] 1.2 创建 `DraftWorkbenchWindow.uxml`：声明式布局骨架，包含双栏容器（`#split-layout` + `#stacked-layout`）、5 个 `Foldout`、所有标准控件（`TextField`、`ObjectField`、`Button`、`Toggle`、`Slider`、`PopupField`、`HelpBox`、`ListView`、`ScrollView`）
- [x] [static] 1.3 创建 `DraftWorkbenchWindow.uss`：静态样式定义（字体大小、间距、颜色、Foldout 样式、`.structure-box` 边框基础样式）
- [x] [static] 1.4 添加 `.uxml` 和 `.uss` 的 `.meta` 文件，确保 Unity 正确识别资源类型

## 2. 窗口框架：CreateGUI + 响应式布局

- [x] [repl] 2.1 将 `DraftWorkbenchWindow.OnEnable()` 改为 `CreateGUI()` 入口：用 `rootVisualElement.Add(uxmlTree.Instantiate())` 加载 UXML，用 `rootVisualElement.styleSheets.Add(uss)` 加载 USS，在 `CreateGUI` 末尾调用 `QueryAndCacheElements()` + `BindEvents()`
- [x] [repl] 2.2 实现 `QueryAndCacheElements()`：用 `UQuery` 缓存所有控件引用（button、textField、foldout、helpBox 等），替代原来的字段声明
- [x] [repl] 2.3 实现 `BindEvents()`：为所有按钮注册 `clicked` 回调，为 `ObjectField` 注册 `RegisterValueChangedCallback`，为 `TextField` 注册 `RegisterValueChangedCallback`，替代 IMGUI 的手动值比较
- [x] [repl] 2.4 实现响应式布局切换：在 `rootContainer` 上注册 `GeometryChangedEvent`，根据宽度切换 `#split-layout` / `#stacked-layout` 的 `style.display`，阈值保持原有逻辑（980px / 1400px physical）

## 3. 控件迁移：5 个 Foldout

- [x] [repl] 3.1 迁移 Settings Foldout：TextField（endpoint/model）、isPasswordField（API Key）、Toggle（JSON mode / Responses API）、IntField（timeout）、TextArea（system prompt）、Test Connection / Save Config 按钮、HelpBox（连接结果）
- [x] [repl] 3.2 迁移 Input Foldout：ObjectField（design image / target prefab）、IntegerField（canvas width/height）、[Generate] 按钮、HelpBox（生成错误）
- [x] [repl] 3.3 迁移叠加层控制：Opacity Slider、FitMode PopupField、显示/隐藏按钮
- [x] [repl] 3.4 迁移 AI Result Foldout：多行 TextField（AI 响应 JSON，可编辑）、[Parse & Preview] 按钮
- [x] [repl] 3.5 迁移 Report Foldout：转换警告列表、应用报告、绑定报告，用 `Label` 动态填充
- [x] [repl] 3.6 迁移 Prefab 切换逻辑：ObjectField 的 `RegisterValueChangedCallback` 触发 `LoadPrefab()` / `UnloadEditableInstance()`，处理首次加载跳过（防止初始化时误触发）

## 4. 结构框预览面板：VisualElement 对象池

- [x] [repl] 4.1 实现设计图渲染：用 `VisualElement.style.backgroundImage = _draftImage` + `BackgroundPropertyHelper.ConvertScaleModeToBackgroundPosition/Repeat/Size(ScaleMode.StretchToFill)` 替代 `GUI.DrawTexture`
- [x] [repl] 4.2 实现结构框对象池：`List<VisualElement> _structureBoxPool`，在每次刷新时创建不足的节点（`new VisualElement` + 设置边框和绝对定位）或回收多余的节点（`RemoveFromHierarchy()`），用 `style.left/top/width/height` 定位
- [x] [repl] 4.3 实现结构框边框：用 `style.borderLeftWidth/RightWidth/TopWidth/BottomWidth`（厚度 2px）+ `style.borderLeftColor` 等 + `style.backgroundColor = transparent` 替代 `DrawOutlineRect`
- [x] [repl] 4.4 实现 ScrollView + 内容容器：ScrollView 包含 `#preview-content` VisualElement，手动设置 content 的 `width/height` = `LayoutResult.ContentWidth/ContentHeight` 控制滚动区域
- [x] [repl] 4.5 实现缩放：手动模式下根据 `_structurePreviewZoom` 重新计算所有元素的尺寸/位置；自动模式下 zoom=1 使用 fitScale；保留 zoom slider + percentage label + Fit 按钮
- [x] [repl] 4.6 实现预览工具栏：着色模式 PopupField（按控件类型/按变更状态）、手动缩放 Toggle + Slider、节点统计 miniLabel

## 5. 节点列表 + Action 按钮

- [x] [repl] 5.1 迁移节点列表为 `ListView`：绑定 `_previewChanges` 为 `itemsSource`，`makeItem` 创建 `Label`，`bindItem` 设置节点名称、状态标记（[新建]/[已存在]/[冲突]）和颜色
- [x] [repl] 5.2 迁移节点摘要：用 `Label` 显示 "新建 N | 更新 M | 冲突 K" 统计
- [x] [repl] 5.3 迁移 Apply / Revert 按钮：`[Apply to Prefab]` 和 `[Revert Last]` 按钮，根据 `_editableInstance == null` / `_previewChanges.Count == 0` / `GetLatestApplyRecord() == null` 动态 `SetEnabled`

## 6. 异步回调改造

- [x] [static] 6.1 将 `TestConnectionAsync` 回调模式改为 `async void` + `TaskCompletionSource<bool>` 包装，直接操作 `HelpBox.text` 和 `Button.SetEnabled` 更新 UI
- [x] [static] 6.2 将 `OnGenerateClicked` 回调模式改为 `async void` + `TaskCompletionSource<string>` 包装，用 `try/catch/finally` 替代 `onSuccess`/`onError` 回调
- [x] [static] 6.3 移除所有 `Repaint()` 调用——UIToolkit 保留模式下不需要

## 7. 测试适配

- [x] [static] 7.1 更新 `DraftWorkbenchWindow_结构框颜色_控件类型模式优先控件类型并保留冲突色`：改为测试 `StructurePreviewColorUtility.GetStructurePreviewColor(StructurePreviewColorMode.ComponentType, change)`，不再反射 Window 实例
- [x] [static] 7.2 更新 `DraftWorkbenchWindow_结构框颜色_变更状态模式按新建状态着色`：改为测试 `StructurePreviewColorUtility.GetStructurePreviewColor(StructurePreviewColorMode.ChangeStatus, change)`，不再反射 Window 实例
- [x] [repl] 7.3 编译检查：运行 `dotnet build UnityProject.slnx --no-restore`，确保无编译错误（0 错误通过）
- [ ] [repl] 7.4 运行 EditMode 测试：确认所有 15 个 DraftWorkbench 测试（包括适配后的 2 个颜色测试）全部绿灯
- [ ] [manual] 7.5 手动验证：打开 DraftWorkbench 窗口，走通完整工作流（配置 AI → 选择设计图/Prefab → 生成 → 解析预览 → 结构框着色切换 → 缩放检查 → 应用/回滚），确认 UI 行为与迁移前一致
