# 增加敌人击败积分并同步玩法 UI（add-enemy-kill-score-ui）

## 问题
飞机大战玩法已具备积分模型与 UI 显示入口，但缺少“敌人死亡 -> 加分”闭环，导致：

1. 击败敌人后积分不增长或增长不稳定。
2. 主玩法界面与暂停菜单的积分文案不统一。
3. 在热更新环境下，计分逻辑曾出现 `VerificationException`（泛型调用校验失败）。

## 根因
1. `EnemyEntity` 死亡流程没有稳定写入 `GamePlayModel.AddScore`。
2. 初版实现中，积分“已结算”标记设置时机过早，模型不可用时会导致本次击杀静默漏记。
3. `GamePlayModel` 仅依赖 UI 侧获取时隐式创建，存在计分对 UI 初始化时序的耦合。
4. 在 HybridCLR 场景中，`TryGetModel<GamePlayModel>()` 泛型路径触发了 IL 校验异常。

## 修复
1. **补齐击败加分链路**
   - 在 `EnemyEntity.TakeDamage` 的死亡分支调用 `TryAwardKillScore()`。
   - 每次有效击败固定加 `1` 分。
2. **修复漏记分时序**
   - 调整为“加分成功后再设置 `_hasAwardedKillScore = true`”。
   - 在 `DelayedHide` 前增加一次兜底重试，避免短暂时序问题导致漏记。
3. **消除 UI 初始化隐式依赖**
   - 在 `GameLogicEntry.InitializeModels()` 中显式注册 `GamePlayModel`。
4. **修复热更新校验异常**
   - 将模型解析改为非泛型 `TryGetModel(typeof(GamePlayModel))`，规避泛型路径校验问题。
5. **统一 UI 文案**
   - `GamePlayView`、`GameMenuView`、`GamePlayView.prefab` 统一为 `击败积分: {score}`。

## 验证
1. `opence validate add-enemy-kill-score-ui --strict` 通过。
2. Unity Editor 手工验证通过：
   - 击败敌人即时加分；
   - 同一敌人不重复计分；
   - 越界回收与流程退出不加分；
   - 主界面与暂停菜单积分同步一致。

## 关键文件
- `Assets/GameScripts/HotFix/GameLogic/GamePlay/Enemy/EnemyEntity.cs`
- `Assets/GameScripts/HotFix/GameLogic/GameLogicEntry.cs`
- `Assets/GameScripts/HotFix/GameLogic/UI/Game/GamePlayView.cs`
- `Assets/GameScripts/HotFix/GameLogic/UI/Game/GameMenuView.cs`
- `Assets/AssetRaw/UI/GamePlay/GamePlayView.prefab`

## 技能检查（Q1-Q4）
1. **Q1: 解决什么问题**
   - 解决“击败计分链路 + 热更新兼容 + UI 同步”的复合问题。
   - 已检查 `opence skill list`，现有技能（plan/work/review/compound/archive、infinite-scroll-background）不直接覆盖该主题。
   - 目前更适合文档沉淀，而非新增独立技能。

2. **Q2: 谁会在何时使用**
   - 使用者：维护飞机大战玩法计分链路、处理 HybridCLR 兼容问题的开发者。
   - 触发语句示例：
     - “为什么击败敌人不加分？”
     - “积分 UI 为什么和暂停菜单不同步？”
     - “HybridCLR 下 TryGetModel 报 VerificationException 怎么办？”
     - “如何避免敌人死亡重复计分？”
     - “游戏重开后积分为什么不对？”

3. **Q3: 技能描述怎么写**
   - 若后续升级为技能，建议描述为：  
     `Validates enemy-kill scoring flow and fixes HybridCLR model-access issues; use when score doesn't update, UI score is inconsistent, or hot-update verification fails.`
   - 含动作词（Validates/Fixes）与用户自然关键词（score、UI、verification）。

4. **Q4: 是否值得创建技能**
   - 预计 6 个月内复用频次低于 3 次。
   - 维护成本高于收益，结论：**暂不创建新技能**，先保留在 `docs/solutions/`。

## 归档前准备（来自 opence-archive）
1. 继续保持 `opence validate add-enemy-kill-score-ui --strict` 通过。
2. 确认 `tasks.md` 全部 `[x]` 且与实际实现一致。
3. 确认复盘文档与技能检查已完成（本文件已完成）。
4. 下一阶段再执行 `/opence-archive`，本阶段不执行 `opence archive`。
