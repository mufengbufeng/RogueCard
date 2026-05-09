## Context

新战斗系统在 UI 层暴露了 4 个新概念：(1) 多效果组合卡 (2) 多目标选择 (3) DoT / Buff (4) 怪物 PendingCards。这些概念的 UI 表达全部缺失。本变更只做 UI 反馈与交互，不动后端逻辑。

## Goals / Non-Goals

**Goals:**

- 玩家能感知"为什么这张卡没打出去"
- 法术卡（SingleManual）的目标选择流程顺畅（拖入 drop-zone → 高亮怪物 → 点击 → 完成）
- 怪物意图能反映卡牌的多效果组合（攻 6 + DoT 2 持续 3 回合的法术卡可视化为"红剑 6 + 紫火 2x3"）
- 玩家与怪物的 Buff 列表（含 DoT）有可见图标 + 剩余回合数

**Non-Goals:**

- 不做"卡牌升级 / 圣物 / 烟花特效"等表现层包装
- 不做拖拽过程中的"投射卡分散预览"动画
- 不做法术卡的"区域 AoE 范围预览"
- 不引入新的资源加载机制（buff 图标用 USS 颜色 + 文字标识，不依赖图集）
- 不修改 Region 切换 / 扇形布局核心算法

## Decisions

### 1. 失败反馈走事件总线，不走返回值

`CardSystem.Play` 维持现有 bool 返回值，但**额外**在失败时通过 `IEventPublisher` 发布 `CardPlayFailedEvent { Reason : string }`：

```
Reason ∈ {
  "NotPlayerTurn",
  "InvalidHandIndex",
  "InsufficientEnergy",
  "InvalidTarget"
}
```

`GameProcedure` 订阅此事件 → 调用 `ViewModel.NotifyCardPlayFailed(reason)` → `GameScreen` 显示 toast。

**选择原因:**
- 同一套发布机制和 `CardPlayedEvent` 对称
- ViewModel 只暴露事件意图，不持有 UI 状态

**Alternatives considered:**
- `Play` 返回 `(bool, FailReason)`：调用方都要处理，但失败信息扩散到 GameProcedure 里更难一处控制

### 2. 目标选择走 GameScreen 的状态机扩展

GameScreen 现有 `CardInteractionState`（Idle / Hovering / Previewing / Dragging）在拖拽释放命中 drop-zone 时直接 `ViewModel.UseCard(idx)`。本变更加一个 `SelectingTarget` 状态：

```
Dragging
  ├─ release in drop-zone, card.TargetMode == SingleManual  → SelectingTarget
  │                                                              ↓
  │                                                       玩家点怪物 / 取消
  │                                                              ↓
  │                                                       UseCard(idx, targetIdx)
  └─ release in drop-zone, 其他 TargetMode                → UseCard(idx)
```

`SelectingTarget` 期间：怪物显示 `.target-selectable.active` 高亮、其他交互禁用、ESC / 点空白返回 Idle 并把卡回弹。

**选择原因:** 复用现有状态机，避免引入第二套 PointerCapture 逻辑。

### 3. ViewModel.UseCard 签名扩展为 (handIndex, targetIndex = -1)

`-1` 表示"由后端按 TargetMode 自动决策"（SingleAuto / All / SplitAcrossAll / Self 都走 -1）；`>= 0` 表示玩家手选的怪物索引（仅 SingleManual 用）。

**选择原因:** 单一入口，向后兼容现有调用点。

### 4. PendingCards 通过 ViewModel 镜像

`GameModel.Monsters[i].PendingCards` 已经在 Change 2 中填入。`GameViewModel.Monsters` 的 `ReactiveProperty<IReadOnlyList<MonsterRuntime>>` 已经能在 `MonsterSystem.BeginMonsterPrepare` 完成后触发刷新，UI 层只需在 `RefreshMonsters` 中遍历 `monster.PendingCards` 渲染。

**选择原因:** 不引入额外的 ReactiveProperty，最小改动。

### 5. 意图渲染按 EffectKind 映射成图标 + 数字

```
EffectKind.Damage      → 类 .intent-icon-damage     文本: "{value}"
EffectKind.Shield      → 类 .intent-icon-shield     文本: "{value}"
EffectKind.DamageDot   → 类 .intent-icon-dot        文本: "{value}×{duration}"
EffectKind.EnergyGain  → 类 .intent-icon-energy     文本: "+{value}"
```

每张 Pending Card 渲染为一个 `intent-card` 容器，里面挂多个 `intent-icon`（按卡的 Effects 列表）。`SplitAcrossAll` 类型的卡显示其分散后单目标值（如 "投射 6 → 单体 3"）。

**选择原因:** 复用 USS class，不依赖图集；数字驱动布局简单。

### 6. Buff 状态条渲染

每个 `IBattleActor` 视觉上方挂一个 `.buff-bar` 容器，遍历 `actor.Buffs`，每条 Buff 渲染一个图标 + `{Value}×{RemainingTurns}` 文本。

`GameModel` 需新增 `PlayerBuffs : IReadOnlyList<BuffRuntime>` 属性 + PropertyChanged，让玩家方的 buff 也能镜像到 ViewModel。怪物方走 `Monsters` 的现有刷新链路。

**选择原因:** 玩家 buff 与怪物 buff 渲染逻辑同形，差异只是数据来源不同。

### 7. fail toast 用 USS transition 自动隐藏

`.fail-toast` 类显示 1.2 秒后自动 fade out（USS 关键帧或 transition + 协程清理）。同一时间最多显示 1 条；新失败覆盖旧失败。

**选择原因:** 简单可靠；不需要队列管理。

## Risks / Trade-offs

- [风险] `SelectingTarget` 状态期间收到 `Phase` 变化（怪物回合切换、回合超时）导致状态机错乱 → [缓解] `OnPhaseChanged` 时若处于 `SelectingTarget` 立即取消并回弹卡片
- [风险] `PendingCards` 列表在 ViewModel 与 Model 之间不是深拷贝，UI 渲染期间 Model 修改会导致渲染数据不一致 → [缓解] `RefreshMonsters` 在数据变更时触发，按当前快照渲染；MonsterTurn 执行后 PendingCards 会被清空，但此时 UI 已更新到 Check / Prepare 阶段
- [风险] Buff 列表更新没有细粒度 PropertyChanged（Add/Remove/Tick 都触发 "Monsters" 整列表变更） → [缓解] MVP 接受这个开销；未来切到 ObservableCollection 再优化
- [风险] 法术卡多目标选择 UI 与现有 Region 切换冲突 → [缓解] `SelectingTarget` 状态期间 disable Region 切换，等回到 Idle 再放行

## Open Questions

- 失败 toast 的文本是中文硬编码还是从配置读？建议**MVP 中文硬编码**，i18n 留作后续
- 法术卡的"All"模式是否也需要选目标 UI（玩家选中"对全部敌方施法的范围"）？建议**否**：All 直接打全部，不需要确认；只有 SingleManual 需要选
- 玩家方的 Buff 渲染位置是放在血条旁还是单独一行？建议**血条旁**，与现有 `player-status` 区域一致
- 拖拽过程中是否要预览伤害（如"对怪物 A 造成 6 伤害"）？建议**MVP 不做**，太多边缘情况；只在松手成功打出后看结果数字
