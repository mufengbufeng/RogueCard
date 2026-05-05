## Context

项目启动链路当前为 `GameLogicEntry` 注册 `InitProcedure` 与 `MainMenuProcedure`，初始化后进入主菜单流程并打开 `MainView`。`MainView` 的开始按钮通过 `MainController` 发布 `StartLevelRequestedEvent`，但运行时没有流程订阅该事件，因此点击按钮后只更新主界面反馈，不会真正进入局内。

`GameView.prefab` 已位于 `Assets/AssetRaw/UI/Game/`，UI 资源组使用 `AddressByFileName`，因此 EF UI 打开窗口时可以使用 `"GameView"` 作为资源地址。EF UI 管理器在 Prefab 缺少对应 View 脚本时会动态添加 View 组件，但仍需要存在可编译的 `GameView : UIView` 和 `GameController : UIController` 类型。

## Goals / Non-Goals

**Goals:**

- 点击主界面开始按钮后，从主菜单流程切换到局内流程。
- 主菜单流程退出时关闭 `MainView`。
- 局内流程进入时打开 `GameView`，并满足 EF MVC UI 的最小 View/Controller 契约。
- 保留 `StartLevelRequestedEvent` 作为主界面输入到流程切换的解耦边界。
- 保持与 `add-game-scene-battle-presentation` 的职责边界：本变更只接入局内 UI 入口。

**Non-Goals:**

- 不实现完整战斗逻辑、波次推进、怪物生成、卡牌效果或奖励结算。
- 不把 `Game` 场景加载接入本次流程切换，除非后续局内场景变更明确要求。
- 不修改 YooAsset 资源收集规则。
- 不引入新的 UI 框架、流程框架或第三方依赖。

## Decisions

### 1. 由 `MainMenuProcedure` 订阅开始关卡事件并切流程

`MainController` 继续只负责把按钮输入转成 `StartLevelRequestedEvent`，`MainMenuProcedure` 在进入时订阅该事件，收到后调用 `ChangeState<GameProcedure>(procedureOwner)`。

- 选择原因：EF 的 `ProcedureBase.ChangeState` 是流程状态内部切换状态的既有模式，能自然触发 `MainMenuProcedure.OnLeave`。
- 替代方案：在 `MainController` 直接调用 `GameLogicEntry.Procedure.StartProcedure<GameProcedure>()`。该方案不可取，因为 `StartProcedure` 只能首次启动状态机，运行中再次调用会抛出状态机已经启动的异常。
- 替代方案：让 `GameLogicEntry` 或全局事件桥订阅事件。该方案会让启动入口承担运行时流程编排职责，不如流程自身清晰。

### 2. 新增 `GameProcedure` 管理局内 UI 生命周期

`GameProcedure.OnEnter` 使用 `OpenWindowAsync<GameView, GameController>(location: "GameView", layer: Normal, cacheOnClose: false, allowMultiple: false)` 打开局内窗口，`OnLeave` 关闭 `"GameView"`。

- 选择原因：与 `MainMenuProcedure` 管理 `MainView` 的模式一致，流程负责窗口生命周期，Controller 只处理窗口内部交互。
- 替代方案：在 `MainController` 收到按钮后直接打开 `GameView` 并关闭 `MainView`。该方案绕过流程层，后续接入局内状态、场景或返回主菜单时会缺少统一生命周期。

### 3. `GameView`/`GameController` 先保持最小骨架

本变更只要求类型存在、可初始化并能通过 EF UI 系统打开，不绑定战斗数据和复杂输入。

- 选择原因：`add-game-scene-battle-presentation` 已覆盖局内 UI 布局和战斗舞台骨架，本变更只补入口衔接，避免范围膨胀。
- 替代方案：同时实现所有局内 UI 引用字段和交互。该方案会和局内表现骨架变更重叠，增加 Prefab 冲突风险。

### 4. 保留默认关卡请求事件携带的关卡信息

流程切换可以先不消费完整关卡数据，但事件仍必须携带默认关卡标识和展示名，后续局内上下文可以复用该边界。

- 选择原因：保留已有测试和主界面能力语义，后续关卡运行时上下文接入时不需要改按钮输入链路。
- 替代方案：开始按钮直接切流程不发布事件。该方案简化首版代码，但会丢失“默认关卡进入请求”的现有规格契约。

## Risks / Trade-offs

- [风险] `StartLevelRequestedEvent` 订阅未在流程离开时解除，可能导致重复切换或访问已离开的流程状态 → [缓解] 在 `MainMenuProcedure.OnEnter` 订阅，在 `OnLeave`/`OnDestroy` 中解除订阅。
- [风险] `GameView.prefab` 当前未显式挂载 `GameView` 脚本 → [缓解] 依赖 EF UI 管理器动态添加 View 组件，同时新增可编译的 `GameView` 类型；后续 Prefab 整理可再显式挂载。
- [风险] 与局内场景/战斗舞台变更并行修改同一 Prefab → [缓解] 本变更尽量不改 `GameView.prefab` 结构，只补脚本和流程入口。
- [风险] 点击按钮多次可能重复发布请求 → [缓解] 开始处理时将主界面按钮设为不可交互或在流程切换后由 `MainView` 关闭消除重复输入。
