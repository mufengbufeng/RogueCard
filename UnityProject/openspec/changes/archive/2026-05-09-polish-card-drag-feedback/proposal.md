## Why

Change 1~3 把战斗后端模型升级到位（卡牌效果模型 + 怪物对称 + 玩家等级）。但 UI 层 `GameScreen` 还停留在旧模型假设：

- 拖拽出牌失败（能量不足 / 阶段不对）时**完全无视觉反馈**——卡片直接回弹，玩家不知道为什么没出牌
- 法术卡的 `TargetMode = SingleManual` 需要玩家手动选目标，**当前没有目标选择 UI**
- 怪物意图当前用 `IntentType` 渲染（"攻击 3 / 防御 5"）；Change 2 后意图变成 `PendingCards`（一组 Card），需要按 `TbCardEffect` 的 Kind 渲染对应图标
- DoT / Buff 状态没有任何 UI 表示

本变更专注于 UI 层把这些反馈补齐，让玩家能"看懂"新战斗系统。

## What Changes

- 拖拽出牌失败提示：当 `ViewModel.UseCard(idx)` 返回 false（隐式 — 通过订阅 `CardSystem` 的"出牌失败"事件）时，在 drop-zone 上方短暂显示原因文本（"能量不足" / "不在你的回合"）
- 多目标选择 UI：`TargetMode = SingleManual` 的卡拖入 drop-zone 后，进入"选目标"模式 — 显示怪物高亮，玩家点击某个怪物完成出牌；按 ESC 或点击空白取消
- 怪物意图渲染基于 PendingCards：意图区为每只怪物渲染其 `PendingCards`，每张卡按其 Effects 列表渲染图标和数字（Damage 红色剑 + 数值 / Shield 蓝色盾 + 数值 / DamageDot 紫色火 + 数值 / EnergyGain 黄色闪电 + 数值）
- Buff / DoT 状态条：玩家与每只怪物头顶显示其 `Buffs` 列表的图标 + 剩余回合数
- 新增 `CardPlayFailedEvent` 用于失败提示

## Capabilities

### Modified Capabilities

- `game-ui-data-binding`：拖拽出牌失败反馈、多目标选择 UI、PendingCards 意图渲染、Buff 状态条

## Impact

- 受影响代码:
  - `Assets/GameScripts/HotFix/GameLogic/UI/Game/GameScreen.cs`（出牌失败提示、目标选择模式、意图与 buff 渲染）
  - `Assets/GameScripts/HotFix/GameLogic/UI/Game/GameViewModel.cs`（暴露 PendingCards / Buffs 镜像、`UseCard(handIndex, targetIndex)` 重载）
  - `Assets/GameScripts/HotFix/GameLogic/UI/Game/GameModel.cs`（玩家 Buffs 属性 + PropertyChanged）
  - `Assets/GameScripts/HotFix/GameLogic/UI/Game/CardSystem.cs`（出牌失败发布事件）
  - `Assets/GameScripts/HotFix/GameLogic/Event/BattleEvents.cs`（新增 `CardPlayFailedEvent`）
  - `GameProcedure.cs`（订阅失败事件转发到 ViewModel）
- 受影响资源:
  - `Assets/AssetRaw/UI/Game/BattlePanel.uxml`（增加目标高亮模板、buff bar 模板、fail toast 占位）
  - `Assets/AssetRaw/UI/Game/GameViewStyles.uss`（USS 类：`.target-selectable.active` / `.buff-icon` / `.fail-toast`）
- 受影响测试:
  - 手测覆盖：5 类卡 + 史莱姆战 + 法术 DoT 持续 3 回合 + 能量不足拖拽
- 不受影响:
  - `BattleSystem` / `MonsterSystem` / `CardEffectExecutor` / `IBattleActor`
  - 配置表
- 依赖关系: 必须在 Change 1（提供 PendingCards / Buffs / Effects 数据结构）与 Change 2（怪物 PendingCards 实际填充）完成之后；与 Change 3 互独立
