## MODIFIED Requirements

### Requirement: Navigator.OpenAsync 必须替换 Screen 层内容
Navigator.OpenAsync(screenType, viewModel) SHALL 关闭当前 Screen（如有），按命名约定（或 Screen 子类 override 的 `UxmlLocation` / `UssLocation`）加载 UXML / USS 资源，创建 Screen 实例，挂载到 ScreenLayer，注入 ViewModel 并调用 Setup 和 OnShow。Navigator SHALL 同时提供按类型重载 `OpenAsync<TScreen>(ViewModelBase vm = null)` 和按字符串重载 `OpenAsync(string viewName, ViewModelBase vm = null)`，两者语义等价。当目标类型派生自 `Popup<>` 时，SHALL 走 PopupLayer 入栈而非替换 ScreenLayer（见 PushPopup 相关 Requirement）。当未传入 ViewModel 时，SHALL 通过 `Activator.CreateInstance` 按 ViewModel 类型自动创建。

#### Scenario: 首次按类型导航到主界面
- **WHEN** 调用 `OpenAsync<MainView>(mainViewModel)` 且 ScreenLayer 为空
- **THEN** Navigator SHALL 按 `MainView.UxmlLocation`（默认 `"MainUxml"`）加载 UXML
- **AND** 创建 MainView 实例并添加到 ScreenLayer
- **AND** 调用 Screen.Setup(mainViewModel) 和 Screen.OnShow()

#### Scenario: 按字符串导航到主界面
- **WHEN** 调用 `OpenAsync("MainView", mainViewModel)` 且 ScreenLayer 为空
- **THEN** Navigator SHALL 通过反射查找名为 `MainView` 的 Screen 派生类型
- **AND** 后续行为 SHALL 与按类型重载一致

#### Scenario: 从主界面切换到局内界面
- **WHEN** 调用 `OpenAsync<GameView>(gameViewModel)` 且 ScreenLayer 上已有 MainView
- **THEN** Navigator SHALL 先调用当前 Screen 的 OnHide 和 OnDispose
- **AND** SHALL 清空 ScreenLayer
- **AND** SHALL 创建 GameView 并添加到 ScreenLayer

#### Scenario: 未传入 ViewModel 时自动创建
- **WHEN** 调用 `OpenAsync<MainView>()` 不传 ViewModel
- **THEN** Navigator SHALL 通过 `Activator.CreateInstance(typeof(MainViewModel))` 自动创建实例
- **AND** SHALL 注入该实例并调用 Screen.Setup

#### Scenario: 按字符串导航到不存在的 Screen 类型
- **WHEN** 调用 `OpenAsync("Unknown", vm)` 且 AppDomain 内无名为 `Unknown` 的 Screen 派生类型
- **THEN** SHALL 抛出 KeyNotFoundException
- **AND** 错误信息 SHALL 包含期望的类型名

### Requirement: Navigator 必须将 Popup 类型的目标入栈到 PopupLayer
Navigator.OpenAsync 当目标类型派生自 `Popup<>` 时 SHALL 加载弹窗 UXML / USS，创建半透明遮罩层，将弹窗添加到 PopupLayer，弹窗在遮罩之上。多次 OpenAsync 派生自 Popup<> 的类型 SHALL 按后进先出顺序叠加。`OpenAsync` 的按类型/按字符串两种重载 SHALL 均支持 Popup 分流。

#### Scenario: 推入第一个弹窗
- **WHEN** 调用 `OpenAsync<SettingsView>(vm)` 且 `SettingsView : Popup<SettingsViewModel>`
- **THEN** PopupLayer SHALL 包含一个遮罩元素和一个弹窗 Screen
- **AND** 弹窗 SHALL 调用 OnShow()
- **AND** ScreenLayer 内容 SHALL NOT 被改动

#### Scenario: 推入第二个弹窗叠加在第一个之上
- **WHEN** 已有一个弹窗在 PopupLayer，再调用 OpenAsync 派生自 Popup<> 的类型
- **THEN** 新弹窗 SHALL 添加到 PopupLayer 的末尾（渲染在最上层）

### Requirement: Navigator.Close 必须按栈顺序关闭顶层弹窗
Navigator.Close() SHALL 移除 PopupLayer 中最后添加的弹窗和对应的遮罩层，调用弹窗的 OnHide 和 OnDispose。Screen.OnDispose 内部 SHALL 调用 ViewModel.Dispose()。Navigator 同时 SHALL 提供 CloseAll() 关闭所有 Popup（保留当前 ScreenLayer 内容）。

#### Scenario: 关闭最顶层弹窗
- **WHEN** PopupLayer 中有 2 个弹窗，调用 Close()
- **THEN** 最后添加的弹窗 SHALL 被移除
- **AND** 第一个弹窗 SHALL 保持显示

#### Scenario: 弹窗栈为空时 Close
- **WHEN** PopupLayer 中无弹窗，调用 Close()
- **THEN** SHALL 不抛异常，直接返回

#### Scenario: CloseAll 关闭所有 Popup
- **WHEN** PopupLayer 中有 2 个弹窗，调用 CloseAll()
- **THEN** 所有弹窗 SHALL 被移除并依次调用 OnDispose
- **AND** ScreenLayer 内容 SHALL 保持不变

## REMOVED Requirements

### Requirement: ScreenRegistry 必须支持注册和查询
**Reason**：消除中心化注册表，改由命名约定 + 反射在 `Navigator.OpenAsync` 内部按需解析。新增 Screen 不再需要回到 `GameLogicEntry` 或任何中心列表登记。

**Migration**：
- 调用方 `Register<TScreen, TViewModel>("Name", "UxmlLocation")` 全部删除
- Screen 类按 `{Stem}View` 命名约定（见 `ui-screen-conventions`），UXML 资源按 `{Stem}Uxml` 命名
- 需要打开时直接调用 `Navigator.OpenAsync<TScreen>()` 或 `Navigator.OpenAsync("StemView")`，无需预先注册
- 自定义 UXML 资源名通过 `Screen<T>.UxmlLocation` 虚属性 override

## ADDED Requirements

### Requirement: Navigator 必须支持按字符串名查找 Screen 类型并缓存

Navigator.OpenAsync(string viewName, ...) SHALL 通过反射在当前 AppDomain 的所有已加载程序集中查找名为 `viewName` 的非抽象 `Screen<>` 派生类型。Navigator SHALL 在内部维护 `Dictionary<string, Type>` 缓存，命中即直接返回；未命中时 SHALL 完整扫描程序集并缓存结果。命中多个同名类型（不同命名空间）时 SHALL 抛出 InvalidOperationException 提示使用类型重载 `OpenAsync<TScreen>()`。

#### Scenario: 首次按字符串查找类型
- **WHEN** 第一次调用 `OpenAsync("MainView", vm)` 且缓存为空
- **THEN** Navigator SHALL 遍历 AppDomain.CurrentDomain.GetAssemblies() 查找名为 `MainView` 且派生自 `Screen<>` 的非抽象类型
- **AND** SHALL 将查找结果存入字典缓存

#### Scenario: 后续按字符串查找命中缓存
- **WHEN** 已经有过一次 `OpenAsync("MainView", vm)` 调用，再次调用同名打开
- **THEN** Navigator SHALL 直接从缓存返回类型
- **AND** SHALL NOT 重新扫描程序集

#### Scenario: 同名类型冲突
- **WHEN** 两个不同命名空间的非抽象类型都名为 `MainView` 且都派生自 `Screen<>`，调用 `OpenAsync("MainView", vm)`
- **THEN** Navigator SHALL 抛出 InvalidOperationException
- **AND** 错误信息 SHALL 列出冲突的两个类型全名
- **AND** SHALL 提示使用 `OpenAsync<TScreen>()` 类型重载消除歧义
