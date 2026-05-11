## Why

当前 EF UI 系统基于 UGUI（MonoBehaviour + Prefab + ReferenceCollector + UHub），与 GameObject 层级深度耦合。UI Toolkit 作为 Unity 6 的生产就绪 UI 方案，提供 UXML/USS 数据驱动布局、VisualElement 渲染树、原生 UQuery 查询等能力，可以消除 UHub 自动绑定、ReferenceCollector 手动引用、运行时回退创建组件等样板代码。趁项目早期 UI 数量少（仅 MainView 和 GameView），一次性迁移成本最低。

## What Changes

- **BREAKING** `UIView` 从 `MonoBehaviour` 改为纯 C# 类，持有 `VisualElement Root`，通过 `root.Q<T>("name")` 查找元素
- **BREAKING** `UIManager` 资源加载从 `GameObject` Prefab 改为 `VisualTreeAsset`（UXML），实例化从 `Instantiate` 改为 `CloneTree`
- **BREAKING** `IUIManager` 接口中 `Transform` 相关参数全部改为 `VisualElement`（`RegisterLayerRoot` → `RegisterLayerElement`、`SetFallbackRoot` → `SetFallbackElement`）
- **BREAKING** `UIRuntimeContext` 中 `Transform LayerRoot` 改为 `VisualElement LayerElement`
- **BREAKING** UILayer 分层从 Transform 父子关系改为多 UIDocument 架构（每层一个 UIDocument，通过 `PanelSettings.sortingOrder` 控制渲染顺序）
- 删除 `UHub/` 整个目录（ComponentBinder、UHubBindingConfig、事件绑定类等），用 UQuery + RegisterViewCallback 替代
- `UIView` 新增 `RegisterViewCallback` 自动追踪 UI Toolkit 回调，OnRelease 时批量清理
- `GameView` 和 `MainView` 从 UGUI 重写为 UI Toolkit（动态子项从 Instantiate Prefab 改为 CloneTree + UQuery）
- UI 资源文件从 Prefab（`.prefab`）迁移为 UXML（`.uxml`）+ USS（`.uss`）
- `UIWindowDescriptor` 的资源类型描述从 "Prefab 路径" 改为 "UXML 资源路径"
- `UIBindingCollection` / `BindProperty` 机制保持不变（与渲染层无关）
- `UISystem`、`UIController` 核心逻辑不变（仅 View 类型签名变化）
- `ReferenceCollector` 及其编辑器保留（非 UI 场景仍使用）

## Capabilities

### New Capabilities
- `uitoolkit-view-base`: UIView 纯 C# 基类重构，包括 VisualElement Root 持有、UQuery 元素查找、RegisterViewCallback 自动回调清理、DisplayStyle 显示/隐藏控制、CloneTree 资源实例化
- `uitoolkit-layer-system`: 多 UIDocument 层级架构，每层独立 UIDocument + PanelSettings.sortingOrder 渲染排序，层元素注册与窗口挂载

### Modified Capabilities
- `ui-system`: UIManager 资源加载和实例化从 GameObject/Transform 改为 VisualTreeAsset/VisualElement，窗口缓存从 SetActive/SetParent 改为 RemoveFromHierarchy/Add；UIRuntimeContext 从 Transform 切换到 VisualElement
- `game-ui-data-binding`: GameView 动态子项实例化从 Prefab + ReferenceCollector 改为 VisualTreeAsset.CloneTree + UQuery；信息显示从 TextMeshProUGUI 改为 Label；ScrollRect 改为 ScrollView
- `single-main-ui-entry`: 主界面资源从 Prefab 改为 UXML，UIManager 注册层根从 Transform 改为 VisualElement
- `main-to-game-view-flow`: 界面资源类型从 Prefab 改为 UXML，GameView 组件不再需要 MonoBehaviour 动态挂载
- `auto-bind-ui-script`: 此能力仅适用于 UGUI UIView，UIToolkit 迁移后不再需要自动绑定代码生成，标记为废弃

## Impact

- **框架层 (EFRuntime/UI)**：UIView（重写）、UIManager（重写）、IUIManager（接口变更）、UIRuntimeContext（变更）、UIWindowDescriptor（变更）。删除 UHub/ 整个目录。
- **游戏层 (HotFix/GameLogic/UI)**：GameView（重写）、MainView（重写）、GameController（小改）、MainController（小改）。
- **资源文件**：`Assets/AssetRaw/UI/` 下的 Prefab 需要全部替换为 UXML + USS 文件。子项 Prefab（GamePlay_MonsterItem 等）也需迁移。
- **场景设置**：需要创建多 UIDocument 的层级结构（UIRoot 下挂 4 个 UIDocument），替代现有的 Canvas + 层级节点方案。
- **测试**：现有 EditMode 测试需要适配新的 UIView（纯 C#）和 UIManager（VisualElement）接口。
- **兼容性**：`UIWindowHandle`、`UIWindowState`、`UILayer` 枚举基本不变。`UIBindingCollection` 不变。`UISystem` 不变。
