## Context

EF.UI 当前的 Navigator 体系把"加 Screen"和"改启动入口"耦合在一起：每个新 Screen 必须在 `GameLogicEntry.InitializeNavigator()` 中调用 `registry.Register<TScreen, TViewModel>("MainMenu", "MainView")` 显式登记，否则 `NavigateToAsync("MainMenu", vm)` 会抛 `KeyNotFoundException`。同时项目里同一功能存在三套名字（类名 `MainMenuScreen` / 字符串 ID `"MainMenu"` / UXML 资源 `"MainView"`），靠注册表把它们粘起来。

类比 UGUI 的体验是 `OpenPanel<MainView>()` → 加载 `MainView.prefab` → 自动绑 `MainView` 脚本。Prefab 上序列化了组件类型，所以"资源"和"脚本"天然焊死。UI Toolkit 的 UXML 是纯标记，没有这个焊点——但只要约定"类名 ↔ UXML 资源名"，就能用反射 + 命名约定补齐这条线。

底层框架（HybridCLR + YooAsset）对该方案均无阻：

- **HybridCLR**：热更新程序集是运行时加载的真实 .NET 程序集，反射可枚举其中的 `Screen<>` 派生类型，不受 AOT 裁剪影响
- **YooAsset 2.3.x**：通过 `IResourceManager.LoadAssetAsync<T>(addressable)` 加载 UXML / USS，资源名即 addressable，与命名约定天然匹配

但 UXML 内嵌的 `<Style src="...uss"/>` 引用走的是 Unity 自己的资源解析（不经过 YooAsset），在 Build 阶段是否会因 bundle 拓扑导致 USS 解析失败，是一个必须验证的灰色地带——这是本设计中唯一的 spike。

## Goals / Non-Goals

**Goals:**

- 新增 Screen = 新建一个 `{Stem}View` 类 + 配同名 UXML，**完全不触碰 `GameLogicEntry` 或任何中心列表**
- 类、ViewModel、UXML、USS 四者命名围绕同一个 `{Stem}` 组织，搜索一个名字能定位整个功能的所有片段
- 提供按类型打开（`OpenAsync<TScreen>()`）和按字符串打开（`OpenAsync(string)`）两种入口，分别服务于代码内调用和配置表数据驱动
- `Popup<TViewModel>` 标记基类替代 `PushPopupAsync` 这一额外 API；Navigator 通过类型分流到 PopupLayer 走栈式管理
- Model 层同步用 `TryGetModel<T>()` 懒加载替代显式注册

**Non-Goals:**

- **不**移除 Procedure 中心化注册（FSM 构造期需全部状态实例，且数量稳定，自动发现并不带来收益）
- **不**重构 ViewModel / ReactiveProperty / Region / Shell 任何 API
- **不**引入 ScriptableObject 清单 / 源码生成器 / DI 容器扫描这些更重的方案
- **不**在 EF 框架引入对 `Screen` 之外类型的反射自动发现（避免变成"什么都自动注册"的不可控状态）
- **不**在本变更内做 UXML 内嵌 `<Style>` 是否保留的最终决策——由 spike 任务在 build 验证后通过后续小改动决定

## Decisions

### 决策 1：约定优先，用反射推导默认值，用虚属性兜底特例

`Screen<TViewModel>` 基类增加：

```csharp
protected virtual string UxmlLocation => DeriveAssetName("Uxml");
protected virtual string UssLocation  => DeriveAssetName("Uss");

string DeriveAssetName(string suffix) {
    var name = GetType().Name;
    if (name.EndsWith("View", StringComparison.Ordinal))
        name = name[..^4];        // "MainView" → "Main"
    return name + suffix;          // "Main" + "Uxml" → "MainUxml"
}
```

99% 的 Screen 走默认；极少数特殊场景可 `override` 单个属性指向自定义资源名。

**为什么不用 Attribute（`[Screen("MainUxml")]`）？**

讨论早期考虑过用 Attribute 标注元数据，但仔细衡量后否决：

- 类名已经强约束为 `{Stem}View`，再加 Attribute 等于把同一个 `{Stem}` 写两遍
- 虚属性 + 反射推导能在零样板的同时保留 override 出口，比 Attribute 更轻
- EF 现存的 Attribute 用法（`EventArgsAttribute`）是给 codegen 工具扫描用的，runtime 路径上没有可参考的先例

**为什么不用纯命名约定（"类名 = UXML 名"）？**

考虑过让类直接叫 `MainUxml` 或让 UXML 直接叫 `MainView.uxml`，但：

- "脚本叫 Uxml" 反直觉
- 用户明确选择了 `{Stem}View / Uxml / Uss` 的角色后缀方案，三个文件在 Project 视图里有清晰的视觉区分（"哪个是模板、哪个是样式"一眼可辨）
- 角色后缀对搜索友好（搜 `Uxml` 列出所有模板）

### 决策 2：`Popup<TViewModel>` 用基类继承而非 Attribute / bool 标志

```csharp
public abstract class Popup<TViewModel> : Screen<TViewModel>
    where TViewModel : ViewModelBase { }
```

`Popup<T>` 仅作为类型 marker，本身**不**包含栈管理逻辑——栈管理仍然在 Navigator 中实现。Navigator 通过 `IsSubclassOfRawGeneric(typeof(TScreen), typeof(Popup<>))` 判断走 ScreenLayer 还是 PopupLayer。

**为什么不放在 Screen 上加 `IsPopup` 虚属性？**

- 基类继承在编译期就把"全屏 vs 弹窗"区分清楚，IDE 跳转、文档生成、代码审查都能看出意图
- 未来 Popup 可能需要不同的生命周期钩子（如 `OnCancel`、`OnBackdropClicked`），有独立基类时扩展自然；放虚属性会逼着把这些钩子塞到 Screen 基类里污染所有非弹窗 Screen
- 与 UGUI 项目中 `Panel` / `Popup` 分基类的工业惯例一致

### 决策 3：`OpenAsync<TScreen>()` 主推，`OpenAsync(string)` 兜底

```csharp
// 主推：类型驱动，编译期检查，IDE 重命名安全
await Navigator.OpenAsync<MainView>(viewModel);
await Navigator.OpenAsync<SettingsView>(viewModel);   // 自动走 Popup 栈

// 兜底：字符串驱动，供 Luban / 配置表 / 数据驱动场景
await Navigator.OpenAsync("MainView", viewModel);
```

字符串重载内部维护 `Dictionary<string, Type>` 缓存：

- 第一次按 `name` 调用时，反射当前 AppDomain 下所有非抽象 `Screen<>` 派生类型，按 `Type.Name` 建索引
- 命中即缓存；找不到 SHALL 抛 `KeyNotFoundException`，错误信息要包含"在以下程序集 [...] 中未找到名为 'XXX' 的 Screen 类型"
- 重名（同名类在不同命名空间）SHALL 抛 `InvalidOperationException` 提示开发者用全名或类型重载

**ViewModel 解析**：从 `typeof(TScreen)` 沿继承链找到 `Screen<>` 闭合泛型，取其泛型参数即 ViewModel 类型。Navigator 不强制调用方传入 ViewModel——若调用方传 null，则用 `Activator.CreateInstance(viewModelType)` 自动创建（要求 ViewModel 有无参构造）。

### 决策 4：API 合并 `NavigateToAsync` + `PushPopupAsync` → `OpenAsync`

旧接口 `INavigator`：

```csharp
UniTask NavigateToAsync(string screenName, ViewModelBase vm, ...);
UniTask PushPopupAsync(string popupName, ViewModelBase vm, ...);
void PopPopup();
```

新接口：

```csharp
UniTask OpenAsync<TScreen>(ViewModelBase vm = null, ...) where TScreen : Screen, new();
UniTask OpenAsync(string viewName, ViewModelBase vm = null, ...);
void Close();           // 关闭顶层（Popup 出栈，或当前 Screen——后者通常无意义）
void CloseAll();        // 关闭所有 Popup，回到当前 Screen
```

**为什么合并？**调用方往往不关心目标是 Screen 还是 Popup（这是被打开类型自身的属性），强制分两套 API 是反直觉的样板。Navigator 内部按基类分流后，外部只看到统一入口。

### 决策 5：Model 简化——删除显式注册，全量切到 `TryGetModel<T>()`

`ModelManager.TryGetModel<TModel>()` 已存在并支持懒注册：

```csharp
public TModel TryGetModel<TModel>() where TModel : ModelBase, new() {
    if (_models.TryGetValue(typeof(TModel), out var existing)) return existing;
    return RegisterInternal(new TModel());
}
```

`InitializeModels()` 删除后，业务侧首次访问 Model 时自动注册。**不**改 `ModelManager` 自身 API。

**风险**：原显式注册有"启动期暴露 Model 构造异常"的隐含好处，懒加载会把这种异常推迟到首次 `TryGetModel` 调用点。本项目 `MainModel` / `GameModel` 的构造极简（无外部依赖、无 IO），实际风险可忽略。如未来出现复杂 Model，再考虑加一个可选的 `ModelManager.WarmupAll()` 给 Loading 流程预热。

### 决策 6：USS 加载策略——约定加载 + UXML 内嵌共存，spike 验证后再调

**当前阶段**（spike 完成前）：

- C# 通过 `Screen<T>.UssLocation` 加载 USS，**主动 attach** 到 Screen 根
- UXML 内 UI Builder 写入的 `<Style src=...>` **保留**（仅服务编辑器预览）
- StyleSheet 在引擎层按引用 dedupe，重复 attach 是幂等操作，不影响渲染

**约定加载找不到 USS 的处理**：

- DEBUG 构建：`Log.Warning("[Screen] 未找到 {Stem}Uss.uss，仅依赖 UXML 内嵌样式或全局样式")`，每个 `{Stem}` 仅警告一次
- Release 构建：静默继续

**Spike**（独立任务）：构建一次 Standalone Player，验证 UXML 内嵌 `<Style src=...>` 在 YooAsset bundle 拓扑下能否正确解析。

- 能解析 → 可以选择从 UXML 删除 `<Style>` 块统一只走 C# 约定加载，也可以保留共存
- 不能解析 → 必须从 UXML 删除 `<Style>`，**只**走 C# 约定加载，否则编辑器和 build 行为会不一致

Spike 结论以一个独立小变更落地，不卡本变更主线。

### 决策 7：命名重构一次性完成

不分两步走（先加新 API 保留旧 API → 后续清理）。两步走在两个 Screen 规模下增加同步成本（要保证旧 API 仍可工作 + 测试两套），收益微乎其微。

```
阶段 1 = 阶段 2 = 在同一变更内：
    ├─ 新基类 + 新 Navigator API 实现
    ├─ 删除 ScreenRegistry / NavigateToAsync / PushPopupAsync
    ├─ 重命名两个 Screen + 对应 UXML/USS 资源
    ├─ 更新所有调用点（Procedure / Tests）
    └─ 更新两个相关 spec
```

## Risks / Trade-offs

- **YooAsset 资源 ID 重映射出错** → 资源管理工具/打包配置中确认重命名后 addressable 名称同步更新；变更内提供一个临时 EditMode 工具或脚本验证所有 `{Stem}Uxml` / `{Stem}Uss` addressable 可加载
- **HybridCLR 程序集扫描漏掉新 Screen** → 字符串重载首次调用时遍历 `AppDomain.CurrentDomain.GetAssemblies()`，但 HybridCLR 加载的热更程序集只有在 `LoadMetadataForAOTAssembly` 完成后才出现在 AppDomain；Navigator 的反射扫描必须在 `GameLogicEntry.Init()` 之后执行（即 HybridCLR 已就绪），实际由"按需扫描"自然保证
- **类名重命名搜索不全** → 重构清单里精确列出每个旧名字（`MainMenuScreen` / `GameScreen` / `"MainMenu"` / `"Game"` / `NavigateToAsync` / `PushPopupAsync` / `ScreenRegistry`）；用 Serena `find_referencing_symbols` 确认无遗漏
- **Tests 引用旧 API** → EditMode 测试在 `Tests/EditMode/` 下，必须同步更新；PlayMode 测试不在 CI 范围但本地需手动跑一遍验证
- **其他子模板 UXML（BattlePanel / RewardPanel / CardItem 等）误改名** → 这些是 Region 内局部加载的子模板，**不**适用 `{Stem}Uxml` 约定，只 Screen 顶层资源改名；任务清单里明确"白名单"
- **USS 加载在 Build 阶段失效** → 通过 spike 验证后再决定 UXML 内 `<Style>` 是否删除；当前阶段保留 = 编辑器和 build 都至少有一条路能加载到样式
- **Procedure 中调用 `NavigateToAsync` 的代码点遗漏** → 用 Grep `NavigateToAsync\|PushPopupAsync` 全量列出后逐个迁移
- **新增 Screen 类放错命名空间导致字符串查找失败** → 反射扫描全程序集而非固定命名空间；同名冲突时抛错并指引使用类型重载

## Migration Plan

由于变更同时涉及 EF 框架（Navigator/Screen/Popup）、业务侧 Screen 重命名、资源重命名和调用点更新，迁移**必须**作为单一原子变更完成（一次 commit 跨多个文件，不能拆成"先发布框架再迁移调用方"）。

迁移顺序（实施时按此序，但同一变更内）：

1. EF 框架：新增 `Popup<T>` 基类、`Screen<T>` 增加虚属性、Navigator 重写、删除 ScreenRegistry
2. 业务 Screen 重命名（类、文件、命名空间不变）
3. 资源重命名（UXML/USS）+ 验证 addressable 可加载
4. 调用点全量更新（GameLogicEntry / Procedure / Tests）
5. 删除 `InitializeModels()`，业务侧改用 `TryGetModel<T>()`
6. 编译检查 + EditMode 全部测试 + 本地 PlayMode 关键场景手测
7. 文档更新（CLAUDE.md 中"UI 系统"段落、`ui-framework-docs` 等相关说明）

**回滚策略**：变更未合并主线前，回滚 = 整个分支丢弃。合并后回滚需 `git revert` 整个 merge commit；由于 ScreenRegistry 删除是破坏性变更，部分回滚不可行。

## Open Questions

- **Q1**：UXML 内嵌 `<Style src=...>` 在 YooAsset Build 下是否能解析？→ 由 spike 任务回答；不阻塞本变更主线
- **Q2**：未来是否需要 `Navigator.WarmupAll()` 在 Loading 阶段预扫所有 Screen 类型 + 预加载 UXML？→ 暂不实现，等到首个对加载耗时敏感的 Screen 出现时再加
- **Q3**：`Popup<T>` 的栈管理在第一个真实 Popup 需求出现前要不要实现完整？→ 本变更**只**加基类和 Navigator 的"按基类分流到 PopupLayer"逻辑；遮罩 / 动画 / 多层栈细节留给"第一个 Popup"那次变更完善
