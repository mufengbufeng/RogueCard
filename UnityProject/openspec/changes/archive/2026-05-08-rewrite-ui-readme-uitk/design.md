## Context

`Assets/EF/EFRuntime/UI/` 在 `migrate-ui-to-uitoolkit` 变更中已经从 UGUI MVC 全量切换到 UITK + MVVM 风格的新框架，目录中目前只剩下：

- `Shell.cs` —— 解析 `UIDocument.rootVisualElement` 中的 `screen-layer` / `popup-layer` / `system-layer` 三个命名 `VisualElement`。
- `INavigator.cs` / `Navigator.cs` —— `NavigateToAsync` 替换 ScreenLayer 内容；`PushPopupAsync` 栈式管理 + 半透明遮罩；`PopPopup` / `Shutdown`。
- `ScreenRegistry.cs` —— `Register<TScreen, TViewModel>(name, uxmlLocation, isPopup)`，`ScreenDescriptor` 持有 `Name` / `Location` / `ScreenType` / `ViewModelType` / `IsPopup`。
- `Screen.cs` —— 非泛型 `Screen : VisualElement`（供 `Navigator` 引用避免泛型协变）+ 泛型 `Screen<TViewModel>`，子类重写 `OnSetup` 做 UQuery + 绑定。
- `ViewModelBase.cs` —— `Prop<T>(initial)` 创建并自动追踪 `ReactiveProperty`，`Dispose` 时一次性 `ClearListeners`。
- `ReactiveProperty.cs` —— `Value` setter 仅在不等时触发 `Changed?.Invoke(value)`，`ClearListeners` 把事件置空。
- `Region.cs` —— 在 Screen 内部预留可切换内容插槽，支持 `ShowAsync(uxmlLocation)` / `Show(VisualElement)` / `Clear`。
- `LocalEventBus.cs` —— 实现 `IEventPublisher`，复用 `EF.Event.EventChannel<T>`，`Dispose` 时释放所有 channel。
- `UILayer.cs` —— **遗留枚举**，仅供 `ReferenceCollectorScriptGenerator` 模板字符串引用，新框架不消费。
- `UHub/` —— 当前空目录，未来按窗口聚合 UI Hub 的占位。

但是 `README.md` 仍在描述 `UIView` / `UIController` / `UIWindowDescriptor` / `IUIManager.OpenWindowAsync(prefab)` / `BindProperty<TSource,TValue>` 等已被删除的 API；按照 README 写代码会立刻撞到编译错误。

下游使用方（如 `GameLogicEntry.InitializeNavigator`、`MainMenuProcedure` + `MainMenuScreen` + `MainViewModel`、`ShellAndRegistryTests` / `ScreenLifecycleTests` / `ReactivePropertyTests`）已经按新框架运行，README 是当前项目里**唯一仍在传播过时心智模型的 UI 资料**。

## Goals / Non-Goals

**Goals:**

- README 与 `Assets/EF/EFRuntime/UI/` 目录下的真实代码 1:1 对齐：所有出现的类型、方法签名、字段名、UQuery 名（如 `screen-layer` / `start-btn`）必须能通过 grep 在源码中命中。
- 新成员或 AI 协作者只读 README 就能完成"注册 Screen → 在 Procedure 创建 ViewModel → `NavigateToAsync` 打开界面 → 绑定 ReactiveProperty + 命令意图"的完整最小路径。
- 把"复用工程内的真实示例"作为一等约束：所有代码片段优先来自 `MainMenuScreen` / `MainViewModel` / `MainMenuProcedure` / `GameLogicEntry.InitializeNavigator` / `Root.uxml`，不写新的"演示用 ShopController" 这类合成示例。
- 明确把 `UILayer` 枚举与空 `UHub/` 目录标注为"遗留 / 占位"，避免读者误以为是当前架构的一部分。

**Non-Goals:**

- 不修改任何 `.cs` / `.uxml` / `.asmdef` / `Packages/manifest.json` / `ProjectSettings/*`。
- 不引入新的运行时 API、不改造 `Navigator` / `Shell` / `ViewModelBase` 等任何契约。
- 不在 README 里描述未来路线图（除以单行"占位"形式提到 `UHub/` 外，不展开多窗口共栈、跨场景导航、UI 池化等设想）。
- 不重写 `migrate-ui-to-uitoolkit` 已经归档的 spec / proposal；本变更只引入新的文档 capability。
- 不调整其他子模块（`Resource` / `Event` / `Model` / `Procedure`）的 README。

## Decisions

### 决策 1：把 README 建模为"文档 capability"而非纯文档任务

**选择**：新增 `ui-framework-docs` capability，用 `### Requirement` + `#### Scenario` 描述 README 必须覆盖的章节与不变量，spec 文件作为契约。

**理由**：

- OpenSpec 工作流里 `tasks` 硬依赖 `specs`，纯文档变更如果不创建 spec 就无法走通归档流程。
- 把章节列表写成可验证场景（"WHEN 读者搜索 Navigator THEN README 必须含 NavigateToAsync 示例"），后续如果新增 UI 类型，可以通过反向检查 README 是否仍满足这些场景，把"文档过期"问题暴露为 spec 不一致。
- 这一思路与 `keep README aligned with code` 的常见 lint 规则一致，零额外工具成本。

**替代方案**：

- _仅写 design + tasks，不写 spec_：会让 `tasks` 永远 `blocked`，无法 `/opsx:archive`，与项目工作流冲突。
- _把章节描述塞进 design.md_：design 不会被 `openspec sync-specs` 同步到 `openspec/specs/`，未来无法被其他变更引用或扩展。

### 决策 2：示例代码全部复用工程现有真实代码

**选择**：README 中的代码片段必须是 `Assets/GameScripts/HotFix/GameLogic/UI/Main/MainMenuScreen.cs` / `MainViewModel.cs` / `Procedure/Main/MainMenuProcedure.cs` / `GameLogicEntry.cs::InitializeNavigator` / `Assets/AssetRaw/UI/Root.uxml` 中的逐字片段（可适当裁剪注释，但标识符与签名保持原样）。

**理由**：

- 旧 README 的"`PlayerModel` / `ShopController` / `InventoryView`"示例与项目里完全没有对应文件，读者无法追溯执行路径。复用真实代码后，读者可以直接 `Ctrl+点击` 进入源码继续阅读。
- 当后续重构修改 `MainMenuScreen` 等真实代码时，CI/Code Review 可以同步要求更新 README，避免"示例代码与实际代码漂移"。
- 真实代码已经覆盖了 README 想表达的所有重点：`OnSetup` 中的 UQuery、`ReactiveProperty.Changed` 订阅、`SetEnabled` / `RegisterCallback<ClickEvent>`、Procedure 内 `vm.StartRequested += ...` + `NavigateToAsync(name, vm)`。

**替代方案**：

- _写抽象的 `class FooScreen : Screen<FooViewModel>` 占位代码_：可以减少代码长度，但读者无法知道项目里"开始游戏 → 切换 Procedure"这种真实流程的写法，仍要再去翻代码，价值低。

### 决策 3：用一张表格说明各类型职责，避免散文讲解

**选择**：在 `## 核心组件` 章节用 `| 类型 | 职责 | 关键 API | 约束 |` 表格依次列出 `Shell` / `INavigator` / `Navigator` / `ScreenRegistry` / `ScreenDescriptor` / `Screen` / `Screen<TViewModel>` / `ViewModelBase` / `ReactiveProperty<T>` / `Region` / `LocalEventBus`。

**理由**：

- 旧 README 的"块状 API 列表"在这次重写后仍有 11 个类型，散文化叙述会显著拉长，表格能让 grep 友好、Diff 友好。
- 表格之后再用小节展开**仅有非显而易见行为的类型**（如 `Screen` 必须用非泛型基类避免协变 `InvalidCastException`、`Navigator.PushPopupAsync` 异常路径回滚 overlay、`ViewModelBase.Prop<T>` 自动追踪并在 `Dispose` 时清理），其余直接交给源码注释。

**替代方案**：

- _按字母顺序逐个 H3 章节展开_：每个类型独占一节会让短小类型（如 `ReactiveProperty`）章节空洞，读者翻页成本高。

### 决策 4：生命周期用一张 ASCII 图描述，并显式标注异常路径

**选择**：在 `## 生命周期` 章节给出一张 ASCII 流程图，覆盖 `NavigateToAsync` 全流程（含 `OnHide` → `OnDispose` 旧 Screen → 加载 UXML → `Activator.CreateInstance` → `LoadContent` → `Add` 到层 → `Setup(viewModel)` → `OnShow`）和 `PushPopupAsync` 全流程（含 overlay 创建 + 异常时 `RemoveFromHierarchy` 回滚），并在 `Shutdown` 子节说明"逐个 try/catch + 清空三层"。

**理由**：

- `Navigator.cs` 的实现里有几个非显而易见的细节：`Activator.CreateInstance(descriptor.ScreenType)` 之后立刻 `LoadContent` 再 `Setup`、`PushPopupAsync` 用 try/catch 包裹后 `RemoveFromHierarchy`、`Shutdown` 用 `try { } catch { }` 兜底。这些在错误处理或扩展时会被反复查阅，文档化成本远低于让读者每次重读源码。

**替代方案**：

- _省略异常路径_：会让"PushPopupAsync 失败时谁负责清理 overlay"成为重复问题，第一次踩坑时容易污染 PopupLayer。

### 决策 5：明确 README 文件长度上限

**选择**：重写后的 README **目标 ≤ 350 行**，硬上限 ≤ 500 行。超过上限的内容（如完整 API 文档、未来路线图）由源码注释或后续 spec 承担。

**理由**：

- 旧 README 815 行，绝大多数体量来自三级"基本配置 / 完整控制 / 传统方式"重复示例与 `v2.0 / v2.1` 历史日志，签名变更后这些章节会全部作废。
- 短 README 让 AI 协作者可以一次性放进上下文，减少幻觉。

**替代方案**：

- _不设上限_：旧 README 走过的弯路就是没有上限导致重复堆叠，最终没人维护。

## Risks / Trade-offs

- **[风险] README 中复用的真实代码片段未来会随 `MainMenuScreen` / `MainViewModel` 重构而漂移**
  → 缓解：在 `ui-framework-docs` spec 的"示例溯源"requirement 里把"示例代码必须能在仓库中通过 grep 命中相同标识符"作为可验证场景；后续重构这些类型的变更需要同步重写 README。
- **[风险] 把 README 建模为 capability 让"文档变更"看起来比实际更重**
  → 缓解：本变更明确 `Impact` 章节只动 `README.md` 一个文件，且 spec 章节只描述 README 必须覆盖的话题，不强约束 wording；归档时不会污染其他模块的 spec。
- **[Trade-off] 不再保留 UGUI MVC 的"迁移指南"章节**
  → 决策依据：`migrate-ui-to-uitoolkit` 变更已经归档并描述了迁移过程，本 README 的读者目标是"今天就要开始用 UI 框架的人"，迁移历史保留在 git log 与归档 proposal 里更合适。
- **[Trade-off] README 不再描述 `UHub/` 设想**
  → 决策依据：`UHub/` 当前空目录，描述未实现内容会立刻让 README 重新过期；用一行"预留给后续 UI Hub 抽象"标注即可。

## Migration Plan

1. **作者侧（执行 `/opsx:apply` 时）**
   - 读取本 design + spec，逐条对照 `Assets/EF/EFRuntime/UI/*.cs` 的真实签名。
   - 整文件覆盖写入 `Assets/EF/EFRuntime/UI/README.md`。
   - 不需要 `unity-compile-check`：本变更不动 C# / asmdef。
   - 仅对仓库做一次 markdown 渲染人工 review（建议直接在 VSCode / GitHub UI 预览）。
2. **读者侧（合入后）**
   - 旧链接（如外部 wiki 引用 `README.md#uicontroller`）会失效，无回退；这些链接在 `migrate-ui-to-uitoolkit` 归档后已经实际指向不存在的代码，本变更只是把文档与现实对齐。
3. **回滚策略**
   - 本变更只动单个 markdown 文件，回滚等价于 `git revert`；不存在数据迁移、不存在运行时影响。

## Open Questions

- 是否需要在 README 中提到 `LocalEventBus` 的"目标使用场景"（窗口内系统间事件 vs 全局 EventHub）？倾向**是**，因为目录里同时存在 `LocalEventBus` 和全局 `EventHub`，读者必然会问。`tasks.md` 中默认勾选写入。
- 是否需要列出 EditMode 测试清单？倾向**只列文件名 + 一句话目的**，不复制断言；防止文件名重命名时 README 失效。
