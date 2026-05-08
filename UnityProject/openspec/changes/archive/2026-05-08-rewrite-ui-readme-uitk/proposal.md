## Why

`Assets/EF/EFRuntime/UI/README.md` 仍在描述已被废弃的 UGUI MVC 架构（`UIView`/`UIController`/`UIWindowDescriptor`/`IUIManager.OpenWindowAsync(prefab)`、`UILayer` 四层、基于 `INotifyPropertyChanged` 的 `BindProperty`），但实际目录中已经只剩下 UITK + MVVM 风格的新框架（`Shell`/`Screen<TViewModel>`/`Navigator`/`ScreenRegistry`/`ViewModelBase`/`ReactiveProperty`/`Region`/`LocalEventBus`）。新成员或 AI 协作者按照 README 写代码会立刻撞到 `UIView 不存在`、`OpenWindowAsync 签名不匹配` 等编译错误，文档已经成为净负担。

## What Changes

- **BREAKING（文档层）** 删除 `Assets/EF/EFRuntime/UI/README.md` 现有全部内容（UGUI MVC 章节、`UIWindowDescriptor` API、`UILayer` 四层、`BindProperty<TSource,TValue>` 示例、v2.0/v2.1 历史日志），不再保留向后兼容章节。
- 重写 README，使其与目录中实际存在的代码保持一致：
  - **架构概览**：UITK + MVVM 三层（Shell / Screen / ViewModel），`Shell` 从 `UIDocument.rootVisualElement` 解析 `screen-layer` / `popup-layer` / `system-layer` 三个命名 `VisualElement`。
  - **核心类型 API 速查**：`Shell`、`INavigator` / `Navigator`、`ScreenRegistry` / `ScreenDescriptor`、`Screen` / `Screen<TViewModel>`、`ViewModelBase` / `Prop<T>()`、`ReactiveProperty<T>` / `Changed`、`Region`、`LocalEventBus`，含每个类型的职责、签名、使用约束。
  - **生命周期**：`NavigateToAsync` 替换 ScreenLayer 的完整流程（旧 Screen 的 `OnHide` → `OnDispose` → 加载 UXML → `Activator.CreateInstance` → `LoadContent` → `Add` 到层 → `Setup(viewModel)` → `OnShow`），`PushPopupAsync` 的栈式管理 + 半透明遮罩 + 异常回滚，`PopPopup` / `Shutdown`。
  - **数据绑定模式**：以 `Screen.OnSetup` 中订阅 `ReactiveProperty<T>.Changed` 为标准模式，初始值同步用 `vm.Xxx.Value`；ViewModel 通过 `Prop<T>(initial)` 创建并自动追踪，`Dispose` 时由基类自动清理监听者。
  - **Procedure ↔ Screen 协作**：以 `MainMenuProcedure` + `MainViewModel` + `MainMenuScreen` 为示例，演示 Procedure 拥有 ViewModel、注入数据、订阅命令意图事件（如 `StartRequested`），Screen 仅做 UQuery + 绑定 + 回调转发。
  - **Root.uxml 约定**：`PanelSettings.ScaleMode` 驱动尺寸、三个层 `picking-mode="Ignore"`、`screen-layer` / `popup-layer` / `system-layer` 命名约定，UIDocument 未配 SourceAsset 时的运行时回退路径。
  - **快速开始**：从 `GameLogicEntry.InitializeNavigator` → `ScreenRegistry.Register<TScreen, TViewModel>(name, uxmlLocation)` → 流程内 `_navigator.NavigateToAsync(name, vm)` 的最小可运行路径。
  - **测试入口**：指向 `GameLogic.Tests.EditMode/Framework/` 下的 `ShellAndRegistryTests`、`ScreenLifecycleTests`、`ReactivePropertyTests`，作为活文档与回归基线。
  - **遗留说明**：`UILayer` 枚举仅供 `ReferenceCollectorScriptGenerator` 模板字符串引用，**不再被新框架使用**；`UHub/` 当前为空目录，预留给后续按窗口聚合的 UI Hub 抽象（不在本变更范围内）。
- 全文使用中文（简体），符号、类名、UQuery 名（如 `screen-layer` / `start-btn`）保持英文原样。
- 移除 `OpenWindowAsync<TView, TController>(...)` / `UIWindowDescriptor.Create<TView, TController>` / `UIBindingCollection` / `BindProperty<TSource, TValue>` / `UILayer.Background` 等旧 API 的所有示例与表格。

## Capabilities

### New Capabilities

- `ui-framework-docs`: 把"`Assets/EF/EFRuntime/UI/README.md` 必须解释的 UI 框架知识"建模为可验证的文档契约，覆盖架构概览、核心 API、生命周期、数据绑定模式、Procedure 协作示例、Root.uxml 约定、测试入口与遗留说明。后续如果新增/重命名 UI 框架类型，可以通过修改本 capability 触发 README 同步。

### Modified Capabilities

<!-- 不修改任何已存在 spec 的需求；UI 框架的运行时契约已经在前序变更 migrate-ui-to-uitoolkit 中沉淀，本变更仅引入新的文档契约。 -->

## Impact

- **唯一改动文件**：`Assets/EF/EFRuntime/UI/README.md`（整文件重写）。
- **不修改任何 `.cs` / `.asmdef` / `Packages/manifest.json` / `ProjectSettings/*`**——本变更不触发 Unity 重编译，不需要执行 `unity-compile-check`。
- **不修改 `openspec/specs/`**——纯文档同步，没有 spec 级行为变化。
- **下游影响**：
  - 阅读 README 的开发者 / AI 协作者将看到与实际代码一致的 API、生命周期、数据绑定模式，减少基于旧文档写出无法编译代码的概率。
  - `migrate-ui-to-uitoolkit` 变更归档后，本变更补齐其遗漏的"使用方文档同步"环节。
- **不影响**：运行时行为、Prefab、UXML、场景、配置表、构建产物。
