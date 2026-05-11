## ADDED Requirements

### Requirement: TargetSelector 必须支持 Enter / 怪物点击 / Cancel 三阶段流程

`TargetSelector` SHALL 提供以下公开 API：

- `IsActive` 只读属性
- `Enter(int handIdx)` —— 进入选目标态
- `Cancel()` —— 外部强制取消（如 Phase 变化）
- `Dispose()` —— 释放并清理

`Enter(handIdx)` SHALL：

1. 设置 `_state = Active` 与 `_selectedHandIdx = handIdx`
2. 调 `MonsterListView.EnterTargetMode(OnMonsterClicked)` —— `MonsterListView` 给所有存活怪物加 `target-selectable.active` 类与点击回调
3. 注册 ESC 监听到 `_rootElement`（`TrickleDown.TrickleDown`）
4. 注册空白点击监听到 `_rootElement`（仅响应非怪物 / 非 drop-zone 的点击为取消）

#### Scenario: Enter 后 IsActive 为真

- **WHEN** `_targetSelector.Enter(2)` 被调用
- **THEN** `_targetSelector.IsActive` SHALL 为 `true`
- **AND** `MonsterListView.EnterTargetMode(...)` SHALL 已被调用

#### Scenario: 怪物点击后调 UseCardOnMonster

- **WHEN** `Enter(handIdx=2)` 后 `MonsterListView` 内某怪物（索引 1）被点击
- **THEN** `ITargetContext.UseCardOnMonster(2, 1)` SHALL 被调用
- **AND** `_state` SHALL 转回 `Idle`
- **AND** `MonsterListView.ExitTargetMode()` SHALL 被调用

#### Scenario: ESC 取消

- **WHEN** `Enter(handIdx=2)` 后玩家按 ESC
- **THEN** `_targetSelector.IsActive` SHALL 变为 `false`
- **AND** `MonsterListView.ExitTargetMode()` SHALL 被调用
- **AND** `HandFanView.RequestGhostRebound(2)` SHALL 被调用
- **AND** `ITargetContext.UseCardOnMonster(...)` SHALL NOT 被调用

#### Scenario: 空白点击取消

- **WHEN** `Enter(handIdx=2)` 后玩家点击非怪物 / 非 drop-zone 区域
- **THEN** 与 ESC 取消行为一致

#### Scenario: Phase 变化外部取消

- **WHEN** `Enter(handIdx=2)` 后 `BattlePanelView` 调 `_targetSelector.Cancel()`
- **THEN** 与 ESC 取消行为一致

#### Scenario: 重复 Enter 报错或被忽略

- **WHEN** 已 `IsActive == true` 时再次调 `Enter(handIdx=3)`
- **THEN** SHALL 通过 `Log.Warning` 记录或直接忽略后续调用（实现可选）
- **AND** SHALL NOT 启动重复监听

### Requirement: TargetSelector 必须区分确认与取消两种退出路径

`TargetSelector` 退出 SHALL 区分两种路径：

- **Confirmed**（怪物点击）—— 调 `HandFanView.RequestGhostCleanup()`：立即销毁 ghost（卡随后被 `Hand.Changed` 自然移除，无需协同回弹动画）
- **Cancelled**（ESC / 空白 / Phase 变化）—— 调 `HandFanView.RequestGhostRebound(_selectedHandIdx)`：复用 change 2 的协同回弹动画（ghost 立即销毁、其他卡 transition 0.15s 回到 N 张布局、被拖卡 opacity 立即恢复）

#### Scenario: Confirmed 路径无回弹动画

- **WHEN** 玩家点击怪物确认
- **THEN** `_handFanView.RequestGhostCleanup()` SHALL 被调用
- **AND** `_handFanView.RequestGhostRebound(...)` SHALL NOT 被调用

#### Scenario: Cancelled 路径触发回弹

- **WHEN** ESC / 空白 / Phase 变化任意一种
- **THEN** `_handFanView.RequestGhostRebound(_selectedHandIdx)` SHALL 被调用
- **AND** `_handFanView.RequestGhostCleanup()` SHALL NOT 被调用

### Requirement: TargetSelector 必须解除全部监听并幂等 Dispose

`TargetSelector.Dispose()` SHALL：

- 调 `Cancel()`（若 IsActive）
- 解除 ESC / 空白点击监听（若仍注册）
- 清空所有字段
- 多次调用安全

#### Scenario: 处于 Active 状态时 Dispose

- **WHEN** `_targetSelector.IsActive == true` 时 `Dispose()` 被调用
- **THEN** `Cancel` 流程 SHALL 完整执行（含 `MonsterListView.ExitTargetMode` + ghost 回弹）
- **AND** 之后 ESC / 空白点击 SHALL NOT 触发任何监听

#### Scenario: 重复 Dispose 安全

- **WHEN** `Dispose()` 被调用两次
- **THEN** 第二次 SHALL NOT 抛出异常

## ADDED Requirements

### Requirement: MonsterListView 必须提供 EnterTargetMode / ExitTargetMode 公开 API

`MonsterListView` SHALL 暴露 `EnterTargetMode(Action<int> onMonsterClick)` 与 `ExitTargetMode()` 公开方法。

`EnterTargetMode` SHALL：

- 缓存 `_onTargetClick = onMonsterClick`
- 设置 `_targetModeActive = true`
- 对每只存活怪物的 `MonsterItemView.Root` 添加 `target-selectable` 与 `active` 两个 CSS 类
- 注册临时 `ClickEvent` 回调，回调内调 `_onTargetClick(monsterIdx)` 并 `StopPropagation`

`ExitTargetMode` SHALL：

- 移除所有 `target-selectable` 与 `active` 类
- 注销所有临时点击回调
- 清空 `_onTargetClick = null`、`_targetModeActive = false`

#### Scenario: EnterTargetMode 加高亮类

- **WHEN** `_monsterListView.EnterTargetMode(callback)`，当前有 3 只存活怪物
- **THEN** 3 个 `MonsterItemView.Root` SHALL 应用 `target-selectable.active`

#### Scenario: 怪物点击触发 callback

- **WHEN** `EnterTargetMode(callback)` 后玩家点击索引 1 的怪物
- **THEN** `callback(1)` SHALL 被调用

#### Scenario: ExitTargetMode 清类

- **WHEN** `_monsterListView.ExitTargetMode()`
- **THEN** 全部 `MonsterItemView.Root` SHALL NOT 包含 `target-selectable` 或 `active`

### Requirement: MonsterListView 在 target 模式下刷新必须保留高亮

`MonsterListView` SHALL 在 `_targetModeActive == true` 时，`Refresh()` 重建怪物项后重新应用 `target-selectable.active` 类与点击回调。

#### Scenario: target 模式下 Monsters 变化保留高亮

- **WHEN** `EnterTargetMode(callback)` 后 `Monsters.Value` 重新发布（如某只死亡）
- **THEN** 重建后所有存活怪物 SHALL 仍应用 `target-selectable.active`
- **AND** 点击新存活怪物 SHALL 仍触发 `callback(newIdx)`

## ADDED Requirements

### Requirement: HandFanView 必须提供 RequestGhostCleanup 与 RequestGhostRebound 公开 API

`HandFanView` SHALL 暴露：

- `RequestGhostCleanup()` —— 立即销毁 ghost（不触发回弹动画）；用于"目标选择确认"场景
- `RequestGhostRebound(int handIdx)` —— 启动协同回弹动画：ghost 立即销毁、其他卡 transition 0.15s 回到 N 张布局、被拖卡 opacity 立即恢复、`options.ReboundDurationMs` 后状态归 Idle

两个方法 SHALL 在非拖拽 / 非"由 BattlePanelView 持有 ghost" 状态下被调用时通过 `Log.Warning` 记录并幂等返回（不抛异常）。

#### Scenario: RequestGhostCleanup 立即销毁 ghost

- **WHEN** `_handFanView.RequestGhostCleanup()` 被调用
- **THEN** ghost VisualElement SHALL 被移除
- **AND** 不应启动 transition 动画

#### Scenario: RequestGhostRebound 启动协同回弹

- **WHEN** `_handFanView.RequestGhostRebound(2)` 被调用
- **THEN** ghost SHALL 立即销毁
- **AND** 其他卡 SHALL 应用 `transitionDuration = 0.15s`
- **AND** 被拖卡 SHALL 立即恢复 opacity
- **AND** `options.ReboundDurationMs` 后内部状态 SHALL 归 Idle

#### Scenario: 非预期状态调用时安全降级

- **WHEN** 当前 `Idle` 态调 `RequestGhostRebound(2)`
- **THEN** SHALL 通过 `Log.Warning` 记录
- **AND** SHALL NOT 抛异常
