## 1. 事件与 ViewModel 接口

- [x] 1.1 在 `Event/BattleEvents.cs` 新增 `readonly struct CardPlayFailedEvent { string Reason; }`
- [x] 1.2 `CardSystem.Play` 在每条失败 return false 之前发布 `CardPlayFailedEvent`，Reason 取自 4 种枚举字符串：`NotPlayerTurn` / `InvalidHandIndex` / `InsufficientEnergy` / `InvalidTarget`
- [x] 1.3 `GameProcedure` 订阅 `CardPlayFailedEvent`，转发到 `_viewModel.NotifyCardPlayFailed(reason)`
- [x] 1.4 `GameViewModel` 新增 `event Action<string> CardPlayFailed`，`NotifyCardPlayFailed(string)` 调用它
- [x] 1.5 `GameViewModel` 修改 `UseCard(int handIndex)` 为 `UseCard(int handIndex, int targetIndex = -1)`，事件签名同步扩展
- [x] 1.6 `GameProcedure.OnCardUsed` 接收 `(handIndex, targetIndex)` 后转发到 `CardSystem.Play(handIndex, targetIndex)`（CardSystem.Play 同步加 `targetIndex` 参数，本变更范围内的最小后端改动）

## 2. 目标选择状态机

- [x] 2.1 `GameScreen` 在 `CardInteractionState` 枚举新增 `SelectingTarget`
- [x] 2.2 `OnCardPointerUp` 中：拖拽释放命中 drop-zone 且 `card.Config.TargetMode == TargetMode.SingleManual` → 进入 `SelectingTarget` 状态（不立即 UseCard）
- [x] 2.3 `SelectingTarget` 进入时：保留 ghost 卡片浮在 drop-zone 上方、给 `_monsterContainer` 内每个 monster item 加 `.target-selectable.active` 类、注册怪物点击回调
- [x] 2.4 玩家点击怪物 item → `UseCard(handIndex, monsterIndex)` → 退出 `SelectingTarget` → 销毁 ghost、清类
- [x] 2.5 `SelectingTarget` 期间按 ESC 或点击空白区域 → 取消，回弹卡片，回 Idle
- [x] 2.6 `OnPhaseChanged` 检测到 `SelectingTarget` 时强制取消并回弹

## 3. 失败 toast 渲染

- [x] 3.1 `GameScreen.OnSetup` 订阅 `ViewModel.CardPlayFailed`，回调显示 toast
- [x] 3.2 在 `BattlePanel.uxml` 增加 `<Label name="fail-toast" class="fail-toast" />`，初始 `display: none`
- [x] 3.3 `GameViewStyles.uss` 添加 `.fail-toast` 样式：drop-zone 上方居中、半透明红底、`opacity` transition 0.4s
- [x] 3.4 toast 显示逻辑：根据 Reason 映射中文文本（"能量不足" / "现在不是你的回合" / "无效目标" / "卡牌索引错误"），display: flex + opacity 1，1.2 秒后 opacity 0、动画结束 display none
- [x] 3.5 toast 显示期间收到新失败覆盖旧文本

## 4. 怪物意图渲染（基于 PendingCards）

- [x] 4.1 `RefreshMonsters` 中为每只 monster item 渲染其 `PendingCards`（`monster.PendingCards`）
- [x] 4.2 `BattlePanel.uxml` 添加 `intent-container` / `intent-card` / `intent-icon` 模板节点
- [x] 4.3 渲染逻辑：每张 PendingCard 创建一个 `.intent-card`；遍历 card 的 Effects 列表，每条 effect 创建一个 `.intent-icon`，按 EffectKind 加对应类名（`-damage` / `-shield` / `-dot` / `-energy`）和文本
- [x] 4.4 `SplitAcrossAll` 类型的 Damage effect：文本显示为分散后的单体值（如 "3" 而不是 "6"）—— 计算方式 `Math.Max(1, value / activeMonsterCount)`
- [x] 4.5 `DamageDot` 文本格式 `{value}×{duration}`，`EnergyGain` 文本 `+{value}`，其余 `{value}`
- [x] 4.6 USS 给 4 种 `intent-icon-*` 配色（红 / 蓝 / 紫 / 黄）+ 字体 / padding

## 5. Buff 状态条

- [x] 5.1 `GameModel` 新增 `PlayerBuffs : IReadOnlyList<BuffRuntime>` 属性 + `SetPlayerBuffs` 调度 PropertyChanged
- [x] 5.2 `GameViewModel` 新增 `ReactiveProperty<IReadOnlyList<BuffRuntime>> PlayerBuffs`，订阅 PropertyChanged 镜像
- [x] 5.3 PlayerActor 的 `AddBuff` 通过 `_model.AddPlayerBuff` 触发更新
- [x] 5.4 `BattleSystem` DoT tick 时（Change 1 已经实现）通知 `_model.SetPlayerBuffs(_playerActor.Buffs)`
- [x] 5.5 `GameScreen` 渲染玩家 buff 条：在 `player-status` 区域旁加 `.buff-bar`，订阅 `ViewModel.PlayerBuffs.Changed` 刷新
- [x] 5.6 `GameScreen` 渲染每只怪物 buff 条：在 `RefreshMonsters` 中给每个 monster item 内嵌 `.buff-bar`
- [x] 5.7 USS `.buff-icon-dot` 紫色 + 文本 `{value}×{turns}`

## 6. 测试

- [x] 6.1 手测：能量不足拖拽出牌 → 看到红色 "能量不足" toast
- [x] 6.2 手测：玩家回合外尝试拖拽（不可能正常达到，保留作 Phase 切换边缘情况）
- [x] 6.3 手测：法术卡拖入 drop-zone → 怪物高亮 → 点击其中一只 → 出牌成功，目标怪物受 8 伤 + 出现 DoT buff 图标
- [x] 6.4 手测：法术 DoT 持续 3 回合，每个 MonsterTurn 起点扣 2 伤，3 回合后 buff 图标消失
- [x] 6.5 手测：投射卡对 2 只怪物 → 意图区分别显示 3 / 3
- [x] 6.6 手测：史莱姆剧本 [攻 / 盾 / 攻] 在意图区按回合切换显示对应 icon
- [x] 6.7 跑 unity-compile-check 通过；现有 EditMode 测试不破坏

## 7. 文档与归档

- [x] 7.1 在 `add-card-rogue-core-loop/tasks.md` 中标记本变更已完成（路书最后一个）
- [x] 7.2 通过 `/opsx:verify` 校验本变更交付（`openspec validate --strict` 通过；EditMode 258/258 全绿）
- [x] 7.3 通过 `/opsx:archive` 归档本变更
